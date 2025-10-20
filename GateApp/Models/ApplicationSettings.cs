using System.Collections.Generic;

namespace GateApp.Models;

public sealed class ApplicationSettings
{
    public GateSettings Gate { get; init; } = new();
    public ApiSettings Api { get; init; } = new();
    public IReadOnlyDictionary<string, ApiSettings> Apis { get; init; } = new Dictionary<string, ApiSettings>();
    public IReadOnlyList<CameraConfiguration> Cameras { get; init; } = Array.Empty<CameraConfiguration>();
    public ScannerSettings Scanner { get; init; } = new();
    public PrintingSettings Printing { get; init; } = new();
    public LoggingSettings Logging { get; init; } = new();
}

public sealed class GateSettings
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = "IN";
    public string ControllerHost { get; init; } = string.Empty;
    public int ControllerPort { get; init; }
    public string OpenCommand { get; init; } = "OPEN";
}

public sealed class ApiSettings
{
    public string BaseUrl { get; init; } = string.Empty;
    public string GateInEndpoint { get; init; } = string.Empty;
    public string GateOutEndpoint { get; init; } = string.Empty;
    public string CaptureEndpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string LoginEndpoint { get; init; } = string.Empty;
    public string LoginUsername { get; init; } = string.Empty;
    public string LoginEmail { get; init; } = string.Empty;
    public string LoginPassword { get; init; } = string.Empty;
    public string DepotId { get; init; } = string.Empty;
    public bool UseAuthorizationHeader { get; init; } = true;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public int RetryCount { get; init; } = 3;
    public double RetryBackoffSeconds { get; init; } = 1.0;
}

public sealed class ScannerSettings
{
    public string Mode { get; init; } = "KeyboardWedge";
    public string SerialPort { get; init; } = "COM1";
    public int BaudRate { get; init; } = 9600;
    public string EndSequence { get; init; } = "\r";
}

public sealed class PrintingSettings
{
    public bool UseEscPos { get; init; }
    public string PrinterName { get; init; } = string.Empty;
}

public sealed class LoggingSettings
{
    public string Path { get; init; } = "logs/gateapp-.log";
    public string RollingInterval { get; init; } = "Day";
    public int? RetainedFileCountLimit { get; init; }
}
