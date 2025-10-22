using System.Collections.Generic;

namespace GateApp.Models;

public sealed class ValidateResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TicketId { get; init; }
    public string? PlateNumber { get; init; }
    public string? DriverName { get; init; }
    public string? Data { get; set; }
    public IDictionary<string, string>? AdditionalData { get; init; }
}
