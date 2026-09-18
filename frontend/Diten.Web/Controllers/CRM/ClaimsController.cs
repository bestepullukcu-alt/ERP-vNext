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
/// SCMM-12-UI (CAND-CAP-0011) Claim authoring console. Proxy-only, mirroring the MOD-0162-FU03 KnowledgeConcepts
/// console: all business traffic is proxied server-side through Gateway 5000, the browser never sees a service URL or
/// bearer token, and no business rule lives here (the CrmService Claim CQRS is the authoritative validation + permission
/// layer). No delete surface — closing a claim is Archive. Approval (draft → approved) is a dedicated action and freezes
/// the governed body. The component picker is fed read-only from the ready MOD-0162 KnowledgeContent list
/// (<c>/api/crm/knowledge/contents</c>); no new API is invented. RBAC keys (<c>crm.claim.*</c>) are seeded and granted
/// (SCMM-12-API), so there is no dev-fallback permission.
/// </summary>
[Authorize]
[Route("CRM/Claims")]
public sealed class ClaimsController : Controller
{
    private const string ReadPermission = "crm.claim.read";
    private const string ManagePermission = "crm.claim.manage";
    private const string ApprovePermission = "crm.claim.approve";
    private const string ViewRoot = "~/Views/CRM/Claims";

    // The claim HTTP surface (SCMM-12-API) + the component picker source (MOD-0162 FU02), both through the gateway.
    private const string ClaimsBase = "/api/crm/content-composition/claims";
    private const string ContentsBase = "/api/crm/knowledge/contents";

    // Create / update carry only these editable states; approved / archived are the dedicated approve / archive actions.
    private static readonly string[] EditableStatuses = ["draft", "inactive"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ClaimsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ClaimsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ClaimsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Pages (Compact list + route-based Create / Edit) ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        var model = new ClaimEditViewModel
        {
            EffectiveFrom = DateTimeOffset.Now,
            ClaimCode = SuggestClaimCode(),
            ClaimVersion = "1.0",
            Status = "draft"
        };
        await PopulateFormOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClaimEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, ct);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, ClaimsBase, ToPayload(model, forUpdate: false), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ClaimGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Edit), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateFormOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        var claim = await LoadClaimAsync(id, ct);
        if (claim is null) return NotFound();

        var model = ToEditModel(claim);
        await PopulateFormOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ClaimEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        model.ClaimId = id;
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, ct);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"{ClaimsBase}/{id}", ToPayload(model, forUpdate: true), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateFormOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    // ---------------- Same-origin browser proxy (claim + component-picker allowlist only) ----------------

    // Claim list — the Index DataTable source. Forwards the query string (status / effectiveAt / search / includeArchived).
    [HttpGet("api/claims")]
    public Task<IActionResult> ClaimList(CancellationToken ct) =>
        ProxyGetAsync($"{ClaimsBase}{Request.QueryString}", ReadPermission, ct);

    [HttpPost("api/claims/{claimId:guid}/approve")]
    public Task<IActionResult> ApproveClaim(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/approve", null, ApprovePermission, ct);

    [HttpPost("api/claims/{claimId:guid}/archive")]
    public Task<IActionResult> ArchiveClaim(Guid claimId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ClaimsBase}/{claimId}/archive", null, ManagePermission, ct);

    // Component picker source — read-only MOD-0162 KnowledgeContent list. Only the list read is allowlisted (no
    // knowledge write path is exposed here); the picker maps rows to by-id ComponentRefs.
    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"{ContentsBase}{Request.QueryString}", ReadPermission, ct);

    // ---------------- helpers ----------------

    private static string SuggestClaimCode() =>
        $"CLM-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private async Task PopulateFormOptionsAsync(ClaimEditViewModel model, CancellationToken ct)
    {
        model.Statuses = EditableStatuses;
        model.ComponentOptions = await LoadComponentOptionsAsync(ct);
        await EnsureSelectedComponentsAsync(model.ComponentOptions, model.ComponentRefs, ct);
    }

    // Component picker options — the ready KnowledgeContent list, labelled "code — title". Read-only reference; a failed
    // load yields an empty list (the form still posts any stored refs, kept by EnsureSelected).
    private async Task<List<ClaimOptionViewModel>> LoadComponentOptionsAsync(CancellationToken ct)
    {
        var options = new List<ClaimOptionViewModel>();
        var response = await SendGatewayAsync(HttpMethod.Get, $"{ContentsBase}?includeArchived=false", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return options;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)) return options;
            JsonElement items;
            if (data.ValueKind == JsonValueKind.Array) items = data;
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var it)) items = it;
            else return options;

            foreach (var el in items.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var id = GetFirstString(el, "contentId", "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var code = GetFirstString(el, "contentCode", "code");
                var title = GetFirstString(el, "contentTitle", "title", "name");
                var label = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(title) ? $"{code} — {title}"
                    : title ?? code ?? id!;
                options.Add(new ClaimOptionViewModel { Value = id!, Label = label });
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Claim component option load failed."); }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // A stored component ref that is not in the active list (archived / unresolved) is kept as an inactive option so it
    // survives the round-trip — the reference is provenance and must never be silently dropped by the picker.
    private async Task EnsureSelectedComponentsAsync(
        List<ClaimOptionViewModel> options, IReadOnlyList<Guid> current, CancellationToken ct)
    {
        foreach (var id in current.Where(g => g != Guid.Empty).Distinct())
        {
            var idStr = id.ToString();
            if (options.Any(o => string.Equals(o.Value, idStr, StringComparison.OrdinalIgnoreCase))) continue;

            var option = new ClaimOptionViewModel { Value = idStr, Label = idStr, IsInactive = true };
            var response = await SendGatewayAsync(HttpMethod.Get, $"{ContentsBase}/{idStr}", null, ct);
            if (response is not null && response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        var code = GetFirstString(data, "contentCode", "code");
                        var title = GetFirstString(data, "contentTitle", "title", "name");
                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(title)) option.Label = $"{code} — {title}";
                        else if (!string.IsNullOrWhiteSpace(title)) option.Label = title!;
                        else if (!string.IsNullOrWhiteSpace(code)) option.Label = code!;
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Claim component ensure-selected parse failed: {ContentId}", idStr); }
            }
            options.Add(option);
        }
    }

    private async Task<ClaimDetailViewModel?> LoadClaimAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"{ClaimsBase}/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ClaimGatewayResponse<ClaimDetailViewModel>>(_json, ct))?.Data;
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
            _logger.LogError(ex, "Claim Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content.ReadFromJsonAsync<ClaimGatewayResponse<object>>(_json, ct);
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

    // Builds the request payload. Create carries ClaimCode; update omits it (the code is immutable). On an approved
    // claim, Status is left out so the backend preserves the approved status (approved/archived are dedicated actions).
    private static object ToPayload(ClaimEditViewModel m, bool forUpdate)
    {
        var applicability = new
        {
            productRefs = Clean(m.ProductRefs),
            marketRefs = Clean(m.MarketRefs),
            audienceRefs = Clean(m.AudienceRefs),
            eligibilityPolicyId = m.EligibilityPolicyId is { } id && id != Guid.Empty ? id : (Guid?)null
        };
        var status = m.IsApproved ? null : m.Status;

        return forUpdate
            ? new
            {
                m.ClaimName, m.ClaimText, m.EffectiveFrom, m.Description,
                qualifiers = Clean(m.Qualifiers), applicability, evidenceRefs = Clean(m.EvidenceRefs),
                componentRefs = m.ComponentRefs.Where(g => g != Guid.Empty).Distinct().ToList(),
                m.ClaimVersion, status, m.EffectiveTo
            }
            : new
            {
                m.ClaimCode, m.ClaimName, m.ClaimText, m.EffectiveFrom, m.Description,
                qualifiers = Clean(m.Qualifiers), applicability, evidenceRefs = Clean(m.EvidenceRefs),
                componentRefs = m.ComponentRefs.Where(g => g != Guid.Empty).Distinct().ToList(),
                m.ClaimVersion, status, m.EffectiveTo
            };
    }

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).Distinct().ToList();

    private static ClaimEditViewModel ToEditModel(ClaimDetailViewModel c) => new()
    {
        ClaimId = c.ClaimId,
        ClaimCode = c.ClaimCode,
        ClaimName = c.ClaimName,
        Description = c.Description,
        ClaimText = c.ClaimText,
        Qualifiers = [.. c.Qualifiers],
        ProductRefs = [.. c.ProductRefs],
        MarketRefs = [.. c.MarketRefs],
        AudienceRefs = [.. c.AudienceRefs],
        EligibilityPolicyId = c.EligibilityPolicyId,
        EvidenceRefs = [.. c.EvidenceRefs],
        ComponentRefs = [.. c.ComponentRefs],
        ClaimVersion = c.ClaimVersion,
        Status = c.Status,
        EffectiveFrom = c.EffectiveFrom,
        EffectiveTo = c.EffectiveTo,
        IsApproved = string.Equals(c.Status, "approved", StringComparison.OrdinalIgnoreCase),
        IsArchived = c.IsArchived
    };

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? GetFirstString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        return null;
    }

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission) =>
        HasAnyPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission) =>
        HasAnyPermission(permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
