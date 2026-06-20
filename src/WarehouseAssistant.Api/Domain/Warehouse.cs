namespace WarehouseAssistant.Api.Domain;

using System;

public sealed class Warehouse
{
    public int Id { get; set; }
    public required string Name { get; set; }
}