using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// Tenant-admin self-service Security Settings (tenant shell). The tenant manages its OWN login/security
// settings — there is NO tenant id in the path; the gateway resolves the tenant from the forwarded
// X-Tenant-Id claim. Reads + the single save happen on the Index page via same-origin AJAX to these
// proxy actions, which forward the server-side HttpOnly bearer token and the tenant claim to the gateway.
// UX-only; backend [HasPermission("platform.tenant-security.manage")] is authoritative.
[Authorize]
[Route("TenantSecurity")]
public sealed class TenantSecuritySettingsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public TenantSecuritySettingsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Governance/TenantSecuritySettings/Index.cshtml");

    [HttpGet("api/login-settings")]
    public Task<IActionResult> LoginSettingsProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenant-security/login-settings");
    }

    [HttpPut("api/login-settings")]
    public async Task<IActionResult> UpdateLoginSettingsProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/tenant-security/login-settings", body);
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        AddAuthHeaders();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        if (ProxyAuthFailure.IsAuthFailure(response.StatusCode))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode((int)response.StatusCode, ProxyAuthFailure.PlatformLoginPayload());
        }

        var content = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
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
