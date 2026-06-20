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
            MovementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var deleted = await service.DeleteAsync(id, cancellationToken);

                return deleted
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
