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
/// MOD-0162-FU03 Concept Graph Admin UI. Proxy-only: all business traffic is proxied server-side through Gateway 5000;
/// the browser never sees a service URL or bearer token, and no business rule lives here (the CrmService FU03 runtime is
/// the authoritative validation + permission layer). No delete surface — closing anything is Archive. Hybrid golden
/// reference: ConceptNode is the Compact page-set (Create/Edit/Details); ConceptType / ConceptRelationship /
/// ConceptChainTemplate are Slim tabs (offcanvas); Graph Preview is read-only. The Global Product picker consumes the
/// MDM selector read-only; on 404/403 the picker is disabled with a reason (never a silent empty list).
/// </summary>
[Authorize]
[Route("CRM/KnowledgeConcepts")]
public sealed class KnowledgeConceptsController : Controller
{
    private const string ReadPermission = "crm.knowledge.concept.read";
    private const string ManagePermission = "crm.knowledge.concept.manage";
    private const string TemplateManagePermission = "crm.knowledge.concept-template.manage";
    private const string LinkManagePermission = "crm.knowledge.concept-link.manage";
    private const string ReadFallback = "crm.territory.read";           // DEV-ONLY fallback until MOD-0162-FU03-RBAC
    private const string ManageFallback = "crm.territory.model.manage";  // DEV-ONLY fallback until MOD-0162-FU03-RBAC
    private const string GlobalProductReadPermission = "mdm.global-products.read";
    private const string ViewRoot = "~/Views/CRM/KnowledgeConcepts";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<KnowledgeConceptsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public KnowledgeConceptsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<KnowledgeConceptsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Pages (tabbed console + ConceptNode Compact page-set) ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission, ReadFallback) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var model = new ConceptNodeEditViewModel { EffectiveFrom = DateTimeOffset.Now, ConceptNodeCode = SuggestNodeCode() };
        await PopulateNodeOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConceptNodeEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateNodeOptionsAsync(model, ct);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/knowledge/concept-nodes", ToNodePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ConceptGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateNodeOptionsAsync(model, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var node = await LoadNodeAsync(id, ct);
        if (node is null) return NotFound();
        if (node.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedNodeReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(node);
        await PopulateNodeOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ConceptNodeEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.ConceptNodeId = id;
        if (!ModelState.IsValid)
        {
            await PopulateNodeOptionsAsync(model, ct);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/knowledge/concept-nodes/{id}", ToNodePayload(model, forUpdate: true), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateNodeOptionsAsync(model, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var node = await LoadNodeAsync(id, ct);
        if (node is null) return NotFound();
        var model = new ConceptNodePageViewModel { Node = node, CanManage = HasAnyPermission(ManagePermission, ManageFallback) };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- Same-origin browser proxy (FU03 allowlist only) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/knowledge/concept-graph/contract", ReadPermission, ct, ReadFallback);

    // Knowledge subjects — read-only FU02 reference. Every Slim tab is subject-scoped (the subject filter chip, the
    // type/connection/template forms), so the browser needs this list through the same-origin proxy. No FU02 write
    // path is exposed here: only the list read is allowlisted.
    [HttpGet("api/subjects")]
    public Task<IActionResult> SubjectList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/subjects{Request.QueryString}", ReadPermission, ct, ReadFallback);

    // Concept types (Slim tab).
    [HttpGet("api/concept-types")]
    public Task<IActionResult> TypeList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-types{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/concept-types")]
    public Task<IActionResult> CreateType([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/concept-types", body, ManagePermission, ct, ManageFallback);

    [HttpPut("api/concept-types/{typeId:guid}")]
    public Task<IActionResult> UpdateType(Guid typeId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/concept-types/{typeId}", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/concept-types/{typeId:guid}/archive")]
    public Task<IActionResult> ArchiveType(Guid typeId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/concept-types/{typeId}/archive", null, ManagePermission, ct, ManageFallback);

    // Concept nodes (Compact primary surface — list + archive proxied; create/edit are server-side pages).
    [HttpGet("api/concept-nodes")]
    public Task<IActionResult> NodeList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-nodes{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/concept-nodes/{nodeId:guid}/archive")]
    public Task<IActionResult> ArchiveNode(Guid nodeId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/concept-nodes/{nodeId}/archive", null, ManagePermission, ct, ManageFallback);

    // Concept relationships (Slim tab).
    [HttpGet("api/concept-relationships")]
    public Task<IActionResult> RelationshipList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-relationships{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/concept-relationships")]
    public Task<IActionResult> CreateRelationship([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/concept-relationships", body, ManagePermission, ct, ManageFallback);

    [HttpPut("api/concept-relationships/{relationshipId:guid}")]
    public Task<IActionResult> UpdateRelationship(Guid relationshipId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/concept-relationships/{relationshipId}", body, ManagePermission, ct, ManageFallback);

    [HttpPost("api/concept-relationships/{relationshipId:guid}/archive")]
    public Task<IActionResult> ArchiveRelationship(Guid relationshipId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/concept-relationships/{relationshipId}/archive", null, ManagePermission, ct, ManageFallback);

    // Concept chain templates (Slim tab).
    [HttpGet("api/concept-chain-templates")]
    public Task<IActionResult> TemplateList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-chain-templates{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/concept-chain-templates")]
    public Task<IActionResult> CreateTemplate([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/concept-chain-templates", body, TemplateManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/concept-chain-templates/{templateId:guid}")]
    public Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/concept-chain-templates/{templateId}", body, TemplateManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/concept-chain-templates/{templateId:guid}/archive")]
    public Task<IActionResult> ArchiveTemplate(Guid templateId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/concept-chain-templates/{templateId}/archive", null, TemplateManagePermission, ct, ManagePermission, ManageFallback);

    // Read-only graph views (Graph Preview tab). These are ADJACENCY reads with a FIXED depth: by-node is exactly one
    // hop, by-content exactly two edge layers. There is no depth/maxHops parameter to forward — traversal, best-path,
    // scoring and recommendation are an engine (F4 / MOD-0058) and are outside FU03.
    [HttpGet("api/concept-graph")]
    public Task<IActionResult> Graph(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-graph{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/concept-graph/by-node/{nodeId:guid}")]
    public Task<IActionResult> GraphByNode(Guid nodeId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-graph/by-node/{nodeId}{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpGet("api/concept-graph/by-content/{contentId:guid}")]
    public Task<IActionResult> GraphByContent(Guid contentId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/concept-graph/by-content/{contentId}{Request.QueryString}", ReadPermission, ct, ReadFallback);

    // Knowledge contents — read-only FU02 reference, the source of the Graph Preview "by content" picker.
    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/contents{Request.QueryString}", ReadPermission, ct, ReadFallback);

    // Content ↔ concept links.
    [HttpGet("api/content-concept-links")]
    public Task<IActionResult> LinkList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/content-concept-links{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/content-concept-links")]
    public Task<IActionResult> CreateLink([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/content-concept-links", body, LinkManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/content-concept-links/{linkId:guid}/archive")]
    public Task<IActionResult> ArchiveLink(Guid linkId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/content-concept-links/{linkId}/archive", null, LinkManagePermission, ct, ManagePermission, ManageFallback);

    // MDM Global Product selector — the ExternalRefType=global-product picker source (read-only, MDM-owned permission).
    // Returns [{ value, label }] or a { disabled, reason } marker so the picker never shows a silent empty list.
    [HttpGet("api/global-product-options")]
    public async Task<IActionResult> GlobalProductOptions(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/global-products/selector{Request.QueryString}", null, ct);
        if (response is null)
            return Json(new { disabled = true, reason = "GlobalProductPickerUnavailable" });
        if ((int)response.StatusCode == 404)
            return Json(new { disabled = true, reason = "GlobalProductEndpointMissing" });
        if ((int)response.StatusCode == 403)
            return Json(new { disabled = true, reason = "GlobalProductPermissionMissing" });
        if (!response.IsSuccessStatusCode)
            return Json(new { disabled = true, reason = "GlobalProductPickerUnavailable" });

        return Json(new { disabled = false, options = await ParseGlobalProductsAsync(response, ct) });
    }

    // ---------------- helpers ----------------

    private async Task PopulateNodeOptionsAsync(ConceptNodeEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady)
        {
            model.ContractError = "ConceptContractUnavailable";
            return;
        }

        model.Statuses = contract.Vocabularies.ConceptStatuses;
        model.ExternalRefTypes = contract.Vocabularies.ExternalRefTypes;

        model.SubjectOptions = await LoadOptionsAsync("/api/crm/knowledge/subjects?includeArchived=false", ct, idKey: "subjectId");
        // Types are cascaded by the selected subject; carry SubjectId as the option group.
        model.TypeOptions = await LoadOptionsAsync("/api/crm/knowledge/concept-types?includeArchived=false", ct, groupKey: "subjectId", idKey: "conceptTypeId");

        await EnsureSelectedAsync(model.SubjectOptions, model.SubjectId, "/api/crm/knowledge/subjects/{0}", ct);
        await EnsureSelectedAsync(model.TypeOptions, model.ConceptTypeId, "/api/crm/knowledge/concept-types/{0}", ct, groupKey: "subjectId");

        // Global Product picker availability probe (the browser searches the selector lazily; here we only set the flag).
        var probe = await SendGatewayAsync(HttpMethod.Get, "/api/global-products/selector?pageSize=1", null, ct);
        model.GlobalProductPickerDisabledReason = probe switch
        {
            null => "GlobalProductPickerUnavailable",
            _ when (int)probe.StatusCode == 404 => "GlobalProductEndpointMissing",
            _ when (int)probe.StatusCode == 403 => "GlobalProductPermissionMissing",
            _ when !probe.IsSuccessStatusCode => "GlobalProductPickerUnavailable",
            _ => null
        };

        model.GlobalProductSelectedLabel = await ResolveGlobalProductLabelAsync(model.ExternalRefType, model.ExternalRefId, ct);
    }

    // EnsureSelected for the Global Product picker. The picker searches the MDM selector page by page, so a stored id
    // is usually NOT in the first page of results — it is resolved here by id and rendered as the pre-selected option.
    // Read-only: MDM stays the SoR and nothing is copied onto the node beyond the reference itself. A miss is not an
    // error; the caller then keeps the raw id so the value still survives the round-trip.
    private async Task<string?> ResolveGlobalProductLabelAsync(string? externalRefType, string? externalRefId, CancellationToken ct)
    {
        if (!string.Equals(externalRefType?.Trim(), "global-product", StringComparison.OrdinalIgnoreCase)) return null;
        if (!Guid.TryParse(externalRefId, out var productId)) return null;

        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/global-products/{productId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
            var code = GetFirstString(data, "canonicalCode", "code");
            var name = GetFirstString(data, "globalProductName", "name");
            return !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) ? $"{code} — {name}"
                : name ?? code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Global product ensure-selected parse failed for {ProductId}.", productId);
            return null;
        }
    }

    private async Task<List<ConceptOptionViewModel>> ParseGlobalProductsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var options = new List<ConceptOptionViewModel>();
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)) return options;
            // Selector returns a PagedResult { items: [ { id, canonicalCode, globalProductName } ] }.
            JsonElement items;
            if (data.ValueKind == JsonValueKind.Array) items = data;
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var it)) items = it;
            else return options;

            foreach (var el in items.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var id = GetFirstString(el, "id", "globalProductId");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var code = GetFirstString(el, "canonicalCode", "code");
                var name = GetFirstString(el, "globalProductName", "name");
                var label = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) ? $"{code} — {name}"
                    : name ?? code ?? id!;
                options.Add(new ConceptOptionViewModel { Value = id!, Label = label });
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Global product selector parse failed."); }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string SuggestNodeCode() =>
        $"CN-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private async Task<List<ConceptOptionViewModel>> LoadOptionsAsync(string path, CancellationToken ct, string? groupKey = null, string? idKey = null)
    {
        var options = new List<ConceptOptionViewModel>();
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
                    ?? GetFirstString(el, "id", "subjectId", "conceptTypeId");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var name = GetFirstString(el, "name", "subjectName", "conceptTypeName");
                var code = GetFirstString(el, "code", "subjectCode", "conceptTypeCode");
                var label = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) ? $"{code} — {name}"
                    : name ?? code ?? id!;
                var group = groupKey is not null ? GetFirstString(el, groupKey) ?? string.Empty : string.Empty;
                options.Add(new ConceptOptionViewModel { Value = id!, Label = label, Group = group });
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Concept reference option load failed: {Path}", path); }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task EnsureSelectedAsync(
        List<ConceptOptionViewModel> options, Guid? currentId, string byIdPathFormat, CancellationToken ct, string? groupKey = null)
    {
        if (currentId is null || currentId == Guid.Empty) return;
        var idStr = currentId.Value.ToString();
        if (options.Any(o => string.Equals(o.Value, idStr, StringComparison.OrdinalIgnoreCase))) return;

        var option = new ConceptOptionViewModel { Value = idStr, Label = idStr, IsInactive = true };
        var response = await SendGatewayAsync(HttpMethod.Get, string.Format(byIdPathFormat, idStr), null, ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    var name = GetFirstString(data, "name", "subjectName", "conceptTypeName");
                    var code = GetFirstString(data, "code", "subjectCode", "conceptTypeCode");
                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) option.Label = $"{code} — {name}";
                    else if (!string.IsNullOrWhiteSpace(name)) option.Label = name!;
                    else if (!string.IsNullOrWhiteSpace(code)) option.Label = code!;
                    if (groupKey is not null) option.Group = GetFirstString(data, groupKey) ?? string.Empty;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Concept ensure-selected parse failed: {Path}", byIdPathFormat); }
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

    private async Task<ConceptContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/knowledge/concept-graph/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ConceptGatewayResponse<ConceptContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<ConceptNodeDetailViewModel?> LoadNodeAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/knowledge/concept-nodes/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ConceptGatewayResponse<ConceptNodeDetailViewModel>>(_json, ct))?.Data;
    }

    private async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, ct), ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
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
            _logger.LogError(ex, "Concept Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content.ReadFromJsonAsync<ConceptGatewayResponse<object>>(_json, ct);
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

    private static object ToNodePayload(ConceptNodeEditViewModel m, bool forUpdate = false) => forUpdate
        ? new
        {
            m.ConceptNodeName, m.EffectiveFrom, m.Description, m.Status, m.EffectiveTo, m.ExternalRefType,
            m.ExternalRefId, m.MetadataJson
        }
        : new
        {
            m.SubjectId, m.ConceptTypeId, m.ConceptNodeCode, m.ConceptNodeName, m.EffectiveFrom, m.Description,
            m.Status, m.EffectiveTo, m.ExternalRefType, m.ExternalRefId, m.MetadataJson
        };

    private static ConceptNodeEditViewModel ToEditModel(ConceptNodeDetailViewModel n) => new()
    {
        ConceptNodeId = n.ConceptNodeId, SubjectId = n.SubjectId, ConceptTypeId = n.ConceptTypeId,
        ConceptNodeCode = n.ConceptNodeCode, ConceptNodeName = n.ConceptNodeName, Description = n.Description,
        Status = n.Status, EffectiveFrom = n.EffectiveFrom, EffectiveTo = n.EffectiveTo,
        ExternalRefType = n.ExternalRefType, ExternalRefId = n.ExternalRefId, MetadataJson = n.MetadataJson,
        IsArchived = n.IsArchived
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
