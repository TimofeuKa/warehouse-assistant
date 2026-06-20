namespace WarehouseAssistant.Api.Services;

public sealed class ValidationException(string message) : Exception(message);