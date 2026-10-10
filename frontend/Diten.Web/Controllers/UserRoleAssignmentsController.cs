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

// FE-C 3/3 (MOD-0018-FU9) — user → role assignment. Component-based screen; mutations proxy through
// here (antiforgery + bearer/tenant forwarding) to AuthService POST/DELETE /api/users/{id}/roles.
// Reads (users, roles, a user's roles) load client-side from the gateway. UX-only — AuthService
// [HasPermission("auth.users.assign-role")] is authoritative.
[Authorize]
[Route("UserRoleAssignments")]
public sealed class UserRoleAssignmentsController : Controller
{
    // WP-URA-FIX-01 (UAS-001) — the key that opens this screen: the platform manifest's USER_ROLES page requires it, and
    // AuthService's POST / DELETE /api/users/{id}/roles demand it. Display + proxy gate only; AuthService stays the authority.
    public const string AssignRolePermission = "auth.users.assign-role";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<UserRoleAssignmentsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public UserRoleAssignmentsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<UserRoleAssignmentsController> logger)
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
        // UAS-001 — no redirect for a signed-in user without the key: the status says 403 and the view draws the
        // access-denied surface inside the shell, and nothing of the screen (Index.cshtml checks first).
        if (!PermissionClaims.HasPermission(User, AssignRolePermission))
            Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Governance/UserRoleAssignments/Index.cshtml");
    }

    // A write this user may not make is refused here with a 403 and the shell's own sentence, before any call leaves
    // for the gateway — the same answer AuthService would give, without the round trip or its technical text.
    private IActionResult? RefuseWithoutPermission() =>
        PermissionClaims.HasPermission(User, AssignRolePermission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden,
                GatewayRefusal.Local(_sharedLocalizer["AccessDeniedMessage"].Value));

    [HttpPost("assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign([FromForm] Guid userId, [FromForm] Guid roleId)
    {
        if (RefuseWithoutPermission() is { } refused)
            return refused;

        if (userId == Guid.Empty || roleId == Guid.Empty)
            return Json(GatewayRefusal.Local(_sharedLocalizer["ValidationFailed"].Value));

        if (!AddAuthHeaders())
            return Json(GatewayRefusal.Local(_sharedLocalizer["Unauthorized"].Value));

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/users/{userId}/roles",
                new { roleId },
                _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer, _logger));
        }
        catch (Exception ex)
        {
            return Json(GatewayRefusal.Failure(ex, _sharedLocalizer, _logger, "User-role assign"));
        }
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromForm] Guid userId, [FromForm] Guid roleId)
    {
        if (RefuseWithoutPermission() is { } refused)
            return refused;

        if (userId == Guid.Empty || roleId == Guid.Empty)
            return Json(GatewayRefusal.Local(_sharedLocalizer["ValidationFailed"].Value));

        if (!AddAuthHeaders())
            return Json(GatewayRefusal.Local(_sharedLocalizer["Unauthorized"].Value));

        try
        {
            var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/users/{userId}/roles/{roleId}");
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer, _logger));
        }
        catch (Exception ex)
        {
            return Json(GatewayRefusal.Failure(ex, _sharedLocalizer, _logger, "User-role revoke"));
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
