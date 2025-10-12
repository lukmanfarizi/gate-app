using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Serilog;

namespace GateApp.Services;

public sealed class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly ApiSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _httpRetryPolicy;
    private bool _disposed;

    public ApiService(IConfiguration configuration, ILogger logger)
    {
        _logger = logger;
        _settings = configuration.GetSection("Api").Get<ApiSettings>() ?? new ApiSettings();
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("API BaseUrl is not configured.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_settings.BaseUrl),
            Timeout = _settings.Timeout
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        var retryCount = Math.Max(0, _settings.RetryCount);
        var baseDelay = _settings.RetryBackoffSeconds <= 0 ? 1 : _settings.RetryBackoffSeconds;
        _httpRetryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(response => (int)response.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(baseDelay * Math.Pow(2, attempt - 1)),
                (outcome, span, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                    {
                        _logger.Warning(outcome.Exception, "HTTP request failed on attempt {Attempt}. Retrying in {Delay}.", attempt, span);
                    }
                    else
                    {
                        _logger.Warning("HTTP request returned status {Status} on attempt {Attempt}. Retrying in {Delay}.", outcome.Result.StatusCode, attempt, span);
                    }
                });
    }

    public async Task<ValidateResponse?> ValidateAsync(ValidateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.Information("Sending validate request for QR {QrCode}", request.QrCode);
            using var response = await _httpRetryPolicy
                .ExecuteAsync(ct => _httpClient.PostAsJsonAsync(_settings.ValidateEndpoint, request, ct), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.Warning("Validate failed with status {Status}: {Message}", response.StatusCode, error);
                return new ValidateResponse
                {
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(error) ? response.ReasonPhrase ?? "Unknown error" : error
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<ValidateResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return payload;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while calling validate endpoint");
            return new ValidateResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> SendCaptureAsync(string ticketId, string gateId, IDictionary<string, byte[]> snapshots, IDictionary<string, string>? additionalPayload, CancellationToken cancellationToken)
    {
        try
        {
            var capturedAtInstant = DateTime.UtcNow;
            var capturedAt = capturedAtInstant.ToString("O");
            using var response = await _httpRetryPolicy
                .ExecuteAsync(async ct =>
                {
                    var content = BuildCaptureForm(ticketId, gateId, capturedAt, capturedAtInstant, snapshots, additionalPayload);
                    try
                    {
                        return await _httpClient.PostAsync(_settings.CaptureEndpoint, content, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        content.Dispose();
                    }
                }, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.Warning("Capture upload failed with status {Status}: {Message}", response.StatusCode, error);
                return false;
            }

            _logger.Information("Capture uploaded for ticket {TicketId}", ticketId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while sending capture");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private static MultipartFormDataContent BuildCaptureForm(
        string ticketId,
        string gateId,
        string capturedAt,
        DateTime capturedAtInstant,
        IDictionary<string, byte[]> snapshots,
        IDictionary<string, string>? additionalPayload)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(ticketId), "ticketId");
        form.Add(new StringContent(gateId), "gateId");
        form.Add(new StringContent(capturedAt), "capturedAt");

        if (additionalPayload is not null)
        {
            var json = JsonSerializer.Serialize(additionalPayload);
            form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "metadata");
        }

        foreach (var (cameraName, bytes) in snapshots)
        {
            if (bytes.Length == 0)
            {
                continue;
            }

            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(imageContent, "captures", $"{cameraName}-{capturedAtInstant:yyyyMMddHHmmss}.jpg");
        }

        return form;
    }
}
