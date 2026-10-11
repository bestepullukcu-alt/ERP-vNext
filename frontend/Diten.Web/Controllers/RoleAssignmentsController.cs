using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Security;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// FE-C (MOD-0018-FU9) — role → permission assignment (the user face of the entitlement bridge).
// Mutations proxy through here (antiforgery + server-side bearer/tenant forwarding) to AuthService.
// Reads (roles list, catalog, role permissions) are loaded client-side from the gateway. The screen
// distinguishes grant sources (System/Module/Manual); only Manual grants are operator-removable.
// UX-only — AuthService [HasPermission("auth.roles.assign-permission")] is authoritative.
[Authorize]
[Route("RoleAssignments")]
public sealed class RoleAssignmentsController : Controller
{
    // WP-URA-FIX-01 FIX1 (UAS-001, CT decision: the same page family as User Roles) — the key that opens this screen:
    // the platform manifest's ROLE_PERMISSIONS page and AuthService's role-permission assign / revoke require it.
    // Display + proxy gate only; AuthService stays the authority.
    public const string AssignPermissionPermission = "auth.roles.assign-permission";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<RoleAssignmentsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RoleAssignmentsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<RoleAssignmentsController> logger)
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
        if (!PermissionClaims.HasPermission(User, AssignPermissionPermission))
            Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Governance/RoleAssignments/Index.cshtml");
    }

    // A write this user may not make is refused here, before any call leaves for the gateway, with the sentence a 403
    // from AuthService produces through GatewayRefusal.
    private IActionResult? RefuseWithoutPermission() =>
        PermissionClaims.HasPermission(User, AssignPermissionPermission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden,
                GatewayRefusal.Local(_sharedLocalizer["AccessDenied"].Value));

    [HttpPost("assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign([FromForm] Guid roleId, [FromForm] Guid permissionId)
    {
        if (RefuseWithoutPermission() is { } refused)
            return refused;

        if (roleId == Guid.Empty || permissionId == Guid.Empty)
            return Json(GatewayRefusal.Local(_sharedLocalizer["ValidationFailed"].Value));

        if (!AddAuthHeaders())
            return Json(GatewayRefusal.Local(_sharedLocalizer["Unauthorized"].Value));

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/roles/{roleId}/permissions",
                new { permissionId },
                _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer, _logger));
        }
        catch (Exception ex)
        {
            return Json(GatewayRefusal.Failure(ex, _sharedLocalizer, _logger, "Role-permission assign"));
        }
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromForm] Guid roleId, [FromForm] Guid permissionId)
    {
        if (RefuseWithoutPermission() is { } refused)
            return refused;

        if (roleId == Guid.Empty || permissionId == Guid.Empty)
            return Json(GatewayRefusal.Local(_sharedLocalizer["ValidationFailed"].Value));

        if (!AddAuthHeaders())
            return Json(GatewayRefusal.Local(_sharedLocalizer["Unauthorized"].Value));

        try
        {
            var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/roles/{roleId}/permissions/{permissionId}");
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer, _logger));
        }
        catch (Exception ex)
        {
            return Json(GatewayRefusal.Failure(ex, _sharedLocalizer, _logger, "Role-permission revoke"));
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
}
