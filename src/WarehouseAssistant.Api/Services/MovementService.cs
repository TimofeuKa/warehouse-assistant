using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;
using WarehouseAssistant.Api.Domain;

namespace WarehouseAssistant.Api.Services;

public sealed class MovementService(WarehouseDbContext db)
{
    public async Task<Movement> CreateAsync(CreateMovementRequest request, CancellationToken cancellationToken = default)
    {
        var items = await ValidateAsync(request, cancellationToken);

        var movement = new Movement
        {
            OccurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Items = items
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

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var movement = await db.Movements
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (movement is null)
        {
            return false;
        }

        if (movement.ToWarehouseId is not null)
        {
            var nomenclatureIds = movement.Items
                .Select(x => x.NomenclatureId)
                .ToArray();

            var timeline = await LoadStockTimelineAsync(
                movement.ToWarehouseId.Value,
                nomenclatureIds,
                excludedMovementId: movement.Id,
                cancellationToken);

            foreach (var item in movement.Items)
            {
                var firstNegativeAt = FindFirstNegativeMoment(
                    timeline.Where(x => x.NomenclatureId == item.NomenclatureId));

                if (firstNegativeAt is not null)
                {
                    var nomenclatureName = await GetNomenclatureNameAsync(
                        item.NomenclatureId,
                        cancellationToken);

                    throw new ValidationException(
                        $"Нельзя удалить движение: это приведет к отрицательному остатку " +
                        $"номенклатуры '{nomenclatureName}' на {firstNegativeAt:dd.MM.yyyy HH:mm}.");
                }
            }
        }

        db.Movements.Remove(movement);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<IReadOnlyList<MovementItemRequest>> ValidateAsync(
        CreateMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FromWarehouseId is null && request.ToWarehouseId is null)
        {
            throw new ValidationException("Укажите склад отправления или склад получения.");
        }

        if (request.FromWarehouseId == request.ToWarehouseId && request.FromWarehouseId is not null)
        {
            throw new ValidationException("Склад отправления и склад получения не должны совпадать.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ValidationException("Добавьте хотя бы одну ТМЦ.");
        }

        var items = request.Items;

        if (items.Any(x => x.Quantity <= 0))
        {
            throw new ValidationException("Количество должно быть больше нуля.");
        }

        var duplicateNomenclature = items
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

        var nomenclatureIds = items
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
                items,
                cancellationToken);
        }

        return items;
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

        var timeline = await LoadStockTimelineAsync(
            warehouseId,
            nomenclatureIds,
            excludedMovementId: null,
            cancellationToken);

        foreach (var item in requestedItems)
        {
            var itemTimeline = timeline
                .Where(x => x.NomenclatureId == item.NomenclatureId)
                .Append(new StockDelta(item.NomenclatureId, at, -item.Quantity));

            var firstNegativeAt = FindFirstNegativeMoment(itemTimeline);
            if (firstNegativeAt is not null)
            {
                var nomenclatureName = await GetNomenclatureNameAsync(
                    item.NomenclatureId,
                    cancellationToken);

                throw new ValidationException(
                    $"Недостаточно остатка для номенклатуры '{nomenclatureName}'. " +
                    $"Операция приведет к отрицательному остатку на {firstNegativeAt:dd.MM.yyyy HH:mm}.");
            }
        }
    }

    private async Task<List<StockDelta>> LoadStockTimelineAsync(
        int warehouseId,
        IReadOnlyCollection<int> nomenclatureIds,
        int? excludedMovementId,
        CancellationToken cancellationToken)
    {
        var query = db.MovementItems
            .Where(x =>
                nomenclatureIds.Contains(x.NomenclatureId) &&
                (x.Movement.ToWarehouseId == warehouseId ||
                 x.Movement.FromWarehouseId == warehouseId));

        if (excludedMovementId is not null)
        {
            query = query.Where(x => x.MovementId != excludedMovementId.Value);
        }

        return await query
            .Select(x => new StockDelta(
                x.NomenclatureId,
                x.Movement.OccurredAt,
                x.Movement.ToWarehouseId == warehouseId ? x.Quantity : -x.Quantity))
            .ToListAsync(cancellationToken);
    }

    private async Task<string> GetNomenclatureNameAsync(
        int nomenclatureId,
        CancellationToken cancellationToken)
    {
        return await db.Nomenclatures
            .Where(x => x.Id == nomenclatureId)
            .Select(x => x.Name)
            .SingleAsync(cancellationToken);
    }

    private static DateTimeOffset? FindFirstNegativeMoment(IEnumerable<StockDelta> timeline)
    {
        var balance = 0;

        foreach (var moment in timeline
                     .GroupBy(x => x.OccurredAt)
                     .OrderBy(x => x.Key))
        {
            balance += moment.Sum(x => x.Quantity);

            if (balance < 0)
            {
                return moment.Key;
            }
        }

        return null;
    }

    private sealed record StockDelta(
        int NomenclatureId,
        DateTimeOffset OccurredAt,
        int Quantity);
}
