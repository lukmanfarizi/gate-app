using System.Net.Sockets;
using System.Text;
using System.Threading;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GateApp.Services;

public sealed class GateControllerClient : IDisposable
{
    private readonly GateSettings _settings;
    private readonly ILogger _logger;
    private bool _disposed;

    public GateControllerClient(IConfiguration configuration, ILogger logger)
    {
        _settings = configuration.GetSection("Gate").Get<GateSettings>() ?? new GateSettings();
        _logger = logger;
    }

    public async Task<bool> OpenGateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_settings.ControllerHost, _settings.ControllerPort, cancellationToken).ConfigureAwait(false);
            await using var stream = client.GetStream();
            var commandBytes = Encoding.ASCII.GetBytes(_settings.OpenCommand + "\r\n");
            await stream.WriteAsync(commandBytes.AsMemory(0, commandBytes.Length), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("Gate open command sent to {Host}:{Port}", _settings.ControllerHost, _settings.ControllerPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open gate");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
