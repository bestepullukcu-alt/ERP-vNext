using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;

namespace Diten.Web.Models.Traceability;

/// <summary>
/// The door from the MOD-0174 MVC proxies to Gateway 5000 <c>/api/trace/**</c> (INT-TRACE, pack §15). The same rules as
/// the FU01 <see cref="InventoryGatewayClient"/> — cookie bearer + <c>X-Tenant-Id</c> per REQUEST, never a shared default
/// header — and the W-0173-02 company rule: InventoryService scopes <c>/api/trace</c> with the ledger's scope middleware,
/// so a token without <c>legal_entity_id</c> sends the user's selected company as <c>X-Legal-Entity-Id</c>, and with no
/// company selected no call is made. (The FU01 client adds that header to <c>/api/inventory</c> and
/// <c>/api/locations</c> only; it is reused as is for those — this one exists for <c>/api/trace</c>.)
/// The browser never calls the Gateway, 5066 or <c>/api/trace</c>.
/// </summary>
public sealed class TraceGatewayClient
{
    public const string BasePath = "/api/trace";

    private readonly HttpClient _http;
    private readonly string _gatewayUrl;
    private readonly string? _accessToken;
    private readonly string _tenantId;
    private readonly Guid? _company;
    private readonly bool _companyRequired;

    public TraceGatewayClient(HttpClient http, string gatewayUrl, string? accessToken, string tenantId, Guid? company, bool companyRequired)
    {
        _http = http;
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _accessToken = accessToken;
        _tenantId = tenantId;
        _company = company;
        _companyRequired = companyRequired;
    }

    /// <summary>The client for this request, or null when the principal carries no tenant.</summary>
    public static TraceGatewayClient? For(HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user)
    {
        var tenantId = user.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(tenantId)) return null;

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(request);
        var tokenHasCompany = CompanySelection.TokenLegalEntity(user) is not null;
        var company = tokenHasCompany ? null : CompanySelection.SelectedCompany(request, user);
        return new TraceGatewayClient(http, gatewayUrl, token, tenantId, company, companyRequired: !tokenHasCompany);
    }

    public Task<GatewayReply> GetAsync(string pathAndQuery, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, pathAndQuery, null, null, null, cancellationToken);

    public Task<GatewayReply> PostAsync(string path, object body, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, idempotencyKey, ifMatch, cancellationToken);

    public Task<GatewayReply> PatchAsync(string path, object body, string ifMatch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Patch, path, body, null, ifMatch, cancellationToken);

    private async Task<GatewayReply> SendAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken)
    {
        if (!pathAndQuery.StartsWith(BasePath, StringComparison.Ordinal))
            throw new ArgumentException($"{nameof(TraceGatewayClient)} calls {BasePath} only.", nameof(pathAndQuery));
        if (_companyRequired && _company is null)
            return new GatewayReply(403, JsonSerializer.Serialize(new { error = new { code = InventoryGatewayClient.CompanyNotSelected, message = "Select a company." } }), null);

        using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
        if (_company is not null)
            request.Headers.Add(CompanySelection.LegalEntityHeader, _company.Value.ToString("D"));
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: InventoryGatewayClient.Json);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Headers.TryGetValues("X-Correlation-Id", out var correlation);
            return new GatewayReply((int)response.StatusCode, text, correlation?.FirstOrDefault()) { ETag = response.Headers.ETag?.ToString() };
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
