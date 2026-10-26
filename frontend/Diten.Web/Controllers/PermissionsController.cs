using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Governance;
using Diten.Web.Security;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// FE-C (MOD-0018-FU9) — tenant Permission catalog viewer (READ-ONLY). The datatable loads the list
// from the gateway (AuthService /api/permissions); this controller only renders the page and supplies
// distinct-module filter options. No mutation endpoints. Backend [HasPermission] is authoritative.
[Authorize]
[Route("Permissions")]
public sealed class PermissionsController : Controller
{
    // WP-USERS-ROLES-SMALL-01 (BL-617, UAS-001; the URA FIX1 pattern of RoleAssignmentsController) — the key that opens
    // this screen: AuthService's PermissionsController reads the catalog under [HasPermission("auth.roles.read")].
    // Display + proxy gate only; AuthService stays the authority.
    public const string ReadPermission = "auth.roles.read";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<PermissionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public PermissionsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<PermissionsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // UAS-001 — 403 and the access-denied face inside the shell (Index.cshtml checks first); never a redirect.
        if (!PermissionClaims.HasPermission(User, ReadPermission))
            Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Governance/Permissions/Index.cshtml");
    }

    // A read this user may not make is refused here, before any call leaves for the gateway, with the sentence a 403
    // from AuthService produces through GatewayRefusal. A JsonResult, not StatusCode(403, value): the refusal is always
    // the same JSON body whatever the caller's Accept header (RoleAssignmentsController, URA FIX2).
    private IActionResult? RefuseWithoutPermission() =>
        PermissionClaims.HasPermission(User, ReadPermission)
            ? null
            : new JsonResult(GatewayRefusal.Local(_sharedLocalizer["AccessDenied"].Value))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (RefuseWithoutPermission() is { } refused)
            return refused;

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
