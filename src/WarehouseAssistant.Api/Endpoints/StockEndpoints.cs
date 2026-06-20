using Microsoft.AspNetCore.Mvc;
using WarehouseAssistant.Api.Services;

namespace WarehouseAssistant.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks", async (
            int warehouseId,
            DateTimeOffset? at,
            [FromServices] StockService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var rows = await service.GetStockAsync(
                    warehouseId,
                    at ?? DateTimeOffset.UtcNow,
                    cancellationToken);

                return Results.Ok(rows);
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
