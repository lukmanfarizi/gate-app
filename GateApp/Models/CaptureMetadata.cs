namespace GateApp.Models;

public sealed class CaptureMetadata
{
    public string GateId { get; init; } = string.Empty;
    public string TicketId { get; init; } = string.Empty;
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    public string CameraName { get; init; } = string.Empty;
    public string? PlateNumber { get; init; }
}
