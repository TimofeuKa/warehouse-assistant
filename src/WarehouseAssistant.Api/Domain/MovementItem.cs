namespace WarehouseAssistant.Api.Domain;

using System;

public sealed class MovementItem
{
    public int Id { get; set; }

    public int MovementId { get; set; }
    public Movement Movement { get; set; } = null!;

    public int NomenclatureId { get; set; }
    public Nomenclature Nomenclature { get; set; } = null!;

    public decimal Quantity { get; set; }
}
