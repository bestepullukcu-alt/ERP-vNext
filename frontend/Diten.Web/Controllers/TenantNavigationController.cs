using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// FEAT-TENANT-NAV-PREFS-S2 — tenant-admin self-service Menu (navigation) Settings (tenant shell). The tenant
// customizes its OWN sidebar at module level (reorder / show-hide / rename). No tenant id in the path; the gateway
// resolves the tenant from the forwarded X-Tenant-Id claim + bearer. Thin proxy only (No-ViewModel): the page reads
// the menu + preferences and saves via same-origin AJAX to these actions, which forward the server-side HttpOnly
// bearer to the gateway. UX-only; the backend is authoritative (entitled-only, tenant-scoped).
[Authorize]
[Route("TenantNavigation")]
public sealed class TenantNavigationController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public TenantNavigationController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Governance/TenantNavigationSettings/Index.cshtml");

    [HttpGet("api/menu")]
    public Task<IActionResult> MenuProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/navigation/menu");
    }

    [HttpGet("api/preferences")]
    public Task<IActionResult> GetPreferencesProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/navigation/preferences");
    }

    [HttpPut("api/preferences")]
    public async Task<IActionResult> ReplacePreferencesProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/navigation/preferences", body);
    }

    // Reflect the downstream response VERBATIM (status + body) — including 401. We never clear cookies / force a
    // logout here (consistent with the login-bridge resilience fix): a token the gateway rejects surfaces as a plain
    // 401 the client can act on, and a valid token just works.
    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        AddAuthHeaders();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return new ContentResult
        {
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    // Tenant pattern: forward the bearer token AND the X-Tenant-Id from the user's tenant claim.
    private void AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }

        var tenantId = GetTenantId();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
