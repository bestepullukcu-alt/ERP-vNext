using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// The one door from the FU01 MVC proxies to Gateway 5000 (pack §8): cookie bearer + <c>X-Tenant-Id</c>, exactly
/// what <c>AddAuthHeaders</c> sends on the sibling controllers — but per REQUEST, never on the shared client's
/// default headers (a concurrent request could otherwise leave with another request's Idempotency-Key).
/// <c>tenantId</c> / <c>legalEntityId</c> never travel in a query or a body (parent §8: 400). <c>X-Legal-Entity-Id</c>
/// (W-0173-02) is added to inventory calls ONLY for a token without a <c>legal_entity_id</c> claim, carrying the user's
/// selected company; with no company selected an inventory call is not sent at all (<see cref="CompanyNotSelected"/>).
/// MOD-0173-FU02 (LOC2): <c>/api/locations</c> runs through the SAME scope middleware in InventoryService (FU02 pack
/// §8), so the company rule covers it too; the location owner operations add PATCH, <c>If-Match</c> and the
/// answer's <c>ETag</c>.
/// </summary>
public sealed class InventoryGatewayClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly string _gatewayUrl;
    private readonly string? _accessToken;
    private readonly string _tenantId;
    private readonly Guid? _company;
    private readonly bool _companyRequired;
    private readonly string? _productMasterBridge;

    /// <summary>The synthetic refusal code for an inventory call made without a company (never reaches the Gateway).</summary>
    public const string CompanyNotSelected = "COMPANY_NOT_SELECTED";

    /// <param name="productMasterBridge">WP-0173-PM0: a DEVELOPMENT-only base URL for <c>/api/product-master/…</c>
    /// (<see cref="ProductMasterBridge"/>); null = the Gateway, as before.</param>
    public InventoryGatewayClient(HttpClient http, string gatewayUrl, string? accessToken, string tenantId, Guid? company = null, bool companyRequired = false, string? productMasterBridge = null)
    {
        _company = company;
        _companyRequired = companyRequired;
        _productMasterBridge = productMasterBridge;
        _http = http;
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _accessToken = accessToken;
        _tenantId = tenantId;
    }

    public Task<GatewayReply> GetAsync(string pathAndQuery, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, pathAndQuery, null, null, null, cancellationToken);

    public Task<GatewayReply> PostAsync(string path, object body, string idempotencyKey, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, idempotencyKey, null, cancellationToken);

    /// <summary>A POST guarded by optimistic concurrency (LOCATION v1.1 status change): <c>If-Match</c>, no key.</summary>
    public Task<GatewayReply> PostIfMatchAsync(string path, object body, string ifMatch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, null, ifMatch, cancellationToken);

    public Task<GatewayReply> PatchAsync(string path, object body, string ifMatch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Patch, path, body, null, ifMatch, cancellationToken);

    /// <summary>The paths InventoryService scopes by company (W-0173-02): the ledger and the location master.</summary>
    private static bool IsCompanyScoped(string pathAndQuery) =>
        pathAndQuery.StartsWith("/api/inventory", StringComparison.Ordinal)
        || pathAndQuery.StartsWith("/api/locations", StringComparison.Ordinal);

    private async Task<GatewayReply> SendAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken)
    {
        var isInventory = IsCompanyScoped(pathAndQuery);
        if (isInventory && _companyRequired && _company is null)
        {
            // W-0173-02: no company, no inventory call — not even one the service would refuse.
            return new GatewayReply(403, JsonSerializer.Serialize(new { error = new { code = CompanyNotSelected, message = "Select a company." } }), null);
        }

        // The headers below are the same on both targets; only the address differs (and the bridge's has no prefix).
        using var request = new HttpRequestMessage(method, ProductMasterBridge.Target(_gatewayUrl, _productMasterBridge, pathAndQuery));
        if (isInventory && _company is not null)
            request.Headers.Add(CompanySelection.LegalEntityHeader, _company.Value.ToString("D"));
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        // W/"<version>" is a weak entity tag; it goes in verbatim, unparsed.
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Headers.TryGetValues("X-Correlation-Id", out var correlation);
            return new GatewayReply((int)response.StatusCode, text, correlation?.FirstOrDefault())
            {
                ETag = response.Headers.ETag?.ToString()
            };
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

    /// <summary>The answer's <c>ETag</c> (LOCATION owner reads/writes: <c>W/"&lt;version&gt;"</c>), if any.</summary>
    public string? ETag { get; init; }

    public T? Read<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(Body)) return null;
        try { return JsonSerializer.Deserialize<T>(Body, InventoryGatewayClient.Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The contract <c>error</c> object, if the body carries one.</summary>
    public InventoryErrorBody? Error => Read<InventoryErrorEnvelope>()?.Error;
}
