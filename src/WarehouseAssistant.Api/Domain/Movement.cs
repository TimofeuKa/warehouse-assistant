namespace WarehouseAssistant.Api.Domain;

using System;

public sealed class Movement
{
    public int Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public int? FromWarehouseId { get; set; }
    public Warehouse? FromWarehouse { get; set; }

    public int? ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }

    public List<MovementItem> Items { get; set; } = [];
}
