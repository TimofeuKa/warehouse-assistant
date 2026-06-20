using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;
using WarehouseAssistant.Api.Services;

namespace WarehouseAssistant.Api.Endpoints;

public static class MovementEndpoints
{
    public static IEndpointRouteBuilder MapMovementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/movements", async (WarehouseDbContext db, CancellationToken cancellationToken) =>
        {
            var movements = await db.Movements
                .Include(x => x.FromWarehouse)
                .Include(x => x.ToWarehouse)
                .Include(x => x.Items)
                .ThenInclude(x => x.Nomenclature)
                .OrderByDescending(x => x.OccurredAt)
                .Select(x => new MovementResponse(
                    x.Id,
                    x.OccurredAt,
                    x.FromWarehouseId,
                    x.FromWarehouse == null ? null : x.FromWarehouse.Name,
                    x.ToWarehouseId,
                    x.ToWarehouse == null ? null : x.ToWarehouse.Name,
                    x.Items
                        .OrderBy(i => i.Nomenclature.Name)
                        .Select(i => new MovementItemResponse(
                            i.NomenclatureId,
                            i.Nomenclature.Name,
                            i.Quantity))
                        .ToList()))
                .ToListAsync(cancellationToken);

            return Results.Ok(movements);
        });

        app.MapPost("/api/movements", async (
            CreateMovementRequest request,
            MovementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var movement = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/movements/{movement.Id}", new { movement.Id });
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/movements/{id:int}", async (
            int id,
            WarehouseDbContext db,
            CancellationToken cancellationToken) =>
        {
            var movement = await db.Movements.FindAsync([id], cancellationToken);

            if (movement is null)
            {
                return Results.NotFound();
            }

            db.Movements.Remove(movement);
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        return app;
    }
}
