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

    // WP-FREQ-F2 — the create/edit editor is a SEPARATE Golden Compact page (the FREQ-B offcanvas is retired). Both are
    // GET shells the reused form.js drives (it still POSTs the create/update payload to the api proxies below — the page
    // change is presentation only). Writes are gated on the manage permission (canonical OR the documented fallback);
    // an unauthorized user gets 403 (no page skeleton — UAS-001), exactly like the Index read gate.
    [HttpGet("Create")]
    public IActionResult Create() =>
        RequirePage(ManageCanonical, ManageFallback) ?? View($"{ViewRoot}/Create.cshtml");

    [HttpGet("Edit/{policyId:guid}")]
    public IActionResult Edit(Guid policyId) =>
        RequirePage(ManageCanonical, ManageFallback) ?? View($"{ViewRoot}/Edit.cshtml", policyId);

    // WP-FREQ-DET-B — the details view is now a SEPARATE Golden Compact page (the FREQ-C quick-view offcanvas is
    // retired). It is a GET read shell: details.js fetches the policy read model (GET /{id}) + the DET-A detail
    // analysis (GET /{id}/analysis) and renders header / stats / context / notes / status-flow / impact / conflicting
    // policies. Gated on the read permission (canonical OR the documented fallback); an unauthorized user gets 403 (no
    // page skeleton — UAS-001), exactly like the Index read gate.
    [HttpGet("Details/{policyId:guid}")]
    public IActionResult Details(Guid policyId) =>
        RequirePage(ReadCanonical, ReadFallback) ?? View($"{ViewRoot}/Details.cshtml", policyId);

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

    // WP-FREQ-DET-A — read-only detail-analysis pass-through (target impact + conflict outcome). The literal
    // "{guid}/analysis" sits under the EXISTING Gateway visit-frequency-policies {everything} wildcard, so no ocelot
    // change is needed. Same read RBAC as the sibling reads.
    [HttpGet("api/visit-frequency-policies/{policyId:guid}/analysis")]
    public Task<IActionResult> PolicyAnalysis(Guid policyId, CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}/{policyId}/analysis", ct, ReadCanonical, ReadFallback);

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

    // ---------------- WP-FREQ-B entity-picker proxies (read-only pass-throughs to surfaces that ALREADY exist) --------
    // The editor picks a target/context entity by NAME (never a raw GUID). Every list below is an existing sibling read;
    // nothing new is opened, the browser never sees a service URL or a bearer token, and each carries the target
    // module's read permission OR the documented territory.read fallback the console already runs on. account-contact-link
    // has no picker (not in the mockup) — the editor falls back to a manual id input for it.

    [HttpGet("api/segments")]
    public Task<IActionResult> Segments(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/segments{Request.QueryString}", ct, "crm.segment.read", ReadFallback);

    [HttpGet("api/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/accounts{Request.QueryString}", ct, "crm.account.read", ReadFallback);

    [HttpGet("api/contacts")]
    public Task<IActionResult> Contacts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/contacts{Request.QueryString}", ct, "crm.contact.read", ReadFallback);

    [HttpGet("api/territory-models")]
    public Task<IActionResult> TerritoryModels(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/territory-models{Request.QueryString}", ct, "crm.territory.read", ReadFallback);

    // WP-FREQ-F19 — territory-node reverse lookup for edit-restore. A stored policy carries only TerritoryNodeId (no
    // parent model), so the editor resolves the id back to {id,name,modelId,code} to re-select the model + cascade its
    // nodes and show the node BY NAME. Read-only pass-through, same territory.read RBAC as the sibling territory reads.
    // The literal "nodes/by-ids" sits under the EXISTING Gateway {everything} wildcard and can never match the
    // {modelId:guid}/nodes route below ("nodes" is not a GUID) — no ocelot change needed (WP-SEG-DETAILS8 pattern).
    [HttpGet("api/territory-models/nodes/by-ids")]
    public Task<IActionResult> TerritoryNodesByIds(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/territory-models/nodes/by-ids{Request.QueryString}", ct, "crm.territory.read", ReadFallback);

    [HttpGet("api/territory-models/{modelId:guid}/nodes")]
    public Task<IActionResult> TerritoryNodes(Guid modelId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/territory-models/{modelId}/nodes{Request.QueryString}", ct, "crm.territory.read", ReadFallback);

    [HttpGet("api/campaigns")]
    public Task<IActionResult> Campaigns(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/campaigns{Request.QueryString}", ct, "crm.campaign.read", ReadFallback);

    // cycle-period picker — the MOD-0165 FU08 selector; downstream guard still decides visibility (fail-closed).
    [HttpGet("api/cycle-periods")]
    public Task<IActionResult> CyclePeriods(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/cycle-periods/selector{Request.QueryString}", ct, "crm.territory.read", ReadFallback);

    [HttpGet("api/audience-profiles")]
    public Task<IActionResult> AudienceProfiles(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/audience-profiles{Request.QueryString}", ct, "crm.knowledge.subject.read", ReadFallback);

    [HttpGet("api/concept-nodes")]
    public Task<IActionResult> ConceptNodes(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-nodes{Request.QueryString}", ct, "crm.knowledge.concept.read", ReadFallback);

    [HttpGet("api/mdm-brands")]
    public Task<IActionResult> MdmBrands(CancellationToken ct) =>
        ProxyGetAsync($"/api/mdm/brands{Request.QueryString}", ct, "mdm.brands.read", ReadFallback);

    [HttpGet("api/mdm-products")]
    public Task<IActionResult> MdmProducts(CancellationToken ct) =>
        ProxyGetAsync($"/api/mdm/products{Request.QueryString}", ct, "mdm.products.read", ReadFallback);

    /// <summary>business-unit context picker. Reads the MOD-0048 PUBLISHED business-unit value set (tenant scope_key) —
    /// the same consumer call the Territory + Campaign forms make. The stored <c>BusinessUnit</c> is a value CODE (a
    /// string), not a GUID, so the editor keeps the picked value code. An unpublished set degrades to an empty list.</summary>
    [HttpGet("api/business-units")]
    public async Task<IActionResult> BusinessUnits(CancellationToken ct)
    {
        if (RequireJson(ReadCanonical, ReadFallback) is { } denied) return denied;
        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return StatusCode(StatusCodes.Status401Unauthorized, new { message = "Tenant context is required." });

        var path = $"/api/v1/reference-data/sets/business-unit/published-values?scope_key={Uri.EscapeDataString(tenantId)}";
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, ct), ct);
    }

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
