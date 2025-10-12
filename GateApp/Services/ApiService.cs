using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly ILogger _logger;
    private readonly Dictionary<string, ApiClientContext> _apiClients;
    private readonly string _gateDirection;
    private bool _disposed;

    public ApiService(IConfiguration configuration, ILogger logger)
    {
        _logger = logger;
        _apiClients = new Dictionary<string, ApiClientContext>(StringComparer.OrdinalIgnoreCase);
        _gateDirection = configuration["Gate:Type"];
        if (string.IsNullOrWhiteSpace(_gateDirection))
        {
            _gateDirection = "IN";
        }

        var apisSection = configuration.GetSection("Apis");
        var apiChildren = apisSection.GetChildren().ToList();

        if (apiChildren.Count > 0)
        {
            foreach (var apiSection in apiChildren)
            {
                var name = apiSection.Key;
                var settings = apiSection.Get<ApiSettings>() ?? new ApiSettings();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                _apiClients[name] = CreateClientContext(name, settings);
            }
        }
        else
        {
            var legacySettings = configuration.GetSection("Api").Get<ApiSettings>() ?? new ApiSettings();
            _apiClients["Default"] = CreateClientContext("Default", legacySettings);
        }

        if (_apiClients.Count == 0)
        {
            throw new InvalidOperationException("No API clients are configured.");
        }
    }

    public async Task<ValidateResponse?> ValidateAsync(ValidateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var client = GetApiClientForQrCode(request.QrCode);
            _logger.Information("Sending validate request for QR {QrCode} using {ApiName}", request.QrCode, client.Name);
            var endpoint = GetGateEndpoint(client);
            var endpointUri = ResolveEndpoint(client, endpoint);
            using var response = await client.RetryPolicy
                .ExecuteAsync(ct => client.HttpClient.PostAsJsonAsync(endpointUri, request, ct), cancellationToken)
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

    public async Task<bool> SendCaptureAsync(
        string qrCode,
        string ticketId,
        string gateId,
        IDictionary<string, byte[]> snapshots,
        IDictionary<string, string>? additionalPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = GetApiClientForQrCode(qrCode);
            var capturedAtInstant = DateTime.UtcNow;
            var capturedAt = capturedAtInstant.ToString("O");
            using var response = await client.RetryPolicy
                .ExecuteAsync(async ct =>
                {
                    if (string.IsNullOrWhiteSpace(client.Settings.CaptureEndpoint))
                    {
                        throw new InvalidOperationException($"Capture endpoint is not configured for API '{client.Name}'.");
                    }

                    var endpointUri = ResolveEndpoint(client, client.Settings.CaptureEndpoint);
                    var content = BuildCaptureForm(ticketId, gateId, capturedAt, capturedAtInstant, snapshots, additionalPayload);
                    try
                    {
                        return await client.HttpClient.PostAsync(endpointUri, content, ct).ConfigureAwait(false);
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

            _logger.Information("Capture uploaded for ticket {TicketId} using {ApiName}", ticketId, client.Name);
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

        foreach (var client in _apiClients.Values)
        {
            client.Dispose();
        }

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

    private ApiClientContext GetApiClientForQrCode(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            if (_apiClients.Count == 1)
            {
                return _apiClients.Values.First();
            }

            throw new InvalidOperationException("QR code is required to determine the API client.");
        }

        var key = DetermineApiKey(qrCode);
        if (key is null)
        {
            if (_apiClients.Count == 1)
            {
                return _apiClients.Values.First();
            }

            throw new InvalidOperationException($"No API mapping found for QR code '{qrCode}'.");
        }

        if (_apiClients.TryGetValue(key, out var client))
        {
            return client;
        }

        throw new InvalidOperationException($"API configuration '{key}' is not defined.");
    }

    private static string? DetermineApiKey(string qrCode)
    {
        if (qrCode.Contains("/DWI/", StringComparison.OrdinalIgnoreCase))
        {
            return "Dss";
        }

        if (qrCode.Contains("/DW/", StringComparison.OrdinalIgnoreCase))
        {
            return "DwiMados";
        }

        if (qrCode.Contains("/RC/", StringComparison.OrdinalIgnoreCase))
        {
            return "EirMados";
        }

        return null;
    }

    private string GetGateEndpoint(ApiClientContext client)
    {
        var direction = _gateDirection;
        if (string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(client.Settings.GateOutEndpoint))
            {
                return client.Settings.GateOutEndpoint;
            }

            if (!string.IsNullOrWhiteSpace(client.Settings.GateInEndpoint))
            {
                _logger.Warning("Gate OUT endpoint not configured for {ApiName}. Falling back to Gate IN endpoint.", client.Name);
                return client.Settings.GateInEndpoint;
            }

            throw new InvalidOperationException($"No Gate OUT endpoint configured for API '{client.Name}'.");
        }

        if (!string.IsNullOrWhiteSpace(client.Settings.GateInEndpoint))
        {
            return client.Settings.GateInEndpoint;
        }

        if (!string.IsNullOrWhiteSpace(client.Settings.GateOutEndpoint))
        {
            _logger.Warning("Gate IN endpoint not configured for {ApiName}. Falling back to Gate OUT endpoint.", client.Name);
            return client.Settings.GateOutEndpoint;
        }

        throw new InvalidOperationException($"No Gate IN endpoint configured for API '{client.Name}'.");
    }

    private static Uri ResolveEndpoint(ApiClientContext client, string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (client.HttpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("API BaseUrl must be configured when using relative endpoints.");
        }

        return new Uri(client.HttpClient.BaseAddress, endpoint);
    }

    private ApiClientContext CreateClientContext(string name, ApiSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException($"API BaseUrl is not configured for '{name}'.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = settings.Timeout
        };

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        var retryCount = Math.Max(0, settings.RetryCount);
        var baseDelay = settings.RetryBackoffSeconds <= 0 ? 1 : settings.RetryBackoffSeconds;
        var retryPolicy = Policy<HttpResponseMessage>
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
                        _logger.Warning(outcome.Exception, "HTTP request failed on attempt {Attempt} for {ApiName}. Retrying in {Delay}.", attempt, name, span);
                    }
                    else
                    {
                        _logger.Warning("HTTP request returned status {Status} on attempt {Attempt} for {ApiName}. Retrying in {Delay}.", outcome.Result.StatusCode, attempt, name, span);
                    }
                });

        return new ApiClientContext(name, settings, httpClient, retryPolicy);
    }

    private sealed class ApiClientContext : IDisposable
    {
        public ApiClientContext(string name, ApiSettings settings, HttpClient httpClient, AsyncRetryPolicy<HttpResponseMessage> retryPolicy)
        {
            Name = name;
            Settings = settings;
            HttpClient = httpClient;
            RetryPolicy = retryPolicy;
        }

        public string Name { get; }
        public ApiSettings Settings { get; }
        public HttpClient HttpClient { get; }
        public AsyncRetryPolicy<HttpResponseMessage> RetryPolicy { get; }

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }
}
