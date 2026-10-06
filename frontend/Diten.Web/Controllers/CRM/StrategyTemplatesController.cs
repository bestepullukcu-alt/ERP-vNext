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
/// MOD-0167-FU04 Strategy Template Admin UI (Compact). All business traffic is proxied server-side through Gateway
/// 5000; the browser never sees a service URL or a bearer token. The CrmService runtime stays the authoritative
/// validation and permission layer — nothing is decided here.
/// <para>There is no delete surface (closing a play is Archive) and no apply/generate surface at all: applying a play
/// to a period is MOD-0155. Every picker is a pass-through to a surface that ALREADY exists — no new CRM endpoint is
/// opened for this page, and no dropdown is ever fed from a hardcoded list. The one composed read
/// (<c>api/line-journeys</c>, WP-SB-3-UIa) joins two existing CRM lists as an option filter only.</para>
/// </summary>
[Authorize]
[Route("CRM/StrategyTemplates")]
public sealed class StrategyTemplatesController : Controller
{
    private const string ReadPermission = "crm.strategy-template.read";
    private const string ManagePermission = "crm.strategy-template.manage";
    private const string ActivatePermission = "crm.strategy-template.activate";
    private const string SegmentReadPermission = "crm.segment.read";
    private const string FrequencyReadPermission = "crm.visit-frequency-policy.read";
    private const string KnowledgePathReadPermission = "crm.knowledge.path.read";
    private const string JourneyReadPermission = "crm.knowledge.content-engagement-journey.read";
    private const string GlobalProductReadPermission = "mdm.global-products.read";

    /// <summary>The MDM gsku selector is guarded by a CREATE key on the MDM side. That is wrong for a read-only picker
    /// but it is MDM's decision and cannot be changed from here, so the SKU picker is simply disabled when the actor
    /// lacks it (follow-up F-GSKU-PICKER-PERM).</summary>
    private const string GskuSelectorPermission = "mdm.finished-goods.create";

    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/StrategyTemplates";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<StrategyTemplatesController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public StrategyTemplatesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<StrategyTemplatesController> logger)
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
        var model = new StrategyTemplateEditViewModel
        {
            EffectiveFrom = DateTimeOffset.Now,
            TemplateCode = SuggestTemplateCode()
        };
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StrategyTemplateEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Post, "/api/crm/strategy-templates", ToCreatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<StrategyTemplateGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            if (envelope?.Data is { } id && id != Guid.Empty)
            {
                // WP-ST-EDIT-W — one-click "save + activate". Activate is a SEPARATE operation over the EXISTING endpoint;
                // it runs ONLY after the save succeeded and ONLY when the actor holds the activate permission. A failed
                // activate never rolls back the save — the play stays created and the author lands on Edit with a notice.
                if (model.ActivateAfterSave && HasAnyPermission(ActivatePermission))
                    return await ActivateAfterSaveAsync(id, nameof(Edit), ct);
                // A new play lands on Edit so the author can keep binding without a second navigation.
                return RedirectToAction(nameof(Edit), new { id });
            }
            return RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(model, await ExtractErrorsAsync(response, ct));
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var template = await LoadTemplateAsync(id, ct);
        if (template is null) return NotFound();
        if (template.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedTemplateReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(template);
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StrategyTemplateEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.TemplateId = id;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/strategy-templates/{id}", ToUpdatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            // WP-ST-EDIT-W — same one-click "save + activate" orchestration as Create. On a failed activate the update is
            // still saved; both outcomes land on Details, the failure adding a "saved, not activated" warning.
            if (model.ActivateAfterSave && HasAnyPermission(ActivatePermission))
                return await ActivateAfterSaveAsync(id, nameof(Details), ct);
            return RedirectToAction(nameof(Details), new { id });
        }

        AddGatewayErrors(model, await ExtractErrorsAsync(response, ct));
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var template = await LoadTemplateAsync(id, ct);
        if (template is null) return NotFound();

        var model = new StrategyTemplatePageViewModel
        {
            Template = template,
            Bindings = await LoadBindingsAsync(id, ct),
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback)
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- JSON proxies (same-origin; the browser never calls 5061) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/strategy-templates/contract", ReadPermission, ct, ReadFallback);

    [HttpGet("api/templates")]
    public Task<IActionResult> TemplateList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/strategy-templates{Request.QueryString}", ReadPermission, ct, ReadFallback);

    /// <summary>WP-ST-SCOPE scope selector feed. The list console consumes it read-only for the KAPSAM label map
    /// (country / legal-entity / business-unit → display name) and the country filter options.</summary>
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/strategy-templates/scope-options{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/templates/{templateId:guid}")]
    public Task<IActionResult> TemplateGet(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/strategy-templates/{templateId}", ReadPermission, ct, ReadFallback);

    /// <summary>The read-only binding view. It returns no member and no member count.</summary>
    [HttpGet("api/templates/{templateId:guid}/bindings")]
    public Task<IActionResult> TemplateBindings(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/strategy-templates/{templateId}/bindings{Request.QueryString}",
            ReadPermission, ct, ReadFallback);

    /// <summary>WP-ST-DETAIL-2 — the Detay "Sürüm geçmişi" panel feed: every version of the play's lineage
    /// (newest first). A pass-through to the DETAIL-1 read; the gateway <c>/{everything}</c> route covers it.</summary>
    [HttpGet("api/templates/{templateId:guid}/versions")]
    public Task<IActionResult> TemplateVersions(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/strategy-templates/{templateId}/versions{Request.QueryString}",
            ReadPermission, ct, ReadFallback);

    [HttpPost("api/templates/{templateId:guid}/activate")]
    public Task<IActionResult> Activate(Guid templateId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/strategy-templates/{templateId}/activate{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/templates/{templateId:guid}/archive")]
    public Task<IActionResult> Archive(Guid templateId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/strategy-templates/{templateId}/archive{Request.QueryString}", null,
            ManagePermission, ct, ManageFallback);

    [HttpPost("api/templates/{templateId:guid}/new-version")]
    public Task<IActionResult> NewVersion(Guid templateId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post, $"/api/crm/strategy-templates/{templateId}/new-version", null,
            ManagePermission, ct, ManageFallback);

    // ---------------- value pickers (all pass-throughs to surfaces that ALREADY exist) ----------------

    /// <summary>The "who" picker: MOD-0167 FU02 segments. Reading the segment LIST never exposes a member.</summary>
    [HttpGet("api/segments")]
    public Task<IActionResult> Segments(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/segments{Request.QueryString}", SegmentReadPermission, ct, ReadFallback);

    /// <summary>The "how often" picker: MOD-0165 policies, read-only. This page never writes one.</summary>
    [HttpGet("api/visit-frequency-policies")]
    public Task<IActionResult> FrequencyPolicies(CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/visit-frequency-policies{Request.QueryString}", FrequencyReadPermission, ct, ReadFallback);

    /// <summary>The "which story" pickers: MOD-0162 paths and journeys, read-only.</summary>
    [HttpGet("api/knowledge-paths")]
    public Task<IActionResult> KnowledgePaths(CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/knowledge/paths{Request.QueryString}", KnowledgePathReadPermission, ct, ReadFallback);

    [HttpGet("api/content-engagement-journeys")]
    public Task<IActionResult> Journeys(CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/knowledge/content-engagement-journeys{Request.QueryString}",
            JourneyReadPermission, ct, ReadFallback);

    /// <summary>
    /// WP-SB-3-UIa — the journey picker of ONE product line: the tenant's PUBLISHED, non-archived engagement journeys
    /// whose subject's primary <c>global-product</c> link is <paramref name="productId"/>. Composed from two EXISTING CRM
    /// reads (journey list + subject list, both tenant-scoped by the JWT tenant header) — no new CRM endpoint.
    /// <para>This is an OPTION FILTER only. The decision stays in CRM (409 <c>journey_not_published</c> /
    /// <c>journey_product_mismatch</c>), so a stale option can never be saved. The product is resolved exactly like
    /// <c>ChainContextResolver.PrimaryGlobalProduct</c>: SourceSystem <c>global-product</c>, IsPrimary, a Guid id.</para>
    /// <para>When the subject list cannot be read (e.g. the author lacks <c>crm.knowledge.subject.read</c>) the published
    /// journeys come back UNFILTERED with <c>productFilterApplied: false</c>, so the form can say the match is checked at
    /// save — never a silently wrong "no journey".</para>
    /// </summary>
    [HttpGet("api/line-journeys")]
    public async Task<IActionResult> LineJourneys([FromQuery] Guid? productId, CancellationToken ct)
    {
        if (RequireJson(JourneyReadPermission, ReadFallback) is { } denied) return denied;
        if (productId is not { } product || product == Guid.Empty)
            return Ok(new { data = new { items = Array.Empty<StrategyTemplateLineJourneyOption>(), productFilterApplied = true } });

        var journeysResponse = await SendGatewayAsync(
            HttpMethod.Get, "/api/crm/knowledge/content-engagement-journeys?status=published&includeArchived=false", null, ct);
        if (journeysResponse is null || !journeysResponse.IsSuccessStatusCode)
            return await ToProxyResultAsync(journeysResponse, ct);
        var journeys = (await journeysResponse.Content
            .ReadFromJsonAsync<StrategyTemplateGatewayResponse<StrategyTemplateItemsApiModel<StrategyTemplateJourneyApiModel>>>(_json, ct))
            ?.Data?.Items ?? new();

        var subjectsResponse = await SendGatewayAsync(
            HttpMethod.Get, "/api/crm/knowledge/subjects?includeArchived=false", null, ct);
        Dictionary<Guid, Guid>? productBySubject = null;
        if (subjectsResponse is not null && subjectsResponse.IsSuccessStatusCode)
        {
            var subjects = (await subjectsResponse.Content
                .ReadFromJsonAsync<StrategyTemplateGatewayResponse<StrategyTemplateItemsApiModel<StrategyTemplateSubjectApiModel>>>(_json, ct))
                ?.Data?.Items ?? new();
            productBySubject = subjects
                .Select(s => (s.SubjectId, Product: PrimaryGlobalProduct(s)))
                .Where(x => x.Product is not null)
                .ToDictionary(x => x.SubjectId, x => x.Product!.Value);
        }

        var items = journeys
            .Where(j => !j.IsArchived && string.Equals(j.JourneyStatus, "published", StringComparison.OrdinalIgnoreCase))
            .Where(j => productBySubject is null
                        || (productBySubject.TryGetValue(j.SubjectId, out var p) && p == product))
            .OrderBy(j => j.JourneyCode, StringComparer.OrdinalIgnoreCase)
            .Select(j => new StrategyTemplateLineJourneyOption
            {
                JourneyId = j.JourneyId,
                JourneyCode = j.JourneyCode,
                JourneyName = j.JourneyName,
                LanguageCode = j.LanguageCode,
                JourneyVersion = j.JourneyVersion
            })
            .ToList();
        return Ok(new { data = new { items, productFilterApplied = productBySubject is not null } });
    }

    /// <summary>The subject's primary MDM Global Product link — the same rule as CRM's
    /// <c>ChainContextResolver.PrimaryGlobalProduct</c> (SourceSystem <c>global-product</c>, IsPrimary, a Guid id).</summary>
    private static Guid? PrimaryGlobalProduct(StrategyTemplateSubjectApiModel subject)
    {
        foreach (var reference in subject.ExternalReferences)
        {
            if (reference.IsPrimary
                && string.Equals(reference.SourceSystem?.Trim(), "global-product", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(reference.ExternalId, out var id) && id != Guid.Empty)
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>The product picker. Re-uses the EXISTING MDM global-product selector — the same surface the MOD-0167
    /// FU02 criteria editor and the MOD-0162 FU03 concept picker use. No new endpoint is opened here.</summary>
    [HttpGet("api/global-products")]
    public Task<IActionResult> GlobalProducts(CancellationToken ct) =>
        ProxyGetAsync($"/api/global-products/selector{Request.QueryString}", GlobalProductReadPermission, ct);

    /// <summary>The SKU picker. Re-uses the EXISTING MDM gsku selector. Note the permission: MDM guards this read-only
    /// selector with a CREATE key, which is why the picker is disabled rather than empty when the actor lacks it
    /// (F-GSKU-PICKER-PERM).</summary>
    [HttpGet("api/gskus")]
    public Task<IActionResult> Gskus(CancellationToken ct) =>
        ProxyGetAsync($"/api/finished-goods/gsku-selector{Request.QueryString}", GskuSelectorPermission, ct);

    // ---------------- helpers ----------------

    private async Task PopulateOptionsAsync(StrategyTemplateEditViewModel model, CancellationToken ct)
    {
        model.CanPickGlobalProducts = HasAnyPermission(GlobalProductReadPermission);
        model.CanPickGskus = HasAnyPermission(GskuSelectorPermission);

        // A picker the actor may not browse is disabled with a reason instead of rendering an always-empty dropdown,
        // and it never degrades into a free-text GUID field.
        var pickers = new List<string>();
        if (HasAnyPermission(SegmentReadPermission, ReadFallback)) pickers.Add("segment");
        if (HasAnyPermission(FrequencyReadPermission, ReadFallback)) pickers.Add("frequency-policy");
        if (HasAnyPermission(KnowledgePathReadPermission, ReadFallback)) pickers.Add("knowledge-path");
        if (HasAnyPermission(JourneyReadPermission, ReadFallback)) pickers.Add("content-engagement-journey");
        if (model.CanPickGlobalProducts) pickers.Add("global-product");
        if (model.CanPickGskus) pickers.Add("gsku");
        model.AvailablePickers = pickers;

        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsStrategyTemplateDefinition)
        {
            model.ContractError = "StrategyTemplateContractUnavailable";
            return;
        }

        model.SubjectTypes = contract.Vocabularies.SubjectTypes;
        model.TemplateStatuses = contract.Vocabularies.TemplateStatuses;
        model.BindingRoles = contract.Vocabularies.SegmentBindingRoles;
        model.FrequencyIntentModes = contract.Vocabularies.FrequencyIntentModes;
        model.SkuAllocationModes = contract.Vocabularies.SkuAllocationModes;
        model.ContentRefTypes = contract.Vocabularies.ContentRefTypes;
        // Published from MOD-0165's own constants, so the editor offers exactly what the runtime accepts.
        model.FrequencyTypes = contract.Vocabularies.FrequencyTypes;
        model.FrequencyPeriodTypes = contract.Vocabularies.FrequencyPeriodTypes;
        model.MaxSegmentBindings = contract.Limits.MaxSegmentBindings;
        model.MaxProductLines = contract.Limits.MaxProductLines;
        model.MaxSkuAllocationsPerLine = contract.Limits.MaxSkuAllocationsPerLine;
        model.MaxContentBindings = contract.Limits.MaxContentBindings;
        model.RequiredAllocationTotal = contract.Limits.RequiredAllocationTotal;
    }

    private static string SuggestTemplateCode() =>
        $"play-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToLowerInvariant()}";

    private async Task<StrategyTemplateContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/strategy-templates/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<StrategyTemplateGatewayResponse<StrategyTemplateContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<StrategyTemplateDetailViewModel?> LoadTemplateAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/strategy-templates/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<StrategyTemplateGatewayResponse<StrategyTemplateDetailViewModel>>(_json, ct))?.Data;
    }

    private async Task<StrategyTemplateBindingsViewModel?> LoadBindingsAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/strategy-templates/{id}/bindings", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<StrategyTemplateGatewayResponse<StrategyTemplateBindingsViewModel>>(_json, ct))?.Data;
    }

    /// <summary>WP-ST-EDIT-W — the "save + activate" tail: calls the EXISTING activate endpoint over Gateway for a play
    /// that was just saved. Activate is a SEPARATE operation and its failure NEVER undoes the save — on success the actor
    /// lands on Details with a "record activated" notice; on any non-success (403 / 409-already-active / unreachable) the
    /// save stands and the actor is told it was saved but not activated, landing on <paramref name="failureAction"/>.
    /// The caller has already verified the actor holds the activate permission.</summary>
    private async Task<IActionResult> ActivateAfterSaveAsync(Guid id, string failureAction, CancellationToken ct)
    {
        var activate = await SendGatewayAsync(
            HttpMethod.Post, $"/api/crm/strategy-templates/{id}/activate", null, ct);
        if (activate is not null && activate.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordActivated"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        // The save already succeeded; keep its success toast and add a warning about the activation.
        TempData["WarningMessage"] = _sharedLocalizer["SavedNotActivated"].Value;
        return RedirectToAction(failureAction, new { id });
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
            _logger.LogError(ex, "Strategy template Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content
                .ReadFromJsonAsync<StrategyTemplateGatewayResponse<object>>(_json, ct);
            if (envelope?.Errors.Count > 0) return envelope.Errors;
        }
        catch
        {
            // Fall through to the raw body below: a non-envelope error is still worth showing.
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    /// <summary>WP-SB-3-UIa — the SB-3a refusals (<see cref="StrategyTemplateErrorMap"/>) are anchored to the place they
    /// concern and shown there, localised, by form.js; anything else is kept in the summary with the runtime's own text.
    /// The CRM envelope carries a refusal as <c>[code, message]</c>; the message is used only to find the line (it names
    /// the line's product or journey) and is then dropped, so a raw code never reaches the screen.</summary>
    private void AddGatewayErrors(StrategyTemplateEditViewModel model, IReadOnlyList<string> errors)
    {
        var anchored = MapFormErrors(errors, model.ProductLinesJson, out var rest);
        model.FormErrorsJson = anchored.Count > 0 ? JsonSerializer.Serialize(anchored, _json) : null;
        foreach (var error in rest) ModelState.AddModelError(string.Empty, error);
    }

    internal static List<StrategyTemplateFormError> MapFormErrors(
        IReadOnlyList<string> errors, string? productLinesJson, out List<string> unmapped)
    {
        var anchored = new List<StrategyTemplateFormError>();
        unmapped = new List<string>();
        var lines = ReadPostedLines(productLinesJson);
        for (var i = 0; i < errors.Count; i++)
        {
            var code = errors[i]?.Trim() ?? string.Empty;
            if (!StrategyTemplateErrorMap.Scopes.TryGetValue(code, out var scope))
            {
                unmapped.Add(errors[i]);
                continue;
            }

            // The message that travels with the code (CRM: code first, message second) names the line.
            var message = i + 1 < errors.Count && !StrategyTemplateErrorMap.Scopes.ContainsKey(errors[i + 1]?.Trim() ?? "")
                ? errors[++i]
                : string.Empty;
            anchored.Add(new StrategyTemplateFormError
            {
                Code = code,
                Key = StrategyTemplateErrorMap.KeyFor(code),
                Scope = scope,
                LineIndex = scope == StrategyTemplateErrorMap.ScopeLine ? FindLine(lines, message) : null
            });
        }

        return anchored;
    }

    /// <summary>The posted line a refusal message names: by product code / product id, else by journey id / code. Null
    /// when the message names none (or names several) — the refusal is then shown at the head of the section.</summary>
    private static int? FindLine(IReadOnlyList<(string? ProductCode, string ProductId, string? JourneyId, string? JourneyCode)> lines, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || lines.Count == 0) return null;
        bool Names(string? token) => !string.IsNullOrWhiteSpace(token)
            && message.Contains($"'{token}'", StringComparison.OrdinalIgnoreCase);

        var byProduct = Enumerable.Range(0, lines.Count)
            .Where(i => Names(lines[i].ProductCode) || Names(lines[i].ProductId)).ToList();
        if (byProduct.Count == 1) return byProduct[0];

        var byJourney = Enumerable.Range(0, lines.Count)
            .Where(i => Names(lines[i].JourneyId) || Names(lines[i].JourneyCode)).ToList();
        return byJourney.Count == 1 ? byJourney[0] : null;
    }

    private static List<(string? ProductCode, string ProductId, string? JourneyId, string? JourneyCode)> ReadPostedLines(string? json)
    {
        var result = new List<(string?, string, string?, string?)>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var line in document.RootElement.EnumerateArray())
            {
                if (line.ValueKind != JsonValueKind.Object) continue;
                result.Add((Text(line, "globalProductCodeDisplay"), Text(line, "globalProductId") ?? string.Empty,
                    Text(line, "journeyId"), Text(line, "journeyCode")));
            }
        }
        catch (JsonException)
        {
            // An unreadable list anchors nothing; the refusal still shows at the head of the section.
        }

        return result;

        static string? Text(JsonElement element, string name)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }
    }

    private object ToCreatePayload(StrategyTemplateEditViewModel m) => new
    {
        m.TemplateCode,
        m.TemplateName,
        m.SubjectType,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo,
        // WP-ST-EDIT-A — the play's address. Only the reference of the SELECTED level is carried (single-reference); the
        // UI clears the others, and the runtime refuses a second reference regardless.
        m.BusinessUnitId,
        m.ScopeType,
        m.CountryScope,
        m.LegalEntityId,
        m.Description,
        m.Notes,
        SegmentBindings = ParseArray(m.SegmentBindingsJson, nameof(m.SegmentBindingsJson)),
        FrequencyIntent = ParseObject(m.FrequencyIntentJson),
        ProductLines = ParseArray(m.ProductLinesJson, nameof(m.ProductLinesJson)),
        ContentBindings = ParseArray(m.ContentBindingsJson, nameof(m.ContentBindingsJson))
    };

    private object ToUpdatePayload(StrategyTemplateEditViewModel m) => new
    {
        m.TemplateName,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo,
        // WP-ST-EDIT-A — scope is editable metadata, correctable even on a frozen version (unlike the binding lists).
        m.BusinessUnitId,
        m.ScopeType,
        m.CountryScope,
        m.LegalEntityId,
        m.Description,
        m.Notes,
        // Frozen bindings are never re-sent: the runtime would answer 409, and the author is pointed at new-version.
        // Sending null means "leave this binding alone", which is exactly what a metadata edit needs.
        SegmentBindings = m.AreBindingsFrozen ? null : ParseArray(m.SegmentBindingsJson, nameof(m.SegmentBindingsJson)),
        FrequencyIntent = m.AreBindingsFrozen ? null : ParseObject(m.FrequencyIntentJson),
        ProductLines = m.AreBindingsFrozen ? null : ParseArray(m.ProductLinesJson, nameof(m.ProductLinesJson)),
        ContentBindings = m.AreBindingsFrozen ? null : ParseArray(m.ContentBindingsJson, nameof(m.ContentBindingsJson))
    };

    /// <summary>The repeaters post JSON. It is parsed (not concatenated) so a malformed list fails here rather than
    /// reaching the runtime as a broken body.</summary>
    private object? ParseArray(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<JsonElement>>(json, _json)
                : Array.Empty<object>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Strategy template {Field} could not be parsed; sending an empty list.", field);
            return Array.Empty<object>();
        }
    }

    private object? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<JsonElement>(json, _json)
                : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Strategy template frequency intent could not be parsed; sending none.");
            return null;
        }
    }

    private static StrategyTemplateEditViewModel ToEditModel(StrategyTemplateDetailViewModel t)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return new StrategyTemplateEditViewModel
        {
            TemplateId = t.TemplateId,
            TemplateCode = t.TemplateCode,
            TemplateName = t.TemplateName,
            SubjectType = t.SubjectType,
            TemplateStatus = t.TemplateStatus,
            BusinessUnitId = t.BusinessUnitId,
            // WP-ST-EDIT-A — seed from the EFFECTIVE scope so a pre-scope play opens on the address it always had
            // (a business unit derives business-unit, nothing derives tenant) rather than on an empty selector.
            ScopeType = string.IsNullOrWhiteSpace(t.EffectiveScopeType) ? t.ScopeType : t.EffectiveScopeType,
            CountryScope = t.CountryScope,
            LegalEntityId = t.LegalEntityId,
            Description = t.Description,
            Notes = t.Notes,
            EffectiveFrom = t.EffectiveFrom,
            EffectiveTo = t.EffectiveTo,
            IsArchived = t.IsArchived,
            AreBindingsFrozen = t.AreBindingsFrozen,
            TemplateVersion = t.TemplateVersion,
            SegmentBindingsJson = JsonSerializer.Serialize(t.SegmentBindings, web),
            FrequencyIntentJson = JsonSerializer.Serialize(t.FrequencyIntent, web),
            ProductLinesJson = JsonSerializer.Serialize(t.ProductLines, web),
            ContentBindingsJson = JsonSerializer.Serialize(t.ContentBindings, web)
        };
    }

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
