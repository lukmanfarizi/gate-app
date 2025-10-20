using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GateApp.Models;
using GateApp.Utils;
using Microsoft.Extensions.Configuration;
using OpenCvSharp;
using Serilog;

namespace GateApp.Services;

public sealed class CameraService : IDisposable
{
    private readonly List<CameraSession> _sessions = new();
    private readonly ILogger _logger;
    private bool _initialized;
    private bool _disposed;

    public CameraService(IConfiguration configuration, ILogger logger)
    {
        _logger = logger;
        var cameraConfigs = configuration.GetSection("Cameras").Get<List<CameraConfiguration>>() ?? new List<CameraConfiguration>();
        CameraConfigurations = cameraConfigs;
    }

    private IReadOnlyList<CameraConfiguration> CameraConfigurations { get; }

    public void Initialize(IReadOnlyList<PictureBox> pictureBoxes)
    {
        if (_initialized)
        {
            return;
        }

        if (pictureBoxes.Count < CameraConfigurations.Count)
        {
            throw new ArgumentException("Not enough picture boxes provided", nameof(pictureBoxes));
        }

        for (var i = 0; i < CameraConfigurations.Count; i++)
        {
            var config = CameraConfigurations[i];
            var session = new CameraSession(config, pictureBoxes[i], _logger);
            _sessions.Add(session);
        }

        _initialized = true;
    }

    public Task StartStreamingAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();
        foreach (var session in _sessions)
        {
            session.StartAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task<IDictionary<string, byte[]>> CaptureSnapshotsAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();
        var result = new Dictionary<string, byte[]>();
        foreach (var session in _sessions)
        {
            var snapshot = await session.CaptureSnapshotAsync(cancellationToken).ConfigureAwait(false);
            result[session.Name] = snapshot;
        }

        return result;
    }

    public void Stop()
    {
        foreach (var session in _sessions)
        {
            session.Stop();
        }
    }

    public int CameraCount
    {
        get
        {
            EnsureInitialized();
            return _sessions.Count;
        }
    }

    public string GetCameraName(int index)
    {
        EnsureInitialized();
        return _sessions[index].Name;
    }

    public bool IsCameraStreaming(int index)
    {
        EnsureInitialized();
        return _sessions[index].IsStreaming;
    }

    public Task StartCameraAsync(int index, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        _sessions[index].StartAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public void StopCamera(int index)
    {
        EnsureInitialized();
        _sessions[index].Stop();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("CameraService has not been initialized");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _disposed = true;
    }

    private sealed class CameraSession : IDisposable
    {
        private readonly CameraConfiguration _configuration;
        private readonly PictureBox _pictureBox;
        private readonly ILogger _logger;
        private readonly object _frameLock = new();
        private readonly object _stateLock = new();
        private CancellationTokenSource? _cts;
        private Task? _streamingTask;
        private Mat? _latestFrame;
        private bool _isStreaming;
        private bool _disposed;

        public CameraSession(CameraConfiguration configuration, PictureBox pictureBox, ILogger logger)
        {
            _configuration = configuration;
            _pictureBox = pictureBox;
            _logger = logger;
        }

        public string Name => _configuration.Name;

        public bool IsStreaming
        {
            get
            {
                lock (_stateLock)
                {
                    return _isStreaming;
                }
            }
        }

        public void StartAsync(CancellationToken externalToken)
        {
            lock (_stateLock)
            {
                if (_isStreaming)
                {
                    return;
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                var token = _cts.Token;

                _streamingTask = Task.Run(() => StreamAsync(token), CancellationToken.None);
                _isStreaming = true;
            }
        }

        private async Task StreamAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Starting stream for camera {Camera}", _configuration.Name);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var capture = new VideoCapture(_configuration.Rtsp);
                        if (!capture.IsOpened())
                        {
                            _logger.Warning("Unable to open stream for camera {Camera}", _configuration.Name);
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        using var frame = new Mat();
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            if (!capture.Read(frame) || frame.Empty())
                            {
                                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            UpdateLatestFrame(frame);
                            UpdatePictureBox(frame);
                            await Task.Delay(75, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Streaming error for camera {Camera}", _configuration.Name);
                        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _logger.Information("Stopped stream for camera {Camera}", _configuration.Name);
                CleanupStreamingState();
            }
        }

        private void UpdateLatestFrame(Mat frame)
        {
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = frame.Clone();
            }
        }

        private void UpdatePictureBox(Mat frame)
        {
            try
            {
                var bitmap = ImageUtils.MatToBitmap(frame);
                if (_pictureBox.InvokeRequired)
                {
                    _pictureBox.BeginInvoke(new Action(() => AssignBitmap(bitmap)));
                }
                else
                {
                    AssignBitmap(bitmap);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Unable to render frame for camera {Camera}", _configuration.Name);
            }
        }

        private void AssignBitmap(Bitmap bitmap)
        {
            var previous = _pictureBox.Image;
            _pictureBox.Image = bitmap;
            previous?.Dispose();
        }

        public Task<byte[]> CaptureSnapshotAsync(CancellationToken cancellationToken)
        {
            Mat? frame = null;
            lock (_frameLock)
            {
                if (_latestFrame is not null)
                {
                    frame = _latestFrame.Clone();
                }
            }

            if (frame is null)
            {
                _logger.Warning("No frame available for snapshot on camera {Camera}", _configuration.Name);
                return Task.FromResult(Array.Empty<byte>());
            }

            try
            {
                return Task.FromResult(ImageUtils.MatToJpegBytes(frame));
            }
            finally
            {
                frame.Dispose();
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            Task? streamingTask;

            lock (_stateLock)
            {
                cts = _cts;
                streamingTask = _streamingTask;
            }

            if (cts is null)
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already cleaned up.
            }

            try
            {
                streamingTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex)
            {
                _logger.Warning(ex, "Error while stopping camera {Camera}", _configuration.Name);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }

            _disposed = true;
        }

        private void CleanupStreamingState()
        {
            lock (_stateLock)
            {
                _streamingTask = null;
                _cts?.Dispose();
                _cts = null;
                _isStreaming = false;
            }
        }
    }
}
