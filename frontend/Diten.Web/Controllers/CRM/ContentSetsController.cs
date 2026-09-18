using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// SCMM-14-UI (CAND-CAP-0011) Content Studio workspace for ContentSet (assembly draft) authoring. Proxy-only, mirroring
/// the Claims console: all business traffic is proxied server-side through Gateway 5000 to the ready ContentSet surface
/// (<c>/api/crm/content-composition/content-sets</c>) and the read-only picker sources (composition templates, content
/// scopes, knowledge contents, claims, concept types). The browser never sees a service URL or bearer token; no business
/// rule, arrangement-cardinality or version-pin logic lives here (the CrmService CQRS is authoritative). Every selection
/// is a searchable select2 — no raw id entry (D14-e). No delete surface — closing a set is Archive. No freeze / render /
/// release here (SCMM-15/16/17). RBAC keys (<c>crm.content-set.*</c>) are seeded + granted (SCMM-14) — no dev-fallback.
/// </summary>
[Authorize]
[Route("CRM/ContentSets")]
public sealed class ContentSetsController : Controller
{
    private const string ReadPermission = "crm.content-set.read";
    private const string ManagePermission = "crm.content-set.manage";
    private const string ViewRoot = "~/Views/CRM/ContentSets";

    private const string SetsBase = "/api/crm/content-composition/content-sets";
    private const string ScopesBase = "/api/crm/content-composition/content-scopes";
    private const string ClaimsBase = "/api/crm/content-composition/claims";
    private const string TemplatesBase = "/api/crm/knowledge/concept-chain-templates";
    private const string ContentsBase = "/api/crm/knowledge/contents";
    private const string ConceptTypesBase = "/api/crm/knowledge/concept-types";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ContentSetsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ContentSetsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ContentSetsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Pages ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        return View($"{ViewRoot}/Create.cshtml", new ContentSetCreateViewModel { SetCode = SuggestSetCode() });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentSetCreateViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        if (!ModelState.IsValid) return View($"{ViewRoot}/Create.cshtml", model);

        var payload = new
        {
            model.SetCode,
            model.SetName,
            model.Description,
            model.ConceptChainTemplateId,
            contentScopeId = model.ContentScopeId is { } s && s != Guid.Empty ? s : (Guid?)null
        };
        var response = await SendGatewayAsync(HttpMethod.Post, SetsBase, payload, ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ContentGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Edit), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    // The workspace shell. JS loads the set + its pinned template skeleton and drives every mutation through the proxy.
    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        ViewData["ContentSetId"] = id;
        return View($"{ViewRoot}/Edit.cshtml");
    }

    // ---------------- Same-origin browser proxy — ContentSet surface ----------------

    [HttpGet("api/content-sets")]
    public Task<IActionResult> SetList(CancellationToken ct) =>
        ProxyGetAsync($"{SetsBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/content-sets/{contentSetId:guid}")]
    public Task<IActionResult> SetGet(Guid contentSetId, CancellationToken ct) =>
        ProxyGetAsync($"{SetsBase}/{contentSetId}", ReadPermission, ct);

    [HttpPut("api/content-sets/{contentSetId:guid}")]
    public Task<IActionResult> SetUpdate(Guid contentSetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{SetsBase}/{contentSetId}", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/clone")]
    public Task<IActionResult> SetClone(Guid contentSetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/clone", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/archive")]
    public Task<IActionResult> SetArchive(Guid contentSetId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/archive", null, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/apply-eligibility")]
    public Task<IActionResult> SetApplyEligibility(Guid contentSetId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/apply-eligibility", null, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/components")]
    public Task<IActionResult> AddComponent(Guid contentSetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/components", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/components/{selectionId:guid}/arrange")]
    public Task<IActionResult> ArrangeComponent(Guid contentSetId, Guid selectionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/components/{selectionId}/arrange", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/components/{selectionId:guid}/remove")]
    public Task<IActionResult> RemoveComponent(Guid contentSetId, Guid selectionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/components/{selectionId}/remove", null, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/claims")]
    public Task<IActionResult> AddClaim(Guid contentSetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/claims", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/claims/{selectionId:guid}/arrange")]
    public Task<IActionResult> ArrangeClaim(Guid contentSetId, Guid selectionId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/claims/{selectionId}/arrange", body, ManagePermission, ct);

    [HttpPost("api/content-sets/{contentSetId:guid}/claims/{selectionId:guid}/remove")]
    public Task<IActionResult> RemoveClaim(Guid contentSetId, Guid selectionId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{SetsBase}/{contentSetId}/claims/{selectionId}/remove", null, ManagePermission, ct);

    // ---------------- Same-origin browser proxy — read-only picker sources ----------------

    [HttpGet("api/templates")]
    public Task<IActionResult> TemplateList(CancellationToken ct) =>
        ProxyGetAsync($"{TemplatesBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/templates/{templateId:guid}")]
    public Task<IActionResult> TemplateGet(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync($"{TemplatesBase}/{templateId}", ReadPermission, ct);

    [HttpGet("api/scopes")]
    public Task<IActionResult> ScopeList(CancellationToken ct) =>
        ProxyGetAsync($"{ScopesBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"{ContentsBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/claims")]
    public Task<IActionResult> ClaimList(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/concept-types")]
    public Task<IActionResult> ConceptTypeList(CancellationToken ct) =>
        ProxyGetAsync($"{ConceptTypesBase}{Request.QueryString}", ReadPermission, ct);

    // ---------------- helpers ----------------

    private static string SuggestSetCode() =>
        $"SET-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken ct)
    {
        if (RequireJson(permission) is { } denied) return denied;
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, ct), ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct)
    {
        if (RequireJson(permission) is { } denied) return denied;
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
                var jsonBody = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, _json);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContentSet Gateway request failed: {Method} {Path}", method, path);
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

    private async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return [_sharedLocalizer["GatewayError"].Value];
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ContentGatewayResponse<object>>(_json, ct);
            if (envelope?.Errors.Count > 0) return envelope.Errors;
        }
        catch { }
        var raw = await response.Content.ReadAsStringAsync(ct);
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission) =>
        HasAnyPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission) =>
        HasAnyPermission(permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
