using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GateApp.Models;
using Microsoft.Extensions.Configuration;
using Polly;
using RestSharp;
using Serilog;

namespace GateApp.Services;

public sealed class ApiService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger _logger;
    private readonly Dictionary<string, ApiClientContext> _apiClients;
    private readonly string _gateDirection;
    private bool _disposed;

    public ApiService(IConfiguration configuration, ILogger logger)
    {
        _logger = logger;
        _apiClients = new Dictionary<string, ApiClientContext>(StringComparer.OrdinalIgnoreCase);
        _gateDirection = configuration["Gate:Type"] ?? "IN";

        var apisSection = configuration.GetSection("Apis");
        var apiChildren = apisSection.GetChildren().ToList();

        if (apiChildren.Count > 0)
        {
            foreach (var apiSection in apiChildren)
            {
                var name = apiSection.Key;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var settings = apiSection.Get<ApiSettings>() ?? new ApiSettings();
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

            var response = await ExecuteAuthorizedWithRetryAsync(
                    client,
                    () =>
                    {
                        var payload = JsonSerializer.Serialize(request, JsonOptions);
                        var restRequest = new RestRequest(endpointUri, Method.Post);
                        restRequest.AddStringBody(payload, DataFormat.Json);
                        return restRequest;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                var message = response.ErrorMessage ?? response.ErrorException?.Message ?? "Request was not completed.";
                _logger.Warning("Validate request failed for {ApiName}: {Message}", client.Name, message);
                return new ValidateResponse
                {
                    Success = false,
                    Message = message
                };
            }

            if (!response.IsSuccessful)
            {
                var error = GetResponseErrorMessage(response);
                _logger.Warning("Validate failed with status {Status}: {Message}", response.StatusCode, error);
                return new ValidateResponse
                {
                    Success = false,
                    Message = error
                };
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                _logger.Warning("Validate response from {ApiName} was empty.", client.Name);
                return new ValidateResponse
                {
                    Success = false,
                    Message = "Empty response received from server."
                };
            }

            try
            {
                var payload = JsonSerializer.Deserialize<ValidateResponse>(response.Content, JsonOptions);
                return payload ?? new ValidateResponse
                {
                    Success = false,
                    Message = "Empty response received from server."
                };
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse validate response for {ApiName}.", client.Name);
                return new ValidateResponse
                {
                    Success = false,
                    Message = "Invalid response format received from server."
                };
            }
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

            if (string.IsNullOrWhiteSpace(client.Settings.CaptureEndpoint))
            {
                throw new InvalidOperationException($"Capture endpoint is not configured for API '{client.Name}'.");
            }

            var captureEndpoint = ResolveEndpoint(client, client.Settings.CaptureEndpoint);

            var response = await ExecuteAuthorizedWithRetryAsync(
                    client,
                    () =>
                    {
                        var payload = BuildCapturePayload(
                            ticketId,
                            gateId,
                            capturedAt,
                            capturedAtInstant,
                            snapshots,
                            additionalPayload);

                        var request = new RestRequest(captureEndpoint, Method.Post);
                        var body = JsonSerializer.Serialize(payload, JsonOptions);
                        request.AddStringBody(body, DataFormat.Json);
                        return request;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                var message = response.ErrorMessage ?? response.ErrorException?.Message ?? "Request was not completed.";
                _logger.Warning("Capture upload failed for {ApiName}: {Message}", client.Name, message);
                return false;
            }

            if (!response.IsSuccessful)
            {
                var error = GetResponseErrorMessage(response);
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

    private static IDictionary<string, object?> BuildCapturePayload(
        string ticketId,
        string gateId,
        string capturedAt,
        DateTime capturedAtInstant,
        IDictionary<string, byte[]> snapshots,
        IDictionary<string, string>? additionalPayload)
    {
        var parameters = new List<Dictionary<string, string?>>();

        var header = BuildCaptureHeader(ticketId, gateId, capturedAt, additionalPayload);
        if (header.Count > 0)
        {
            parameters.Add(header);
        }

        foreach (var (cameraName, bytes) in snapshots)
        {
            if (bytes is null || bytes.Length == 0)
            {
                continue;
            }

            var detail = BuildCaptureDetailEntry(cameraName, bytes, capturedAtInstant, header, additionalPayload);
            parameters.Add(detail);
        }

        var metadata = BuildCaptureMetadata(ticketId, gateId, capturedAt, additionalPayload);

        return new Dictionary<string, object?>
        {
            ["timestamp"] = capturedAt,
            ["param"] = parameters,
            ["opt"] = metadata
        };
    }

    private static Dictionary<string, string?> BuildCaptureHeader(
        string ticketId,
        string gateId,
        string capturedAt,
        IDictionary<string, string>? additionalPayload)
    {
        var header = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["INS_UPT"] = GetValue(additionalPayload, "INS_UPT") ?? "INS",
            ["FSC"] = GetValue(additionalPayload, "FSC") ?? string.Empty,
            ["RECTYPE"] = GetValue(additionalPayload, "RECTYPE") ?? string.Empty,
            ["DEPOT"] = GetValue(additionalPayload, "DEPOT") ?? string.Empty,
            ["REFF_NO"] = GetValue(additionalPayload, "REFF_NO") ?? ticketId,
            ["TICKET_ID"] = ticketId,
            ["GATE_ID"] = gateId,
            ["CAPTURED_AT"] = capturedAt
        };

        return header;
    }

    private static Dictionary<string, string?> BuildCaptureDetailEntry(
        string cameraName,
        byte[] bytes,
        DateTime capturedAtInstant,
        IDictionary<string, string?> header,
        IDictionary<string, string>? additionalPayload)
    {
        var fileName = $"{cameraName}-{capturedAtInstant:yyyyMMddHHmmss}.jpg";

        header.TryGetValue("FSC", out var fsc);
        header.TryGetValue("RECTYPE", out var rectype);
        header.TryGetValue("DEPOT", out var depot);
        header.TryGetValue("REFF_NO", out var referenceNo);

        var detail = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["INS_UPT"] = GetValue(additionalPayload, "INS_UPT_DETAIL") ?? "DTL",
            ["FSC"] = fsc ?? string.Empty,
            ["RECTYPE"] = rectype ?? string.Empty,
            ["DEPOT"] = depot ?? string.Empty,
            ["REFF_NO"] = referenceNo ?? string.Empty,
            ["FILE_NAME"] = fileName,
            ["FILE"] = Convert.ToBase64String(bytes)
        };

        return detail;
    }

    private static string BuildCaptureMetadata(
        string ticketId,
        string gateId,
        string capturedAt,
        IDictionary<string, string>? additionalPayload)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ticketId"] = ticketId,
            ["gateId"] = gateId,
            ["capturedAt"] = capturedAt
        };

        if (additionalPayload is not null)
        {
            foreach (var kvp in additionalPayload)
            {
                if (!metadata.ContainsKey(kvp.Key))
                {
                    metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        return metadata.Count > 0
            ? JsonSerializer.Serialize(metadata, JsonOptions)
            : string.Empty;
    }

    private static string? GetValue(IDictionary<string, string>? source, string key)
    {
        if (source is null)
        {
            return null;
        }

        foreach (var kvp in source)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
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
        if (string.Equals(_gateDirection, "OUT", StringComparison.OrdinalIgnoreCase))
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

        var baseUrl = client.RestClient.Options.BaseUrl;
        if (baseUrl is null)
        {
            throw new InvalidOperationException("API BaseUrl must be configured when using relative endpoints.");
        }

        return new Uri(baseUrl, endpoint);
    }

    private ApiClientContext CreateClientContext(string name, ApiSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException($"API BaseUrl is not configured for '{name}'.");
        }

        var baseUri = new Uri(settings.BaseUrl);
        var maxTimeout = settings.Timeout <= TimeSpan.Zero
            ? -1
            : (int)Math.Min(int.MaxValue, settings.Timeout.TotalMilliseconds);

        var options = new RestClientOptions(baseUri)
        {
            ThrowOnAnyError = false,
            MaxTimeout = maxTimeout
        };

        var restClient = new RestClient(options);
        var retryPolicy = CreateRetryPolicy(name, settings);

        var context = new ApiClientContext(name, settings, restClient, retryPolicy);

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            context.SetToken(settings.ApiKey, null);
        }

        return context;
    }

    private AsyncPolicy<RestResponse> CreateRetryPolicy(string name, ApiSettings settings)
    {
        var retryCount = Math.Max(0, settings.RetryCount);
        if (retryCount == 0)
        {
            return Policy.NoOpAsync<RestResponse>();
        }

        var baseDelay = settings.RetryBackoffSeconds <= 0 ? 1 : settings.RetryBackoffSeconds;

        return Policy
            .Handle<TimeoutException>()
            .Or<TaskCanceledException>()
            .OrResult<RestResponse>(response =>
                response.ResponseStatus == ResponseStatus.TimedOut ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                (response.ResponseStatus == ResponseStatus.Error &&
                 response.ErrorException is TimeoutException or TaskCanceledException))
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(baseDelay * Math.Pow(2, attempt - 1)),
                (outcome, span, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                    {
                        _logger.Warning(outcome.Exception, "Request timed out on attempt {Attempt} for {ApiName}. Retrying in {Delay}.", attempt, name, span);
                    }
                    else
                    {
                        _logger.Warning("Request timed out on attempt {Attempt} for {ApiName}. Retrying in {Delay}.", attempt, name, span);
                    }
                });
    }

    private Task<RestResponse> ExecuteAuthorizedWithRetryAsync(ApiClientContext client, Func<RestRequest> requestFactory, CancellationToken cancellationToken)
    {
        return client.RetryPolicy.ExecuteAsync(ct => SendAuthorizedAsync(client, requestFactory, ct), cancellationToken);
    }

    private Task<RestResponse> ExecuteWithoutAuthorizationWithRetryAsync(ApiClientContext client, Func<RestRequest> requestFactory, CancellationToken cancellationToken)
    {
        return client.RetryPolicy.ExecuteAsync(ct => client.RestClient.ExecuteAsync(requestFactory(), ct), cancellationToken);
    }

    private async Task<RestResponse> SendAuthorizedAsync(ApiClientContext client, Func<RestRequest> requestFactory, CancellationToken cancellationToken)
    {
        await EnsureAuthorizationAsync(client, cancellationToken).ConfigureAwait(false);

        var response = await ExecuteRequestAsync(client, requestFactory, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !client.CanRefreshToken)
        {
            return response;
        }

        _logger.Warning("Authorization failed for {ApiName}. Refreshing token and retrying once.", client.Name);
        client.ClearToken();

        await EnsureAuthorizationAsync(client, cancellationToken).ConfigureAwait(false);
        return await ExecuteRequestAsync(client, requestFactory, cancellationToken).ConfigureAwait(false);
    }

    private Task<RestResponse> ExecuteRequestAsync(ApiClientContext client, Func<RestRequest> requestFactory, CancellationToken cancellationToken)
    {
        var request = requestFactory();
        client.ApplyAuthorization(request);
        return client.RestClient.ExecuteAsync(request, cancellationToken);
    }

    private async Task EnsureAuthorizationAsync(ApiClientContext client, CancellationToken cancellationToken)
    {
        if (client.HasValidToken)
        {
            return;
        }

        if (client.CanRefreshToken)
        {
            await client.TokenSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (client.HasValidToken)
                {
                    return;
                }

                var token = await AuthenticateAsync(client, cancellationToken).ConfigureAwait(false);
                client.SetToken(token.Token, token.ExpiresAt);
                var expiryMessage = token.ExpiresAt?.ToString("O") ?? "unknown";
                _logger.Information("Obtained bearer token for {ApiName}. Expires at {Expiry}.", client.Name, expiryMessage);
            }
            finally
            {
                client.TokenSemaphore.Release();
            }

            return;
        }

        if (client.HasLoginConfiguration && !client.CanRefreshToken)
        {
            throw new InvalidOperationException($"Login credentials are not fully configured for API '{client.Name}'.");
        }

        if (!string.IsNullOrWhiteSpace(client.Settings.ApiKey) && !client.HasValidToken)
        {
            client.SetToken(client.Settings.ApiKey, null);
        }
    }

    private async Task<AuthToken> AuthenticateAsync(ApiClientContext client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(client.Settings.LoginEndpoint))
        {
            throw new InvalidOperationException($"Login endpoint is not configured for API '{client.Name}'.");
        }

        var endpointUri = ResolveEndpoint(client, client.Settings.LoginEndpoint);
        var payload = new Dictionary<string, object?>
        {
            ["param"] = new[]
            {
                new Dictionary<string, string?>
                {
                    ["EMAIL"] = client.Settings.LoginEmail,
                    ["PASSWORD"] = client.Settings.LoginPassword
                }
            }
        };

        _logger.Information("Requesting bearer token for {ApiName}.", client.Name);

        var response = await ExecuteWithoutAuthorizationWithRetryAsync(
                client,
                () =>
                {
                    var request = new RestRequest(endpointUri, Method.Post);
                    var body = JsonSerializer.Serialize(payload);
                    request.AddStringBody(body, DataFormat.Json);
                    return request;
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (response.ResponseStatus != ResponseStatus.Completed)
        {
            var message = response.ErrorMessage ?? response.ErrorException?.Message ?? "Request was not completed.";
            throw new InvalidOperationException($"Login request failed for API '{client.Name}': {message}");
        }

        if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
        {
            var message = GetResponseErrorMessage(response);
            throw new InvalidOperationException($"Login request failed for API '{client.Name}' with status {response.StatusCode}: {message}");
        }

        using var document = JsonDocument.Parse(response.Content);

        if (!document.RootElement.TryGetProperty("status", out var statusElement) || statusElement.GetString() != "1")
        {
            var message = document.RootElement.TryGetProperty("msg", out var msgElement)
                ? msgElement.GetString() ?? "Unknown error"
                : "Unknown error";
            throw new InvalidOperationException($"Login request was not successful for API '{client.Name}': {message}");
        }

        string? token = null;
        if (document.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in dataElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("TOKEN", out var tokenElement))
                {
                    token = tokenElement.GetString();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException($"Login response did not contain a bearer token for API '{client.Name}'.");
        }

        var expiresAt = TryDecodeJwtExpiry(token);
        return new AuthToken(token, expiresAt);
    }

    private static DateTimeOffset? TryDecodeJwtExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.TryGetProperty("exp", out var expElement) && expElement.TryGetInt64(out var expSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string GetResponseErrorMessage(RestResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Content))
        {
            return response.Content;
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            return response.ErrorMessage;
        }

        if (response.ErrorException is not null)
        {
            return response.ErrorException.Message;
        }

        return response.StatusDescription ?? "Unknown error";
    }

    private readonly record struct AuthToken(string Token, DateTimeOffset? ExpiresAt);

    private sealed class ApiClientContext : IDisposable
    {
        private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(1);
        private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
        private string? _token;
        private DateTimeOffset? _tokenExpiry;

        public ApiClientContext(string name, ApiSettings settings, RestClient restClient, AsyncPolicy<RestResponse> retryPolicy)
        {
            Name = name;
            Settings = settings;
            RestClient = restClient;
            RetryPolicy = retryPolicy;
        }

        public string Name { get; }
        public ApiSettings Settings { get; }
        public RestClient RestClient { get; }
        public AsyncPolicy<RestResponse> RetryPolicy { get; }
        public bool HasLoginConfiguration => !string.IsNullOrWhiteSpace(Settings.LoginEndpoint);
        public bool CanRefreshToken => HasLoginConfiguration && !string.IsNullOrWhiteSpace(Settings.LoginEmail) && !string.IsNullOrWhiteSpace(Settings.LoginPassword);
        public bool HasValidToken => !string.IsNullOrWhiteSpace(_token) && (_tokenExpiry is null || _tokenExpiry > DateTimeOffset.UtcNow);
        public SemaphoreSlim TokenSemaphore => _tokenSemaphore;

        public void SetToken(string token, DateTimeOffset? expiresAt)
        {
            _token = token;
            if (expiresAt is null)
            {
                _tokenExpiry = null;
            }
            else
            {
                var refreshTime = expiresAt.Value - TokenRefreshBuffer;
                _tokenExpiry = refreshTime > DateTimeOffset.UtcNow ? refreshTime : expiresAt.Value;
            }
        }

        public void ClearToken()
        {
            _token = null;
            _tokenExpiry = null;
        }

        public void ApplyAuthorization(RestRequest request)
        {
            if (string.IsNullOrWhiteSpace(_token))
            {
                return;
            }

            request.AddOrUpdateHeader("Authorization", $"Bearer {_token}");
        }

        public void Dispose()
        {
            RestClient.Dispose();
            _tokenSemaphore.Dispose();
        }
    }
}
