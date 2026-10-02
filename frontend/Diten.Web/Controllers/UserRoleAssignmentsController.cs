using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Services.Governance;
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
    public IActionResult Index() => View("~/Views/Governance/UserRoleAssignments/Index.cshtml");

    [HttpPost("assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign([FromForm] Guid userId, [FromForm] Guid roleId)
    {
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
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User-role assign failed for {UserId}/{RoleId}.", userId, roleId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromForm] Guid userId, [FromForm] Guid roleId)
    {
        if (userId == Guid.Empty || roleId == Guid.Empty)
            return Json(GatewayRefusal.Local(_sharedLocalizer["ValidationFailed"].Value));

        if (!AddAuthHeaders())
            return Json(GatewayRefusal.Local(_sharedLocalizer["Unauthorized"].Value));

        try
        {
            var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/users/{userId}/roles/{roleId}");
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(await GatewayRefusal.ReadAsync(response, _sharedLocalizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User-role revoke failed for {UserId}/{RoleId}.", userId, roleId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        return [string.IsNullOrWhiteSpace(message) ? _sharedLocalizer["GatewayError"].Value : message];
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
