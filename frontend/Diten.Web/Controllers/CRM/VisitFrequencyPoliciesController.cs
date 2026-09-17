using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0165-FU03 (WP-FREQ-A) Visit Frequency / Call-Cycle Policy console — proxy-only skeleton mirroring the
/// SCMM-11 EligibilityPolicies console. All business traffic is proxied server-side through Gateway 5000 to the ready
/// FU03 surface (<c>/api/crm/visit-frequency-policies</c> + <c>/contract</c> + <c>/resolve</c> + CRUD + archive +
/// the WP-FREQ-A additive <c>/delete</c> soft-delete); the browser never sees a service URL or bearer token, and no
/// frequency logic lives here (the CrmService resolver/handlers are authoritative).
/// <para>
/// <b>Lifecycle:</b> Archive closes a policy as readable history (status=archived, still listed); Delete is the
/// WP-FREQ-A soft-delete (IsDeleted=true) that removes it from the list + resolve working set — the two are distinct
/// and both are soft.
/// </para>
/// <para>
/// <b>RBAC:</b> the canonical keys are <c>crm.visit-frequency-policy.{read,manage,resolve}</c>, but the RBAC catalog
/// does not carry them yet, so — exactly like the CrmService <c>[HasPermission]</c> guards — this console runs on the
/// documented fallback (<c>crm.territory.read</c> for read/resolve, <c>crm.territory.model.manage</c> for writes).
/// Either the canonical OR the fallback key opens each surface; the fallback widens nothing. Follow-up: MOD-0165-FU-RBAC.
/// </para>
/// <para>
/// <b>Scope (WP-FREQ-A):</b> this is the console iskeleti — Index (2 tabs) + Golden Compact list + proxy + soft-delete.
/// The create/edit offcanvas editor (FREQ-B), the details quick-view and the resolve/çözümleme panel (FREQ-C) are empty
/// placeholders here; the create/update/resolve proxies are wired ready for them.
/// </para>
/// </summary>
[Authorize]
[Route("CRM/VisitFrequencyPolicies")]
public sealed class VisitFrequencyPoliciesController : Controller
{
    // Canonical FU03 keys (definition only — not seeded yet) OR the documented fallback the CrmService guards use.
    private const string ReadCanonical = "crm.visit-frequency-policy.read";
    private const string ManageCanonical = "crm.visit-frequency-policy.manage";
    private const string ResolveCanonical = "crm.visit-frequency-policy.resolve";
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";

    private const string ViewRoot = "~/Views/CRM/VisitFrequencyPolicies";
    private const string PoliciesBase = "/api/crm/visit-frequency-policies";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitFrequencyPoliciesController> _logger;

    public VisitFrequencyPoliciesController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<VisitFrequencyPoliciesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- Page ----------------

    [HttpGet("")]
    public IActionResult Index() =>
        RequirePage(ReadCanonical, ReadFallback) ?? View($"{ViewRoot}/Index.cshtml");

    // ---------------- Same-origin browser proxy ----------------

    [HttpGet("api/visit-frequency-policies")]
    public Task<IActionResult> PolicyList(CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}{Request.QueryString}", ct, ReadCanonical, ReadFallback);

    [HttpGet("api/visit-frequency-policies/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}/contract", ct, ReadCanonical, ReadFallback);

    [HttpGet("api/visit-frequency-policies/resolve")]
    public Task<IActionResult> Resolve(CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}/resolve{Request.QueryString}", ct, ResolveCanonical, ReadFallback);

    [HttpGet("api/visit-frequency-policies/{policyId:guid}")]
    public Task<IActionResult> PolicyGet(Guid policyId, CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}/{policyId}", ct, ReadCanonical, ReadFallback);

    [HttpPost("api/visit-frequency-policies")]
    public Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, PoliciesBase, body, ct, ManageCanonical, ManageFallback);

    [HttpPut("api/visit-frequency-policies/{policyId:guid}")]
    public Task<IActionResult> Update(Guid policyId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{PoliciesBase}/{policyId}", body, ct, ManageCanonical, ManageFallback);

    [HttpPost("api/visit-frequency-policies/{policyId:guid}/archive")]
    public Task<IActionResult> Archive(Guid policyId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PoliciesBase}/{policyId}/archive", null, ct, ManageCanonical, ManageFallback);

    // WP-FREQ-A additive soft-delete — DISTINCT from archive (archive keeps history; delete removes from the working set).
    [HttpPost("api/visit-frequency-policies/{policyId:guid}/delete")]
    public Task<IActionResult> Delete(Guid policyId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PoliciesBase}/{policyId}/delete", null, ct, ManageCanonical, ManageFallback);

    // ---------------- helpers ----------------

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct, params string[] permissions)
    {
        if (RequireJson(permissions) is { } denied) return denied;
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, ct), ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, CancellationToken ct, params string[] permissions)
    {
        if (RequireJson(permissions) is { } denied) return denied;
        if (body.HasValue && ContainsTenantId(body.Value))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        return await ToProxyResultAsync(await SendGatewayAsync(method, path, body, ct), ct);
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (body is not null)
            {
                var jsonBody = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VisitFrequencyPolicies Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(params string[] permissions) =>
        HasAnyPermission(permissions) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(params string[] permissions) =>
        HasAnyPermission(permissions)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
