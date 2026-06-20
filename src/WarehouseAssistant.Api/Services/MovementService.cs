using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;
using WarehouseAssistant.Api.Domain;

namespace WarehouseAssistant.Api.Services;

public sealed class MovementService(WarehouseDbContext db)
{
    public async Task<Movement> CreateAsync(CreateMovementRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);

        var movement = new Movement
        {
            OccurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Items = request.Items
                .Select(x => new MovementItem
                {
                    NomenclatureId = x.NomenclatureId,
                    Quantity = x.Quantity
                })
                .ToList()
        };

        db.Movements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return movement;
    }

    private async Task ValidateAsync(CreateMovementRequest request, CancellationToken cancellationToken)
    {
        if (request.FromWarehouseId is null && request.ToWarehouseId is null)
        {
            throw new ValidationException("Укажите склад отправления или склад получения.");
        }

        if (request.FromWarehouseId == request.ToWarehouseId && request.FromWarehouseId is not null)
        {
            throw new ValidationException("Склад отправления и склад получения не должны совпадать.");
        }

        if (request.Items.Count == 0)
        {
            throw new ValidationException("Добавьте хотя бы одну ТМЦ.");
        }

        if (request.Items.Any(x => x.Quantity <= 0))
        {
            throw new ValidationException("Количество должно быть больше нуля.");
        }

        var duplicateNomenclature = request.Items
            .GroupBy(x => x.NomenclatureId)
            .Any(x => x.Count() > 1);

        if (duplicateNomenclature)
        {
            throw new ValidationException("Номенклатуры в одном движении не должны повторяться.");
        }

        var warehouseIds = new[] { request.FromWarehouseId, request.ToWarehouseId }
            .OfType<int>()
            .Distinct()
            .ToArray();

        var existingWarehouses = await db.Warehouses
            .CountAsync(x => warehouseIds.Contains(x.Id), cancellationToken);

        if (existingWarehouses != warehouseIds.Length)
        {
            throw new ValidationException("Один из складов не найден.");
        }

        var nomenclatureIds = request.Items
            .Select(x => x.NomenclatureId)
            .Distinct()
            .ToArray();

        var existingNomenclatures = await db.Nomenclatures
            .CountAsync(x => nomenclatureIds.Contains(x.Id), cancellationToken);

        if (existingNomenclatures != nomenclatureIds.Length)
        {
            throw new ValidationException("Одна из номенклатур не найдена.");
        }

        if (request.FromWarehouseId is not null)
        {
            await EnsureEnoughStockAsync(
                request.FromWarehouseId.Value,
                request.OccurredAt ?? DateTimeOffset.UtcNow,
                request.Items,
                cancellationToken);
        }
    }

    private async Task EnsureEnoughStockAsync(
        int warehouseId,
        DateTimeOffset at,
        IReadOnlyList<MovementItemRequest> requestedItems,
        CancellationToken cancellationToken)
    {
        var nomenclatureIds = requestedItems
            .Select(x => x.NomenclatureId)
            .Distinct()
            .ToArray();

        var incomingRows = await db.MovementItems
            .Where(x =>
                x.Movement.ToWarehouseId == warehouseId &&
                x.Movement.OccurredAt <= at &&
                nomenclatureIds.Contains(x.NomenclatureId))
            .GroupBy(x => x.NomenclatureId)
            .Select(x => new
            {
                NomenclatureId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            })
            .ToDictionaryAsync(x => x.NomenclatureId, x => x.Quantity, cancellationToken);

        var outgoingRows = await db.MovementItems
            .Where(x =>
                x.Movement.FromWarehouseId == warehouseId &&
                x.Movement.OccurredAt <= at &&
                nomenclatureIds.Contains(x.NomenclatureId))
            .GroupBy(x => x.NomenclatureId)
            .Select(x => new
            {
                NomenclatureId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            })
            .ToDictionaryAsync(x => x.NomenclatureId, x => x.Quantity, cancellationToken);

        foreach (var item in requestedItems)
        {
            incomingRows.TryGetValue(item.NomenclatureId, out var received);
            outgoingRows.TryGetValue(item.NomenclatureId, out var spent);

            var available = received - spent;
            if (available < item.Quantity)
            {
                var nomenclatureName = await db.Nomenclatures
                    .Where(x => x.Id == item.NomenclatureId)
                    .Select(x => x.Name)
                    .SingleAsync(cancellationToken);

                throw new ValidationException(
                    $"Недостаточно остатка для номенклатуры '{nomenclatureName}'. Доступно: {available}, требуется: {item.Quantity}.");
            }
        }
    }
}
