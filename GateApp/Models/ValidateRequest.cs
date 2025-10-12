namespace GateApp.Models;

public sealed class ValidateRequest
{
    public string QrCode { get; init; } = string.Empty;
    public string GateId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? OperatorId { get; init; }
}
