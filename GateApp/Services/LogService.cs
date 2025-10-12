using System.IO;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace GateApp.Services;

public sealed class LogService : IDisposable
{
    private bool _disposed;

    private LogService(Logger logger)
    {
        Logger = logger;
    }

    public Logger Logger { get; }

    public static LogService Create(IConfiguration configuration)
    {
        var loggingSettings = configuration.GetSection("Logging").Get<LoggingSettings>() ?? new LoggingSettings();

        var rollingInterval = Enum.TryParse(loggingSettings.RollingInterval, ignoreCase: true, out RollingInterval parsed)
            ? parsed
            : RollingInterval.Day;

        Directory.CreateDirectory(Path.GetDirectoryName(loggingSettings.Path) ?? "logs");

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .WriteTo.File(loggingSettings.Path,
                          rollingInterval: rollingInterval,
                          retainedFileCountLimit: loggingSettings.RetainedFileCountLimit,
                          shared: true)
            .WriteTo.Console()
            .CreateLogger();

        return new LogService(logger);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Logger.Dispose();
        _disposed = true;
    }
}
