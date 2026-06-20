namespace WarehouseAssistant.Api.Contracts;

public sealed record CatalogItemDto(int Id, string Name);

public sealed record CatalogsResponse(
    IReadOnlyList<CatalogItemDto> Warehouses,
    IReadOnlyList<CatalogItemDto> Nomenclatures);
