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
/// SCMM-11-UI (CAND-CAP-0011) Eligibility policy authoring console + evaluate/preview. Proxy-only, mirroring the SCMM-14
/// ContentSets / SCMM-12 Claims consoles: all business traffic is proxied server-side through Gateway 5000 to the ready
/// SCMM-11 eligibility surface (/api/crm/content-composition/eligibility-policies + eligibility:evaluate); the browser
/// never sees a service URL or bearer token, and no eligibility logic lives here (the CrmService resolver is
/// authoritative — the UI only sends the context and shows the disjoint result). No delete surface — closing a policy is
/// Archive. Publish / freeze / review is out of scope (a published policy is view-only here). RBAC keys
/// (<c>crm.eligibility.*</c>) are seeded + granted (SCMM-11-follow-API), so there is no dev-fallback.
/// </summary>
[Authorize]
[Route("CRM/EligibilityPolicies")]
public sealed class EligibilityPoliciesController : Controller
{
    private const string ReadPermission = "crm.eligibility.read";
    private const string ManagePermission = "crm.eligibility.manage";
    private const string EvaluatePermission = "crm.eligibility.evaluate";
    private const string ViewRoot = "~/Views/CRM/EligibilityPolicies";

    private const string PoliciesBase = "/api/crm/content-composition/eligibility-policies";
    private const string EvaluatePath = "/api/crm/content-composition/eligibility:evaluate";

    // Author states offered by this console; approved/published/archived are lifecycle transitions handled elsewhere.
    private static readonly string[] EditableStatuses = ["draft", "review", "inactive"];
    // Context dimensions (docx: audience/product/market/channel/language/period — reference strings, sector-neutral, no LE).
    private static readonly string[] ContextDimensions = ["audience", "product", "market", "channel", "language", "period"];
    private static readonly string[] MatchKinds = ["includes", "excludes"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<EligibilityPoliciesController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public EligibilityPoliciesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<EligibilityPoliciesController> logger)
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
        var model = new EligibilityPolicyEditViewModel
        {
            PolicyCode = SuggestPolicyCode(),
            PolicyVersion = "1.0",
            Status = "draft",
            EffectiveFrom = DateTimeOffset.Now,
            Conditions = [new EligibilityConditionRowViewModel()]
        };
        PopulateFormOptions(model);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EligibilityPolicyEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        PopulateFormOptions(model);
        if (!ModelState.IsValid) return View($"{ViewRoot}/Create.cshtml", model);

        var response = await SendGatewayAsync(HttpMethod.Post, PoliciesBase, ToPayload(model, forUpdate: false), ct);
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

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        var policy = await LoadPolicyAsync(id, ct);
        if (policy is null) return NotFound();
        var model = ToEditModel(policy);
        PopulateFormOptions(model);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EligibilityPolicyEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        model.EligibilityPolicyId = id;
        PopulateFormOptions(model);
        if (!ModelState.IsValid) return View($"{ViewRoot}/Edit.cshtml", model);

        var response = await SendGatewayAsync(HttpMethod.Put, $"{PoliciesBase}/{id}", ToPayload(model, forUpdate: true), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    // ---------------- Same-origin browser proxy ----------------

    [HttpGet("api/eligibility-policies")]
    public Task<IActionResult> PolicyList(CancellationToken ct) =>
        ProxyGetAsync($"{PoliciesBase}{Request.QueryString}", ReadPermission, ct);

    [HttpPost("api/eligibility-policies/{policyId:guid}/archive")]
    public Task<IActionResult> ArchivePolicy(Guid policyId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{PoliciesBase}/{policyId}/archive", null, ManagePermission, ct);

    // Evaluate — the resolver decides; this only forwards the context and returns the disjoint result.
    [HttpPost("api/eligibility:evaluate")]
    public Task<IActionResult> Evaluate([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, EvaluatePath, body, EvaluatePermission, ct);

    // ---------------- helpers ----------------

    private static string SuggestPolicyCode() =>
        $"POL-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static void PopulateFormOptions(EligibilityPolicyEditViewModel model)
    {
        model.Statuses = EditableStatuses;
        model.Dimensions = ContextDimensions;
        model.Matches = MatchKinds;
        if (model.Conditions.Count == 0 && !model.IsReadOnly)
        {
            model.Conditions.Add(new EligibilityConditionRowViewModel());
        }
    }

    private async Task<EligibilityPolicyDetailViewModel?> LoadPolicyAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"{PoliciesBase}/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ContentGatewayResponse<EligibilityPolicyDetailViewModel>>(_json, ct))?.Data;
    }

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
            _logger.LogError(ex, "Eligibility Gateway request failed: {Method} {Path}", method, path);
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

    private static object ToPayload(EligibilityPolicyEditViewModel m, bool forUpdate)
    {
        var conditions = (m.Conditions ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Dimension))
            .Select(c => new
            {
                dimension = c.Dimension.Trim(),
                match = string.IsNullOrWhiteSpace(c.Match) ? "includes" : c.Match.Trim(),
                values = Clean(c.Values),
                required = c.Required
            })
            .ToList();

        return forUpdate
            ? new
            {
                m.PolicyName, m.Description, m.PolicyVersion, m.Status, m.EffectiveFrom, m.EffectiveTo, conditions
            }
            : new
            {
                m.PolicyCode, m.PolicyName, m.Description, m.PolicyVersion, m.Status, m.EffectiveFrom, m.EffectiveTo, conditions
            };
    }

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).Distinct().ToList();

    private static EligibilityPolicyEditViewModel ToEditModel(EligibilityPolicyDetailViewModel p) => new()
    {
        EligibilityPolicyId = p.EligibilityPolicyId,
        PolicyCode = p.PolicyCode,
        PolicyName = p.PolicyName,
        Description = p.Description,
        PolicyVersion = p.PolicyVersion,
        Status = p.Status,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo,
        Conditions = [.. p.Conditions],
        IsPublished = string.Equals(p.Status, "published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Status, "approved", StringComparison.OrdinalIgnoreCase),
        IsArchived = p.IsArchived
    };

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
