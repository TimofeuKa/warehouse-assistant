namespace WarehouseAssistant.Api.Contracts;

public sealed record StockRowResponse(
    int NomenclatureId,
    string NomenclatureName,
    int Quantity);
