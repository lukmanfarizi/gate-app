using System.Collections.Generic;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GateApp.Models;
using GateApp.Services;
using Microsoft.Extensions.Configuration;

namespace GateApp.Forms;

public partial class MainForm : Form
{
    private readonly IConfiguration _configuration;
    private readonly ApiService _apiService;
    private readonly CameraService _cameraService;
    private readonly GateControllerClient _gateControllerClient;
    private readonly PrintService _printService;
    private readonly ScannerService _scannerService;
    private readonly LogService _logService;
    private readonly ApplicationSettings _settings;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private CancellationTokenSource? _operationCts;

    public MainForm(IConfiguration configuration,
                    ApiService apiService,
                    CameraService cameraService,
                    GateControllerClient gateControllerClient,
                    PrintService printService,
                    ScannerService scannerService,
                    LogService logService)
    {
        _configuration = configuration;
        _apiService = apiService;
        _cameraService = cameraService;
        _gateControllerClient = gateControllerClient;
        _printService = printService;
        _scannerService = scannerService;
        _logService = logService;
        _settings = configuration.Get<ApplicationSettings>() ?? new ApplicationSettings();

        InitializeComponent();
        InitializeServices();
    }

    private void InitializeServices()
    {
        _cameraService.Initialize(new[] { pictureBox1, pictureBox2, pictureBox3, pictureBox4 });
        scannerTextBox.KeyDown += ScannerTextBoxOnKeyDown;
        scannerTextBox.GotFocus += ScannerTextBoxOnGotFocus;
        scannerTextBox.LostFocus += ScannerTextBoxOnLostFocus;
        Activated += (_, _) => FocusScanner();
        topPanel.MouseDown += ControlOnMouseDown;
        scannerLabel.MouseDown += ControlOnMouseDown;
        tableLayoutPanel.MouseDown += ControlOnMouseDown;
        bottomPanel.MouseDown += ControlOnMouseDown;
        logTextBox.MouseDown += ControlOnMouseDown;
        statusStrip.MouseDown += ControlOnMouseDown;
        pictureBox1.MouseDown += ControlOnMouseDown;
        pictureBox2.MouseDown += ControlOnMouseDown;
        pictureBox3.MouseDown += ControlOnMouseDown;
        pictureBox4.MouseDown += ControlOnMouseDown;
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        FocusScanner();
        _scannerService.Start();
        _ = Task.Run(async () => await _cameraService.StartStreamingAsync(_lifetimeCts.Token));
        UpdateStatus(apiStatusLabel, "API: Ready");
        UpdateStatus(cameraStatusLabel, "Cameras: Streaming");
        UpdateStatus(gateStatusLabel, "Gate: Ready");
        LogMessage("GateApp ready. Waiting for scans...");
    }

    private void ScannerTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        var qrCode = scannerTextBox.Text.Trim();
        scannerTextBox.Clear();

        _ = HandleQrCodeAsync(qrCode);
    }

    private async Task HandleQrCodeAsync(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            return;
        }

        if (!await _processingSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            LogMessage("System busy. Please wait...");
            SystemSounds.Beep.Play();
            return;
        }

        try
        {
            await InvokeAsync(() =>
            {
                UpdateStatus(apiStatusLabel, "API: Validating");
                UpdateStatus(gateStatusLabel, "Gate: Pending");
            }).ConfigureAwait(false);

            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _operationCts = operationCts;
            var token = operationCts.Token;

            LogMessage($"QR scanned: {qrCode}");
            var validateRequest = new ValidateRequest
            {
                QrCode = qrCode,
                GateId = _settings.Gate.Id,
                Timestamp = DateTime.UtcNow
            };

            var validateResponse = await _apiService.ValidateAsync(validateRequest, token).ConfigureAwait(false);
            if (validateResponse is null || !validateResponse.Success)
            {
                var message = validateResponse?.Message ?? "Validation failed";
                LogMessage($"Validation failed: {message}");
                await InvokeAsync(() =>
                {
                    UpdateStatus(apiStatusLabel, "API: Failed");
                    UpdateStatus(gateStatusLabel, "Gate: Locked");
                }).ConfigureAwait(false);
                SystemSounds.Beep.Play();
                return;
            }

            await InvokeAsync(() => UpdateStatus(apiStatusLabel, "API: Success")).ConfigureAwait(false);

            var additionalPayload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(validateResponse.PlateNumber))
            {
                additionalPayload["plateNumber"] = validateResponse.PlateNumber;
            }

            if (!string.IsNullOrWhiteSpace(validateResponse.DriverName))
            {
                additionalPayload["driverName"] = validateResponse.DriverName;
            }

            if (validateResponse.AdditionalData is not null)
            {
                foreach (var item in validateResponse.AdditionalData)
                {
                    additionalPayload[item.Key] = item.Value;
                }
            }

            await InvokeAsync(() => UpdateStatus(cameraStatusLabel, "Cameras: Capturing")).ConfigureAwait(false);
            var snapshots = await _cameraService.CaptureSnapshotsAsync(token).ConfigureAwait(false);
            LogMessage($"Captured {snapshots.Count} snapshots");

            await InvokeAsync(() => UpdateStatus(apiStatusLabel, "API: Uploading Capture")).ConfigureAwait(false);
            var captureUploaded = await _apiService.SendCaptureAsync(qrCode,
                                                                     validateResponse.TicketId ?? qrCode,
                                                                     _settings.Gate.Id,
                                                                     snapshots,
                                                                     additionalPayload,
                                                                     token).ConfigureAwait(false);
            if (!captureUploaded)
            {
                LogMessage("Capture upload failed. Continuing workflow.");
            }
            else
            {
                LogMessage("Capture uploaded successfully.");
            }

            await InvokeAsync(() => UpdateStatus(gateStatusLabel, "Gate: Opening")).ConfigureAwait(false);
            var gateOpened = await _gateControllerClient.OpenGateAsync(token).ConfigureAwait(false);
            if (gateOpened)
            {
                LogMessage("Gate open command sent.");
                await InvokeAsync(() => UpdateStatus(gateStatusLabel, "Gate: Open command sent")).ConfigureAwait(false);
            }
            else
            {
                LogMessage("Failed to open gate. Operator intervention required.");
                await InvokeAsync(() => UpdateStatus(gateStatusLabel, "Gate: Error")).ConfigureAwait(false);
            }

            await _printService.PrintReceiptAsync(validateResponse, token).ConfigureAwait(false);
            LogMessage("Print job dispatched.");
            await InvokeAsync(() =>
            {
                UpdateStatus(apiStatusLabel, "API: Ready");
                UpdateStatus(cameraStatusLabel, "Cameras: Ready");
                UpdateStatus(gateStatusLabel, gateOpened ? "Gate: Open" : "Gate: Error");
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.Logger.Error(ex, "Error while processing QR code");
            LogMessage($"Unexpected error: {ex.Message}");
            await InvokeAsync(() =>
            {
                UpdateStatus(apiStatusLabel, "API: Error");
                UpdateStatus(cameraStatusLabel, "Cameras: Ready");
                UpdateStatus(gateStatusLabel, "Gate: Error");
            }).ConfigureAwait(false);
            SystemSounds.Beep.Play();
        }
        finally
        {
            _operationCts?.Dispose();
            _operationCts = null;
            _processingSemaphore.Release();
            FocusScanner();
        }
    }

    private void LogMessage(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logService.Logger.Information(message);
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(line)));
        }
        else
        {
            AppendLog(line);
        }
    }

    private void AppendLog(string line)
    {
        logTextBox.AppendText(line + Environment.NewLine);
        logTextBox.SelectionStart = logTextBox.TextLength;
        logTextBox.ScrollToCaret();
    }

    private void UpdateStatus(ToolStripStatusLabel label, string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => label.Text = text));
        }
        else
        {
            label.Text = text;
        }
    }

    private Task InvokeAsync(Action action)
    {
        if (IsHandleCreated && InvokeRequired)
        {
            return Task.Run(() => BeginInvoke(action));
        }

        action();
        return Task.CompletedTask;
    }

    private void FocusScanner()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(FocusScanner));
            return;
        }

        scannerTextBox.Focus();
        scannerTextBox.Select(scannerTextBox.TextLength, 0);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _operationCts?.Cancel();
        _lifetimeCts.Cancel();
        scannerTextBox.KeyDown -= ScannerTextBoxOnKeyDown;
        scannerTextBox.GotFocus -= ScannerTextBoxOnGotFocus;
        scannerTextBox.LostFocus -= ScannerTextBoxOnLostFocus;
        topPanel.MouseDown -= ControlOnMouseDown;
        scannerLabel.MouseDown -= ControlOnMouseDown;
        tableLayoutPanel.MouseDown -= ControlOnMouseDown;
        bottomPanel.MouseDown -= ControlOnMouseDown;
        logTextBox.MouseDown -= ControlOnMouseDown;
        statusStrip.MouseDown -= ControlOnMouseDown;
        pictureBox1.MouseDown -= ControlOnMouseDown;
        pictureBox2.MouseDown -= ControlOnMouseDown;
        pictureBox3.MouseDown -= ControlOnMouseDown;
        pictureBox4.MouseDown -= ControlOnMouseDown;
    }

    private void ScannerTextBoxOnGotFocus(object? sender, EventArgs e)
    {
        scannerTextBox.Select(scannerTextBox.TextLength, 0);
    }

    private void ScannerTextBoxOnLostFocus(object? sender, EventArgs e)
    {
        FocusScanner();
    }

    private void ControlOnMouseDown(object? sender, MouseEventArgs e)
    {
        FocusScanner();
    }
}
