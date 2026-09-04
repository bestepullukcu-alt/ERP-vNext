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
/// MOD-0162-FU04 KnowledgePath Admin UI (Compact). All business traffic is proxied server-side through Gateway 5000;
/// the browser never sees a service URL or bearer token. The CrmService FU04 runtime remains the authoritative
/// validation/permission layer. There is no delete surface — closing anything is Archive. Steps are the path's embedded
/// sub-resource (D2); the step sub-editor on the Edit page drives them through the /steps proxy routes.
/// </summary>
[Authorize]
[Route("CRM/KnowledgePaths")]
public sealed class KnowledgePathsController : Controller
{
    private const string ReadPermission = "crm.knowledge.path.read";
    private const string ManagePermission = "crm.knowledge.path.manage";
    private const string PublishPermission = "crm.knowledge.path.publish";
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/KnowledgePaths";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<KnowledgePathsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public KnowledgePathsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<KnowledgePathsController> logger)
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
        var model = new KnowledgePathEditViewModel
        {
            EffectiveFrom = DateTimeOffset.Now,
            PathCode = SuggestPathCode(),
            PathVersion = "1.0"
        };
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KnowledgePathEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/knowledge/paths", ToPayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<KnowledgePathGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            // New paths land on Edit so the author can add steps immediately (steps are the path's sub-resource).
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
        var path = await LoadPathAsync(id, ct);
        if (path is null) return NotFound();
        if (path.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedPathReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(path);
        await PopulateOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, KnowledgePathEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.PathId = id;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/knowledge/paths/{id}", ToPayload(model), ct);
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
        var path = await LoadPathAsync(id, ct);
        if (path is null) return NotFound();

        // Resolve the classification ids to display labels (fail-soft; the view falls back to the id when null).
        if (path.SubjectId != Guid.Empty)
            path.SubjectName = await ResolveReferenceLabelAsync($"/api/crm/knowledge/subjects/{path.SubjectId}", ct);
        if (path.TopicId is { } topicId && topicId != Guid.Empty)
            path.TopicName = await ResolveReferenceLabelAsync($"/api/crm/knowledge/topics/{topicId}", ct);
        if (path.AudienceProfileId is { } audienceId && audienceId != Guid.Empty)
            path.AudienceProfileName = await ResolveReferenceLabelAsync($"/api/crm/knowledge/audience-profiles/{audienceId}", ct);

        var model = new KnowledgePathPageViewModel
        {
            Path = path,
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanPublish = HasAnyPermission(PublishPermission, ManagePermission, ManageFallback)
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- Same-origin browser proxy (FU04 allowlist only) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/knowledge/path/contract", ReadPermission, ct, ReadFallback);

    [HttpGet("api/paths")]
    public Task<IActionResult> PathList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/paths{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/paths/{pathId:guid}")]
    public Task<IActionResult> PathGet(Guid pathId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/paths/{pathId}{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/paths/{pathId:guid}/archive")]
    public Task<IActionResult> ArchivePath(Guid pathId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/archive{Request.QueryString}", null, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/publish")]
    public Task<IActionResult> PublishPath(Guid pathId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/publish{Request.QueryString}", null, PublishPermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/new-version")]
    public Task<IActionResult> NewVersion(Guid pathId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/new-version", body, ManagePermission, ct, ManageFallback);

    [HttpGet("api/paths/{pathId:guid}/steps")]
    public Task<IActionResult> StepList(Guid pathId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/paths/{pathId}/steps{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/paths/{pathId:guid}/steps")]
    public Task<IActionResult> AddStep(Guid pathId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/steps", body, ManagePermission, ct, ManageFallback);

    [HttpPut("api/paths/{pathId:guid}/steps/{stepId:guid}")]
    public Task<IActionResult> UpdateStep(Guid pathId, Guid stepId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/paths/{pathId}/steps/{stepId}", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/paths/{pathId:guid}/steps/{stepId:guid}/archive")]
    public Task<IActionResult> ArchiveStep(Guid pathId, Guid stepId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/paths/{pathId}/steps/{stepId}/archive{Request.QueryString}", null, ManagePermission, ct, ManageFallback);

    // Reference lists the step sub-editor / filters consume (read-only; FU02 content + FU03 concept surfaces).
    [HttpGet("api/subjects")]
    public Task<IActionResult> SubjectList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/subjects{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/topics")]
    public Task<IActionResult> TopicList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/topics{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/audience-profiles")]
    public Task<IActionResult> ProfileList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/audience-profiles{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/contents{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/concept-nodes")]
    public Task<IActionResult> ConceptNodeList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-nodes{Request.QueryString}", ReadPermission, ct, ReadFallback);

    // ---------------- helpers ----------------

    private async Task PopulateOptionsAsync(KnowledgePathEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsKnowledgePath)
        {
            model.ContractError = "KnowledgePathContractUnavailable";
            return;
        }

        model.PathStatuses = contract.Vocabularies.PathStatuses;
        model.Sources = contract.Vocabularies.Sources;
        model.StepTypes = contract.Vocabularies.StepTypes;
        model.CompletionRules = contract.Vocabularies.CompletionRules;
        model.VersionPinPolicies = contract.Vocabularies.VersionPinPolicies;

        var subjects = await LoadOptionsAsync("/api/crm/knowledge/subjects?includeArchived=false", ct, idKey: "subjectId");
        var topics = await LoadOptionsAsync("/api/crm/knowledge/topics?includeArchived=false", ct, groupKey: "subjectId", idKey: "topicId");
        var audiences = await LoadOptionsAsync("/api/crm/knowledge/audience-profiles?includeArchived=false", ct, idKey: "audienceProfileId");

        await EnsureSelectedAsync(subjects, model.SubjectId, "/api/crm/knowledge/subjects/{0}", ct);
        await EnsureSelectedAsync(topics, model.TopicId, "/api/crm/knowledge/topics/{0}", ct, groupKey: "subjectId");
        await EnsureSelectedAsync(audiences, model.AudienceProfileId, "/api/crm/knowledge/audience-profiles/{0}", ct);

        model.SubjectOptions = subjects;
        model.TopicOptions = topics;
        model.AudienceProfileOptions = audiences;
    }

    private static string SuggestPathCode() =>
        $"KP-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private async Task<List<KnowledgePathOptionViewModel>> LoadOptionsAsync(
        string path, CancellationToken ct, string? groupKey = null, string? idKey = null)
    {
        var options = new List<KnowledgePathOptionViewModel>();
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
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
                if (GetBool(el, "isArchived")) continue;

                var id = (idKey is not null ? GetFirstString(el, idKey) : null)
                    ?? GetFirstString(el, "id", "subjectId", "topicId", "audienceProfileId");
                if (string.IsNullOrWhiteSpace(id)) continue;

                var name = GetFirstString(el, "name", "subjectName", "topicName", "profileName");
                var code = GetFirstString(el, "code", "subjectCode", "topicCode", "profileCode");
                var label = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) ? $"{code} — {name}"
                    : !string.IsNullOrWhiteSpace(name) ? name!
                    : !string.IsNullOrWhiteSpace(code) ? code!
                    : id!;
                var group = groupKey is not null ? GetFirstString(el, groupKey) ?? string.Empty : string.Empty;
                options.Add(new KnowledgePathOptionViewModel { Value = id!, Label = label, Group = group });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KnowledgePath reference option load failed: {Path}", path);
        }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task EnsureSelectedAsync(
        List<KnowledgePathOptionViewModel> options, Guid? currentId, string byIdPathFormat, CancellationToken ct,
        string? groupKey = null)
    {
        if (currentId is null || currentId == Guid.Empty) return;
        var idStr = currentId.Value.ToString();
        if (options.Any(o => string.Equals(o.Value, idStr, StringComparison.OrdinalIgnoreCase))) return;

        var option = new KnowledgePathOptionViewModel { Value = idStr, Label = idStr, IsInactive = true };
        var response = await SendGatewayAsync(HttpMethod.Get, string.Format(byIdPathFormat, idStr), null, ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    var name = GetFirstString(data, "name", "subjectName", "topicName", "profileName");
                    var code = GetFirstString(data, "code", "subjectCode", "topicCode", "profileCode");
                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) option.Label = $"{code} — {name}";
                    else if (!string.IsNullOrWhiteSpace(name)) option.Label = name!;
                    else if (!string.IsNullOrWhiteSpace(code)) option.Label = code!;
                    if (groupKey is not null) option.Group = GetFirstString(data, groupKey) ?? string.Empty;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "KnowledgePath ensure-selected parse failed: {Path}", byIdPathFormat); }
        }
        options.Insert(0, option);
    }

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

    private static bool GetBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private async Task<KnowledgePathContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/knowledge/path/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<KnowledgePathGatewayResponse<KnowledgePathContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<KnowledgePathDetailViewModel?> LoadPathAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/knowledge/paths/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<KnowledgePathGatewayResponse<KnowledgePathDetailViewModel>>(_json, ct))?.Data;
    }

    // Resolve a single reference (subject/topic/audience-profile) id to a "code — name" label. Fail-soft: any error
    // returns null so the Details page falls back to rendering the raw id. Mirrors KnowledgeController.ResolveReferenceLabelAsync.
    private async Task<string?> ResolveReferenceLabelAsync(string path, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
            var name = GetFirstString(data, "name", "subjectName", "topicName", "profileName", "audienceProfileName");
            var code = GetFirstString(data, "code", "subjectCode", "topicCode", "profileCode");
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) return $"{code} — {name}";
            return name ?? code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KnowledgePath reference label resolve failed: {Path}", path);
            return null;
        }
    }

    private async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        if (body.HasValue && ContainsTenantId(body.Value))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });

        var response = await SendGatewayAsync(method, path, body, ct);
        return await ToProxyResultAsync(response, ct);
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
                var json = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, _json);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KnowledgePath Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content.ReadFromJsonAsync<KnowledgePathGatewayResponse<object>>(_json, ct);
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

    private static object ToPayload(KnowledgePathEditViewModel m) => new
    {
        m.PathCode,
        m.PathName,
        m.Description,
        m.SubjectId,
        m.TopicId,
        m.AudienceProfileId,
        m.Objective,
        m.LanguageCode,
        m.PathVersion,
        m.PathStatus,
        m.Source,
        EffectiveFrom = m.EffectiveFrom ?? DateTimeOffset.Now,
        m.EffectiveTo
    };

    private static KnowledgePathEditViewModel ToEditModel(KnowledgePathDetailViewModel p) => new()
    {
        PathId = p.PathId,
        PathCode = p.PathCode,
        PathName = p.PathName,
        Description = p.Description,
        SubjectId = p.SubjectId,
        TopicId = p.TopicId,
        AudienceProfileId = p.AudienceProfileId,
        Objective = p.Objective,
        LanguageCode = p.LanguageCode,
        PathVersion = p.PathVersion,
        PathStatus = p.PathStatus,
        Source = p.Source,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo,
        IsArchived = p.IsArchived,
        IsStepSetFrozen = p.IsStepSetFrozen
    };

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
