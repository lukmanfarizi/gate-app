using System.IO.Ports;
using System.Text;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GateApp.Services;

public sealed class ScannerService : IDisposable
{
    private readonly ScannerSettings _settings;
    private readonly ILogger _logger;
    private readonly StringBuilder _buffer = new();
    private SerialPort? _serialPort;
    private bool _disposed;

    public ScannerService(IConfiguration configuration, ILogger logger)
    {
        _settings = configuration.GetSection("Scanner").Get<ScannerSettings>() ?? new ScannerSettings();
        _logger = logger;
    }

    public event EventHandler<string>? QrCodeReceived;

    public void Start()
    {
        if (_settings.Mode.Equals("Serial", StringComparison.OrdinalIgnoreCase))
        {
            InitializeSerialPort();
        }
        else
        {
            _logger.Information("Scanner running in {Mode} mode", _settings.Mode);
        }
    }

    private void InitializeSerialPort()
    {
        try
        {
            _serialPort = new SerialPort(_settings.SerialPort, _settings.BaudRate)
            {
                DtrEnable = true,
                RtsEnable = true,
                NewLine = _settings.EndSequence
            };
            _serialPort.DataReceived += SerialPortOnDataReceived;
            _serialPort.Open();
            _logger.Information("Serial scanner connected on {Port}", _settings.SerialPort);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open serial port {Port}", _settings.SerialPort);
        }
    }

    private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is null)
        {
            return;
        }

        try
        {
            var data = _serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(data))
            {
                foreach (var ch in data)
                {
                    ProcessCharacter(ch);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading from serial scanner");
        }
    }

    public void ProcessCharacter(char ch)
    {
        _buffer.Append(ch);
        var endSequence = _settings.EndSequence;
        if (_buffer.Length < endSequence.Length)
        {
            return;
        }

        if (_buffer.ToString().EndsWith(endSequence, StringComparison.Ordinal))
        {
            var code = _buffer.ToString()[..^endSequence.Length];
            _buffer.Clear();
            if (!string.IsNullOrWhiteSpace(code))
            {
                _logger.Information("QR code received: {Code}", code);
                QrCodeReceived?.Invoke(this, code.Trim());
            }
        }
    }

    public void ResetBuffer()
    {
        _buffer.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_serialPort is not null)
        {
            _serialPort.DataReceived -= SerialPortOnDataReceived;
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
        }

        _disposed = true;
    }
}
