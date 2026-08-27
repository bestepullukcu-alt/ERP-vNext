using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// FE-C (MOD-0018-FU9) — tenant Permission catalog viewer (READ-ONLY). The datatable loads the list
// from the gateway (AuthService /api/permissions); this controller only renders the page and supplies
// distinct-module filter options. No mutation endpoints. Backend [HasPermission] is authoritative.
[Authorize]
[Route("Permissions")]
public sealed class PermissionsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<PermissionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public PermissionsController(HttpClient httpClient, IConfiguration configuration, ILogger<PermissionsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Governance/Permissions/Index.cshtml");

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
            return Json(new { modules = Array.Empty<object>() });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/permissions");
            if (!response.IsSuccessStatusCode)
                return Json(new { modules = Array.Empty<object>() });

            var payload = await response.Content.ReadFromJsonAsync<GovernanceGatewayResponse<List<PermissionCatalogItem>>>(_jsonOptions);
            var modules = (payload?.Data ?? [])
                .Select(x => x.Module)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new { value = x, text = x })
                .ToList();

            return Json(new { modules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permission catalog lookups failed.");
            return Json(new { modules = Array.Empty<object>() });
        }
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        // BL-294 — read the token through AuthTokenCookies, NEVER Request.Cookies["access_token"] directly.
        // The access token outgrows a single cookie (>3800 chars) and is written in chunks: the base cookie
        // then holds the literal marker "chunks-N" and the token itself lives in access_tokenC1..CN. A direct
        // read therefore sends `Bearer chunks-4` and the gateway 401s. GetAccessToken reassembles the chunks
        // (and returns a short token unchanged), so this call site works in both shapes.
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private sealed class PermissionCatalogItem
    {
        public string Module { get; set; } = string.Empty;
    }
}
