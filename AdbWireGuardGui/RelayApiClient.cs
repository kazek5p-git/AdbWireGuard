using System.Net.Http.Json;
using System.Text.Json;

namespace AdbWireGuardGui;

internal sealed class RelayApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RelayHealthResponse> GetHealthAsync(string serverUrl, CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        var response = await client.GetAsync("healthz", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RelayHealthResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer relay zwrocil pusty health check.");
    }

    public async Task<RelayCreateSessionResponse> CreateSessionAsync(
        string serverUrl,
        string hostToken,
        string deviceName,
        int requestedTtlMinutes,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.PostAsJsonAsync(
            "api/v1/relay/sessions",
            new RelayCreateSessionRequest(deviceName, requestedTtlMinutes),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<RelayCreateSessionResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer relay zwrocil pusty wynik tworzenia sesji.");
    }

    public async Task<RelayClaimSessionResponse> ClaimSessionAsync(
        string serverUrl,
        string pairCode,
        string clientName,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        var response = await client.PostAsJsonAsync(
            "api/v1/relay/claim",
            new RelayClaimSessionRequest(pairCode, clientName),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<RelayClaimSessionResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer relay zwrocil pusty wynik claim sesji.");
    }

    public async Task<RelaySessionStatusResponse> GetSessionStatusAsync(
        string serverUrl,
        Guid sessionId,
        string hostToken,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.GetAsync($"api/v1/relay/sessions/{sessionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<RelaySessionStatusResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer relay zwrocil pusty status sesji.");
    }

    public async Task CloseSessionAsync(
        string serverUrl,
        Guid sessionId,
        string hostToken,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.DeleteAsync($"api/v1/relay/sessions/{sessionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    private static HttpClient CreateClient(string serverUrl)
    {
        var normalized = NormalizeServerUrl(serverUrl);
        return new HttpClient
        {
            BaseAddress = new Uri(normalized, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    internal static string NormalizeServerUrl(string serverUrl)
    {
        var value = serverUrl.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return value.TrimEnd('/') + "/";
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<RelayErrorResponse>(JsonOptions, cancellationToken);
            message = error?.Error;
        }
        catch
        {
            // Fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }

        throw new InvalidOperationException(message.Trim());
    }
}

internal sealed record RelayHealthResponse(bool Ok, string Service, int HostTokensConfigured);

internal sealed record RelayCreateSessionRequest(string DeviceName, int RequestedTtlMinutes);

internal sealed record RelayCreateSessionResponse(
    Guid SessionId,
    string PairCode,
    string HostConnectToken,
    string HostResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int TtlSeconds,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

internal sealed record RelayClaimSessionRequest(string PairCode, string ClientName);

internal sealed record RelayClaimSessionResponse(
    Guid SessionId,
    string ClientConnectToken,
    string ClientResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

internal sealed record RelaySessionStatusResponse(
    Guid SessionId,
    string DeviceName,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    bool HostConnected,
    bool ClientConnected,
    bool RelayStarted,
    int ClaimAttempts,
    DateTimeOffset? HostLastSeenUtc,
    DateTimeOffset? ClientLastSeenUtc,
    DateTimeOffset? HostReconnectDeadlineUtc,
    DateTimeOffset? ClientReconnectDeadlineUtc);

internal sealed record RelayErrorResponse(string Error);
