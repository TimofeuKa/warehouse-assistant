namespace WarehouseAssistant.Api.Domain;

using System;

public sealed class Nomenclature
{
    public int Id { get; set; }
    public required string Name { get; set; }
}