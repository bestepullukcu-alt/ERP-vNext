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
/// to a period is MOD-0155. Every picker is a pass-through to a surface that ALREADY exists — no new endpoint is opened
/// for this page, and no dropdown is ever fed from a hardcoded list.</para>
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
            // A new play lands on Edit so the author can keep binding without a second navigation.
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

    [HttpGet("api/templates/{templateId:guid}")]
    public Task<IActionResult> TemplateGet(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/strategy-templates/{templateId}", ReadPermission, ct, ReadFallback);

    /// <summary>The read-only binding view. It returns no member and no member count.</summary>
    [HttpGet("api/templates/{templateId:guid}/bindings")]
    public Task<IActionResult> TemplateBindings(Guid templateId, CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/strategy-templates/{templateId}/bindings{Request.QueryString}",
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

    private void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private object ToCreatePayload(StrategyTemplateEditViewModel m) => new
    {
        m.TemplateCode,
        m.TemplateName,
        m.SubjectType,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo,
        m.BusinessUnitId,
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
        m.BusinessUnitId,
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
