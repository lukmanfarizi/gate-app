using System;
using System.Collections.Generic;
using System.Globalization;
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
                        var payload = IsDssClient(client)
                            ? BuildDssValidatePayload(client, request)
                            : IsMadosClient(client)
                                ? BuildMadosValidatePayload(client, request)
                                : (object)request;

                        var restRequest = new RestRequest(endpointUri, Method.Post);
                        var body = JsonSerializer.Serialize(payload, JsonOptions);
                        restRequest.AddStringBody(body, DataFormat.Json);
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
                if (IsDssClient(client))
                {
                    return ParseDssValidateResponse(response.Content);
                }

                if (IsMadosClient(client))
                {
                    return ParseMadosValidateResponse(response.Content);
                }

                var payload = JsonSerializer.Deserialize<ValidateResponse>(response.Content, JsonOptions);
                return payload ?? new ValidateResponse
                {
                    Success = false,
                    Message = "Empty response received from server."
                };
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
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

    private static bool IsDssClient(ApiClientContext client)
    {
        return string.Equals(client.Name, "Dss", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMadosClient(ApiClientContext client)
    {
        return client.Name.EndsWith("Mados", StringComparison.OrdinalIgnoreCase);
    }

    private object BuildDssValidatePayload(ApiClientContext client, ValidateRequest request)
    {
        var token = client.GetToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Authorization token is not available for DSS API requests.");
        }

        var depotId = TryExtractDepotId(request.QrCode);
        if (string.IsNullOrWhiteSpace(depotId))
        {
            depotId = string.IsNullOrWhiteSpace(client.Settings.DepotId) ? null : client.Settings.DepotId;
        }

        if (string.IsNullOrWhiteSpace(depotId))
        {
            throw new InvalidOperationException("Depot ID could not be determined for DSS API requests.");
        }

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["QRCODE"] = request.QrCode,
            ["DEPOTID"] = depotId,
            ["TOKEN"] = token
        };

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["param"] = new[] { parameters }
        };
    }

    private object BuildMadosValidatePayload(ApiClientContext client, ValidateRequest request)
    {
        var depotId = TryExtractDepotId(request.QrCode);
        if (string.IsNullOrWhiteSpace(depotId))
        {
            depotId = string.IsNullOrWhiteSpace(client.Settings.DepotId) ? null : client.Settings.DepotId;
        }

        if (string.IsNullOrWhiteSpace(depotId))
        {
            throw new InvalidOperationException("Depot ID could not be determined for MADOS API requests.");
        }

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["QRCODE"] = request.QrCode,
            ["DEPOTID"] = depotId
        };

        if (string.Equals(_gateDirection, "OUT", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(request.GateId))
        {
            parameters["GATE_PASS"] = request.GateId;
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["param"] = new[] { parameters }
        };
    }

    private static string? TryExtractDepotId(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            return null;
        }

        var separatorIndex = qrCode.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var depotId = qrCode[..separatorIndex].Trim();
        return depotId.Length == 0 ? null : depotId;
    }

    private ValidateResponse ParseDssValidateResponse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Unexpected DSS response format.");
        }

        var statusText = TryGetStringCaseInsensitive(root, "status");
        var success = IsSuccessfulStatus(statusText);
        var message = TryGetStringCaseInsensitive(root, "msg") ?? string.Empty;

        Dictionary<string, string>? additionalData = null;
        string? ticketId = null;
        string? plateNumber = null;
        string? driverName = null;

        if (TryGetPropertyCaseInsensitive(root, "data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in dataElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                additionalData ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var property in entry.EnumerateObject())
                {
                    var value = ConvertJsonValueToString(property.Value);
                    if (value is null)
                    {
                        continue;
                    }

                    additionalData[property.Name] = value;

                    var upperName = property.Name.ToUpperInvariant();
                    switch (upperName)
                    {
                        case "TICKET_ID":
                        case "TICKETID":
                        case "REFF_NO":
                        case "REFFNO":
                            ticketId ??= value;
                            break;
                        case "NOPOL":
                        case "PLATE_NO":
                        case "PLATENO":
                        case "PLATE":
                            plateNumber ??= value;
                            break;
                        case "DRIVER":
                        case "DRIVER_NAME":
                            driverName ??= value;
                            break;
                        case "MESSAGE":
                            if (string.IsNullOrWhiteSpace(message))
                            {
                                message = value;
                            }

                            break;
                    }
                }

                break;
            }
        }

        if (!success && string.IsNullOrWhiteSpace(message) && additionalData is not null &&
            additionalData.TryGetValue("MESSAGE", out var detailMessage) && !string.IsNullOrWhiteSpace(detailMessage))
        {
            message = detailMessage;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = success ? "Validation successful." : "Validation request was not successful.";
        }

        if (additionalData is not null && additionalData.Count == 0)
        {
            additionalData = null;
        }

        return new ValidateResponse
        {
            Success = success,
            Message = message,
            TicketId = ticketId,
            PlateNumber = plateNumber,
            DriverName = driverName,
            AdditionalData = additionalData
        };
    }

    private ValidateResponse ParseMadosValidateResponse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Unexpected MADOS response format.");
        }

        var statusText = TryGetStringCaseInsensitive(root, "status");
        var success = IsSuccessfulStatus(statusText);
        var message = TryGetStringCaseInsensitive(root, "msg") ?? string.Empty;

        Dictionary<string, string>? additionalData = null;
        string? ticketId = null;
        string? plateNumber = null;
        string? driverName = null;

        if (TryGetPropertyCaseInsensitive(root, "data", out var dataElement) &&
            dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in dataElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                additionalData ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var property in entry.EnumerateObject())
                {
                    var value = ConvertJsonValueToString(property.Value);
                    if (value is null)
                    {
                        continue;
                    }

                    additionalData[property.Name] = value;

                    var upperName = property.Name.ToUpperInvariant();
                    switch (upperName)
                    {
                        case "DWI":
                        case "EIR":
                        case "GATEPASS":
                        case "GATE_PASS":
                        case "PASS_NUMBER":
                        case "TICKET_ID":
                        case "TICKETID":
                        case "QRCODE":
                            ticketId ??= value;
                            break;
                        case "NOPOL":
                        case "PLATE_NO":
                        case "PLATENO":
                        case "NOPOLIS":
                        case "PLATE":
                            plateNumber ??= value;
                            break;
                        case "DRIVER":
                        case "DRIVER_NAME":
                        case "DRIVERNAME":
                            driverName ??= value;
                            break;
                        case "GATESTATUS":
                        case "GATE_STATUS":
                        case "REASON":
                        case "REMARK":
                            if (string.IsNullOrWhiteSpace(message))
                            {
                                message = value;
                            }

                            break;
                    }
                }

                break;
            }
        }

        if (!success && string.IsNullOrWhiteSpace(message) &&
            TryGetPropertyCaseInsensitive(root, "reason", out var reasonElement))
        {
            var reason = ConvertJsonValueToString(reasonElement);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                message = reason;
            }
        }

        if (string.IsNullOrWhiteSpace(message) &&
            additionalData is not null &&
            additionalData.TryGetValue("GATESTATUS", out var gateStatus) &&
            !string.IsNullOrWhiteSpace(gateStatus))
        {
            message = gateStatus;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = success ? "Validation successful." : "Validation request was not successful.";
        }

        if (additionalData is not null && additionalData.Count == 0)
        {
            additionalData = null;
        }

        return new ValidateResponse
        {
            Success = success,
            Message = message,
            TicketId = ticketId,
            PlateNumber = plateNumber,
            DriverName = driverName,
            AdditionalData = additionalData
        };
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? TryGetStringCaseInsensitive(JsonElement element, string propertyName)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out var value)
            ? ConvertJsonValueToString(value)
            : null;
    }

    private static string? ConvertJsonValueToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => element.GetRawText()
        };
    }

    private static bool IsSuccessfulStatus(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            return false;
        }

        statusText = statusText.Trim();

        if (int.TryParse(statusText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
        {
            return statusCode == 1 || statusCode == 200;
        }

        if (bool.TryParse(statusText, out var boolStatus))
        {
            return boolStatus;
        }

        return string.Equals(statusText, "OK", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(statusText, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(statusText, "S", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string?> BuildLoginParameters(ApiSettings settings)
    {
        var credentials = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(settings.LoginEmail))
        {
            credentials["EMAIL"] = settings.LoginEmail;
        }

        if (!string.IsNullOrWhiteSpace(settings.LoginUsername))
        {
            credentials["Username"] = settings.LoginUsername;
        }

        if (!string.IsNullOrWhiteSpace(settings.LoginPassword))
        {
            credentials["PASSWORD"] = settings.LoginPassword;
            credentials["Password"] = settings.LoginPassword;
        }

        if (credentials.Count == 0)
        {
            throw new InvalidOperationException("Login credentials are not configured.");
        }

        return credentials;
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

        if (qrCode.Contains("/RC/", StringComparison.OrdinalIgnoreCase) ||
            qrCode.Contains("/DL/", StringComparison.OrdinalIgnoreCase))
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
            throw new InvalidOperationException($"Login credentials are not fully configured for API '{client.Name}'. Username or email along with a password is required.");
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
                BuildLoginParameters(client.Settings)
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
        var root = document.RootElement;

        if (!TryGetPropertyCaseInsensitive(root, "status", out var statusElement) ||
            !IsSuccessfulStatus(ConvertJsonValueToString(statusElement)))
        {
            var message = TryGetStringCaseInsensitive(root, "msg") ?? "Unknown error";
            throw new InvalidOperationException($"Login request was not successful for API '{client.Name}': {message}");
        }

        string? token = null;
        if (TryGetPropertyCaseInsensitive(root, "data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in dataElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object &&
                    TryGetPropertyCaseInsensitive(entry, "TOKEN", out var tokenElement))
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
        public bool CanRefreshToken => HasLoginConfiguration &&
                                       !string.IsNullOrWhiteSpace(Settings.LoginPassword) &&
                                       (!string.IsNullOrWhiteSpace(Settings.LoginEmail) ||
                                        !string.IsNullOrWhiteSpace(Settings.LoginUsername));
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

        public string? GetToken()
        {
            return _token;
        }

        public void ApplyAuthorization(RestRequest request)
        {
            if (!Settings.UseAuthorizationHeader || string.IsNullOrWhiteSpace(_token))
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
