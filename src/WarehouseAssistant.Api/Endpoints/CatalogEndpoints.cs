using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;

namespace WarehouseAssistant.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalogs", async (WarehouseDbContext db, CancellationToken cancellationToken) =>
        {
            var warehouses = await db.Warehouses
                .OrderBy(x => x.Name)
                .Select(x => new CatalogItemDto(x.Id, x.Name))
                .ToListAsync(cancellationToken);

            var nomenclatures = await db.Nomenclatures
                .OrderBy(x => x.Name)
                .Select(x => new CatalogItemDto(x.Id, x.Name))
                .ToListAsync(cancellationToken);

            return Results.Ok(new CatalogsResponse(warehouses, nomenclatures));
        });

        return app;
    }
}
