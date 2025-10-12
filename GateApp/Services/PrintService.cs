using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GateApp.Services;

public sealed class PrintService : IDisposable
{
    private readonly PrintingSettings _settings;
    private readonly ILogger _logger;
    private bool _disposed;

    public PrintService(IConfiguration configuration, ILogger logger)
    {
        _settings = configuration.GetSection("Printing").Get<PrintingSettings>() ?? new PrintingSettings();
        _logger = logger;
    }

    public Task PrintReceiptAsync(ValidateResponse response, CancellationToken cancellationToken)
    {
        if (_settings.UseEscPos)
        {
            _logger.Warning("ESC/POS printing requested but not implemented. Falling back to Windows printing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.PrinterName))
        {
            _logger.Warning("Printer name is not configured. Skipping printing.");
            return Task.CompletedTask;
        }

        var content = BuildReceiptContent(response);
        return Task.Run(() => Print(content), cancellationToken);
    }

    private static string BuildReceiptContent(ValidateResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("*** Gate Receipt ***");
        builder.AppendLine($"Ticket: {response.TicketId}");
        builder.AppendLine($"Plate: {response.PlateNumber}");
        builder.AppendLine($"Driver: {response.DriverName}");
        builder.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (response.AdditionalData is not null)
        {
            foreach (var kvp in response.AdditionalData)
            {
                builder.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
        }

        builder.AppendLine("********************");
        return builder.ToString();
    }

    private void Print(string content)
    {
        try
        {
            using var document = new PrintDocument();
            document.PrinterSettings.PrinterName = _settings.PrinterName;
            document.PrintPage += (_, e) => DrawContent(e, content);
            document.Print();
            _logger.Information("Receipt sent to printer {Printer}", _settings.PrinterName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to print receipt");
        }
    }

    private static void DrawContent(PrintPageEventArgs e, string content)
    {
        using var font = new Font("Consolas", 10f);
        e.Graphics.DrawString(content, font, Brushes.Black, new RectangleF(0, 0, e.MarginBounds.Width, e.MarginBounds.Height));
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
