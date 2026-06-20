namespace WarehouseAssistant.Api.Contracts;

public sealed record MovementItemRequest(
    int NomenclatureId,
    int Quantity);

public sealed record CreateMovementRequest(
    DateTimeOffset? OccurredAt,
    int? FromWarehouseId,
    int? ToWarehouseId,
    IReadOnlyList<MovementItemRequest>? Items);

public sealed record MovementItemResponse(
    int NomenclatureId,
    string NomenclatureName,
    int Quantity);

public sealed record MovementResponse(
    int Id,
    DateTimeOffset OccurredAt,
    int? FromWarehouseId,
    string? FromWarehouseName,
    int? ToWarehouseId,
    string? ToWarehouseName,
    IReadOnlyList<MovementItemResponse> Items);
