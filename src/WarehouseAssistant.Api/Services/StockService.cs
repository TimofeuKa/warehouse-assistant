using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;

namespace WarehouseAssistant.Api.Services;

public sealed class StockService(WarehouseDbContext db)
{
    public async Task<IReadOnlyList<StockRowResponse>> GetStockAsync(
        int warehouseId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var warehouseExists = await db.Warehouses
            .AnyAsync(x => x.Id == warehouseId, cancellationToken);

        if (!warehouseExists)
        {
            throw new ValidationException("Склад не найден.");
        }

        var incoming = db.MovementItems
            .Where(x => x.Movement.ToWarehouseId == warehouseId && x.Movement.OccurredAt <= at)
            .GroupBy(x => x.NomenclatureId)
            .Select(x => new
            {
                NomenclatureId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            });

        var outgoing = db.MovementItems
            .Where(x => x.Movement.FromWarehouseId == warehouseId && x.Movement.OccurredAt <= at)
            .GroupBy(x => x.NomenclatureId)
            .Select(x => new
            {
                NomenclatureId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            });

        var incomingRows = await incoming
            .ToDictionaryAsync(x => x.NomenclatureId, x => x.Quantity, cancellationToken);

        var outgoingRows = await outgoing
            .ToDictionaryAsync(x => x.NomenclatureId, x => x.Quantity, cancellationToken);

        var nomenclatures = await db.Nomenclatures
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return nomenclatures
            .Select(x =>
            {
                incomingRows.TryGetValue(x.Id, out var received);
                outgoingRows.TryGetValue(x.Id, out var spent);

                return new StockRowResponse(
                    x.Id,
                    x.Name,
                    received - spent);
            })
            .ToList();
    }
}