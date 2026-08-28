using System.Net;
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
/// MOD-0167-FU02 Segment Admin UI (Compact). All business traffic is proxied server-side through Gateway 5000; the
/// browser never sees a service URL or a bearer token. The CrmService runtime stays the authoritative validation and
/// permission layer — nothing is decided here.
/// <para>There is no delete surface: closing anything is Archive. The criteria tree travels inside the segment payload
/// (it is embedded), and the manual membership rows are the segment's own sub-resource.</para>
/// </summary>
[Authorize]
[Route("CRM/Segments")]
public sealed class SegmentsController : Controller
{
    private const string ReadPermission = "crm.segment.read";
    private const string ManagePermission = "crm.segment.manage";
    private const string ActivatePermission = "crm.segment.activate";
    private const string ResolvePermission = "crm.segment.resolve";
    private const string TargetReadPermission = "crm.segment.target.read";
    private const string TargetManagePermission = "crm.segment.target.manage";
    private const string GlobalProductReadPermission = "mdm.global-products.read";
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/Segments";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<SegmentsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public SegmentsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<SegmentsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Compact pages ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission, ReadFallback) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var model = new SegmentEditViewModel
        {
            EffectiveFrom = DateTimeOffset.Now,
            SegmentCode = SuggestSegmentCode()
        };
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SegmentEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/segments", ToCreatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<SegmentGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            // A new segment lands on Edit so the author can keep shaping the rule and add manual rows immediately.
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Edit), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var segment = await LoadSegmentAsync(id, ct);
        if (segment is null) return NotFound();
        if (segment.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedSegmentReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(segment);
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SegmentEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.SegmentId = id;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/segments/{id}", ToUpdatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var segment = await LoadSegmentAsync(id, ct);
        if (segment is null) return NotFound();

        var model = new SegmentPageViewModel
        {
            Segment = segment,
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback),
            // Member identity is PII: the resolve preview is hidden unless the actor may actually see members.
            CanResolve = HasAnyPermission(ResolvePermission, ReadPermission, ReadFallback)
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- Same-origin browser proxy (FU02 allowlist only) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/segments/contract", ReadPermission, ct, ReadFallback);

    [HttpGet("api/attribute-catalog")]
    public Task<IActionResult> AttributeCatalog(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/segments/attribute-catalog", ReadPermission, ct, ReadFallback);

    [HttpGet("api/segments")]
    public Task<IActionResult> SegmentList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/segments{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/segments/{segmentId:guid}")]
    public Task<IActionResult> SegmentGet(Guid segmentId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/segments/{segmentId}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/segments/{segmentId:guid}/activate")]
    public Task<IActionResult> Activate(Guid segmentId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/activate{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/segments/{segmentId:guid}/archive")]
    public Task<IActionResult> Archive(Guid segmentId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/archive{Request.QueryString}", null,
            ManagePermission, ct, ManageFallback);

    [HttpPost("api/segments/{segmentId:guid}/new-version")]
    public Task<IActionResult> NewVersion(Guid segmentId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/new-version", null,
            ManagePermission, ct, ManageFallback);

    /// <summary>The resolve preview. It writes nothing — the POST only carries options.</summary>
    [HttpPost("api/segments/{segmentId:guid}/resolve")]
    public Task<IActionResult> Resolve(Guid segmentId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/resolve", body,
            ResolvePermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/segments/{segmentId:guid}/membership/evaluate")]
    public Task<IActionResult> Evaluate(Guid segmentId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/membership/evaluate", body,
            ResolvePermission, ct, ReadPermission, ReadFallback);

    [HttpGet("api/segments/{segmentId:guid}/targets")]
    public Task<IActionResult> TargetList(Guid segmentId, CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/segments/{segmentId}/targets{Request.QueryString}",
            TargetReadPermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/segments/{segmentId:guid}/targets")]
    public Task<IActionResult> AddTarget(Guid segmentId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/segments/{segmentId}/targets", body,
            TargetManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/segments/{segmentId:guid}/targets/{targetId:guid}")]
    public Task<IActionResult> UpdateTarget(
        Guid segmentId, Guid targetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Put, $"/api/crm/segments/{segmentId}/targets/{targetId}", body,
            TargetManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/segments/{segmentId:guid}/targets/{targetId:guid}/archive")]
    public Task<IActionResult> ArchiveTarget(
        Guid segmentId, Guid targetId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post,
            $"/api/crm/segments/{segmentId}/targets/{targetId}/archive{Request.QueryString}", null,
            TargetManagePermission, ct, ManagePermission, ManageFallback);

    /// <summary>The concept.affinity value picker. Re-uses the EXISTING MDM global-product selector — the same surface
    /// the MOD-0162 FU03 concept-node external reference picker uses. No new endpoint is opened here.</summary>
    [HttpGet("api/global-products")]
    public Task<IActionResult> GlobalProducts(CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/global-products/selector{Request.QueryString}", GlobalProductReadPermission, ct);

    // ---------------- P1a value pickers (all pass-throughs to surfaces that ALREADY exist) ----------------

    /// <summary>
    /// Published values of one MOD-0048 reference set, tenant-scoped — the same consumer call the Account and Contact
    /// forms already make. The criteria editor uses it to OFFER values; the runtime still accepts a hand-typed one, so
    /// an unpublished set degrades to an empty dropdown plus free text rather than blocking authoring. There is no
    /// local fallback list anywhere.
    /// <para>The set code is not free-form: it must be one the attribute catalog actually declares, so this proxy can
    /// never be used to enumerate arbitrary reference data.</para>
    /// </summary>
    [HttpGet("api/reference-values/{setCode}")]
    public async Task<IActionResult> ReferenceValues(string setCode, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;

        var declared = await IsDeclaredReferenceSetAsync(setCode, ct);
        if (!declared)
        {
            return BadRequest(new { errors = new[] { "Unknown reference set for the segment attribute catalog." } });
        }

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { message = "Tenant context is required." });
        }

        var path = $"/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}"
                   + $"/published-values?scope_key={Uri.EscapeDataString(tenantId)}";
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        return await ToProxyResultAsync(response, ct);
    }

    /// <summary>account.parent-account picker, and the manual-membership subject picker for an account segment.
    /// Existing MOD-0149 account list; nothing new is opened.</summary>
    [HttpGet("api/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/accounts{Request.QueryString}", "crm.account.read", ct, ReadFallback);

    /// <summary>The manual-membership subject picker for a contact segment. Existing MOD-0150 contact list — read
    /// only, and the chosen id is still passed through untouched (D-TC: the master is never read or written here).</summary>
    [HttpGet("api/contacts")]
    public Task<IActionResult> Contacts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/contacts{Request.QueryString}", "crm.contact.read", ct, ReadFallback);

    /// <summary>territory.model picker. Existing MOD-0151 model list.</summary>
    [HttpGet("api/territory-models")]
    public Task<IActionResult> TerritoryModels(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/territory-models{Request.QueryString}", "crm.territory.read", ct, ReadFallback);

    /// <summary>territory.node picker. Nodes belong to a model, so the UI cascades model then node — the existing
    /// MOD-0151 surface, not a new flat node endpoint.</summary>
    [HttpGet("api/territory-models/{modelId:guid}/nodes")]
    public Task<IActionResult> TerritoryNodes(Guid modelId, CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/territory-models/{modelId}/nodes{Request.QueryString}", "crm.territory.read", ct, ReadFallback);

    /// <summary>consent.scope-product picker. Existing MDM product list.</summary>
    [HttpGet("api/mdm-products")]
    public Task<IActionResult> MdmProducts(CancellationToken ct) =>
        ProxyGetAsync($"/api/mdm/products{Request.QueryString}", "mdm.products.read", ct);

    /// <summary>consent.scope-brand picker. Existing MDM brand list.</summary>
    [HttpGet("api/mdm-brands")]
    public Task<IActionResult> MdmBrands(CancellationToken ct) =>
        ProxyGetAsync($"/api/mdm/brands{Request.QueryString}", "mdm.brands.read", ct);

    /// <summary>Only a set the attribute catalog itself declares may be read through this proxy. The catalog is the
    /// authority, so the allowlist can never drift away from what the editor legitimately needs.</summary>
    private async Task<bool> IsDeclaredReferenceSetAsync(string setCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(setCode)) return false;

        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/segments/attribute-catalog", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return false;

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("attributes", out var attributes))
            {
                return false;
            }

            foreach (var attribute in attributes.EnumerateArray())
            {
                if (attribute.TryGetProperty("valueSource", out var source)
                    && source.TryGetProperty("referenceSetCode", out var code)
                    && code.ValueKind == JsonValueKind.String
                    && string.Equals(code.GetString(), setCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Segment attribute catalog could not be parsed while checking a reference set.");
        }

        return false;
    }

    // ---------------- helpers ----------------

    private async Task PopulateOptionsAsync(SegmentEditViewModel model, CancellationToken ct)
    {
        model.CanPickGlobalProducts = HasAnyPermission(GlobalProductReadPermission);

        // A picker the actor may not browse is disabled with a reason instead of rendering an always-empty dropdown.
        var pickers = new List<string>();
        if (model.CanPickGlobalProducts) pickers.Add("global-product");
        if (HasAnyPermission("crm.account.read", ReadFallback)) pickers.Add("account");
        if (HasAnyPermission("crm.contact.read", ReadFallback)) pickers.Add("contact");
        if (HasAnyPermission("crm.territory.read", ReadFallback))
        {
            pickers.Add("territory-model");
            pickers.Add("territory-node");
        }
        if (HasAnyPermission("mdm.products.read")) pickers.Add("mdm-product");
        if (HasAnyPermission("mdm.brands.read")) pickers.Add("mdm-brand");
        model.AvailablePickers = pickers;

        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsSegmentDefinition)
        {
            model.ContractError = "SegmentContractUnavailable";
            return;
        }

        model.SegmentTypes = contract.Vocabularies.SegmentTypes;
        model.SubjectTypes = contract.Vocabularies.SubjectTypes;
        model.SegmentStatuses = contract.Vocabularies.SegmentStatuses;
        model.MatchModes = contract.Vocabularies.MatchModes;
        model.MaxCriteriaNodes = contract.Limits.MaxCriteriaNodes;
        model.MaxCriteriaDepth = contract.Limits.MaxCriteriaDepth;
        model.MaxChildrenPerGroup = contract.Limits.MaxChildrenPerGroup;
        model.MaxCandidateSet = contract.Limits.MaxCandidateSet;
    }

    private static string SuggestSegmentCode() =>
        $"seg-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToLowerInvariant()}";

    private async Task<SegmentContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/segments/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<SegmentGatewayResponse<SegmentContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<SegmentDetailViewModel?> LoadSegmentAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/segments/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<SegmentGatewayResponse<SegmentDetailViewModel>>(_json, ct))?.Data;
    }

    private async Task<IActionResult> ProxyGetAsync(
        string path, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct,
        params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        if (body.HasValue && ContainsTenantId(body.Value))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });

        var response = await SendGatewayAsync(method, path, body, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
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
                var json = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, _json);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Segment Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };

        // A bodiless status must stay bodiless: writing a body onto a 204/205/304/1xx makes Kestrel throw
        // ("Content-Length not allowed"), which turns a perfectly good no-content answer into a 500. Archive and
        // activate can legitimately answer 204.
        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent
            or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return [_sharedLocalizer["GatewayError"].Value];
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<SegmentGatewayResponse<object>>(_json, ct);
            if (envelope?.Errors.Count > 0) return envelope.Errors;
        }
        catch
        {
            // Fall through to the raw body below: a non-envelope error is still worth showing.
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private object ToCreatePayload(SegmentEditViewModel m) => new
    {
        m.SegmentCode,
        m.SegmentName,
        m.SegmentType,
        m.SubjectType,
        m.MatchMode,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo,
        m.BusinessUnitId,
        m.Description,
        m.Notes,
        Criteria = ParseCriteria(m.CriteriaJson)
    };

    private object ToUpdatePayload(SegmentEditViewModel m) => new
    {
        m.SegmentName,
        m.SegmentType,
        m.SegmentStatus,
        m.MatchMode,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo,
        m.BusinessUnitId,
        m.Description,
        m.Notes,
        // Frozen criteria are never re-sent: the runtime would answer 409, and the author is pointed at new-version.
        Criteria = m.IsCriteriaFrozen ? null : ParseCriteria(m.CriteriaJson)
    };

    /// <summary>The criteria tree is edited as JSON in the embedded editor. It is parsed (not concatenated) so a
    /// malformed tree fails here rather than reaching the runtime as a broken body.</summary>
    private object? ParseCriteria(string? criteriaJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaJson)) return Array.Empty<object>();
        try
        {
            using var document = JsonDocument.Parse(criteriaJson);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<JsonElement>>(criteriaJson, _json)
                : Array.Empty<object>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Segment criteria payload could not be parsed; sending an empty tree.");
            return Array.Empty<object>();
        }
    }

    private static SegmentEditViewModel ToEditModel(SegmentDetailViewModel s) => new()
    {
        SegmentId = s.SegmentId,
        SegmentCode = s.SegmentCode,
        SegmentName = s.SegmentName,
        SegmentType = s.SegmentType,
        SubjectType = s.SubjectType,
        SegmentStatus = s.SegmentStatus,
        MatchMode = s.MatchMode,
        BusinessUnitId = s.BusinessUnitId,
        Description = s.Description,
        Notes = s.Notes,
        EffectiveFrom = s.EffectiveFrom,
        EffectiveTo = s.EffectiveTo,
        IsArchived = s.IsArchived,
        IsCriteriaFrozen = s.IsCriteriaFrozen,
        SegmentVersion = s.SegmentVersion,
        CriteriaJson = JsonSerializer.Serialize(s.Criteria, new JsonSerializerOptions(JsonSerializerDefaults.Web))
    };

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
