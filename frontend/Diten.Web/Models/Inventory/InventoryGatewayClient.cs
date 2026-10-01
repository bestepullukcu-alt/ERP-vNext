using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// The one door from the FU01 MVC proxies to Gateway 5000 (pack §8): cookie bearer + <c>X-Tenant-Id</c>, exactly
/// what <c>AddAuthHeaders</c> sends on the sibling controllers — but per REQUEST, never on the shared client's
/// default headers (a concurrent request could otherwise leave with another request's Idempotency-Key).
/// <c>X-Legal-Entity-Id</c> is never sent (parent ASSUMPTION-0173-03); <c>tenantId</c> / <c>legalEntityId</c> never
/// travel in a body (parent §8: 400).
/// </summary>
public sealed class InventoryGatewayClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly string _gatewayUrl;
    private readonly string? _accessToken;
    private readonly string _tenantId;

    public InventoryGatewayClient(HttpClient http, string gatewayUrl, string? accessToken, string tenantId)
    {
        _http = http;
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _accessToken = accessToken;
        _tenantId = tenantId;
    }

    public Task<GatewayReply> GetAsync(string pathAndQuery, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, pathAndQuery, null, null, cancellationToken);

    public Task<GatewayReply> PostAsync(string path, object body, string idempotencyKey, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, idempotencyKey, cancellationToken);

    private async Task<GatewayReply> SendAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Headers.TryGetValues("X-Correlation-Id", out var correlation);
            return new GatewayReply((int)response.StatusCode, text, correlation?.FirstOrDefault());
        }
        catch (HttpRequestException)
        {
            return GatewayReply.Unreachable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GatewayReply.Unreachable;
        }
    }
}

/// <summary>A Gateway answer as text. <see cref="StatusCode"/> 0 = the Gateway itself could not be reached.</summary>
public sealed record GatewayReply(int StatusCode, string Body, string? CorrelationId)
{
    public static readonly GatewayReply Unreachable = new(0, string.Empty, null);

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public T? Read<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(Body)) return null;
        try { return JsonSerializer.Deserialize<T>(Body, InventoryGatewayClient.Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The contract <c>error</c> object, if the body carries one.</summary>
    public InventoryErrorBody? Error => Read<InventoryErrorEnvelope>()?.Error;
}
