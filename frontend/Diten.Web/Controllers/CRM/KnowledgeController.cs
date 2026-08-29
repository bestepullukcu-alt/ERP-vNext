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
/// MOD-0162-FU02 Knowledge / Content Taxonomy Admin UI. All business traffic is proxied server-side through Gateway
/// 5000; the browser never sees a service URL or bearer token. The CrmService FU02 runtime remains the authoritative
/// validation and permission layer. There is no delete surface — closing anything is Archive.
/// </summary>
[Authorize]
[Route("CRM/Knowledge")]
public sealed class KnowledgeController : Controller
{
    private const string ReadPermission = "crm.knowledge.read";
    private const string ManagePermission = "crm.knowledge.manage";
    private const string SubjectReadPermission = "crm.knowledge.subject.read";
    private const string SubjectManagePermission = "crm.knowledge.subject.manage";
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/Knowledge";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<KnowledgeController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public KnowledgeController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<KnowledgeController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Content Compact pages ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission, ReadFallback) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var model = new KnowledgeContentEditViewModel
        {
            EffectiveFrom = DateTimeOffset.Now,
            ContentCode = SuggestContentCode()
        };
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KnowledgeContentEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/knowledge/contents", ToPayload(model, includeCode: true), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<KnowledgeGatewayResponse<Guid>>(_json, cancellationToken);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var content = await LoadContentAsync(id, cancellationToken);
        if (content is null) return NotFound();
        if (content.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedContentReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(content);
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, KnowledgeContentEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.ContentId = id;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/knowledge/contents/{id}", ToPayload(model, includeCode: false), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var content = await LoadContentAsync(id, cancellationToken);
        if (content is null) return NotFound();

        var model = new KnowledgeContentPageViewModel
        {
            Content = content,
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            DocumentRef = await ResolveDocumentRefAsync(content.FileRef, cancellationToken)
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    [HttpGet("Taxonomy")]
    public IActionResult Taxonomy()
        => RequirePage(SubjectReadPermission, ReadPermission, ReadFallback) ?? View($"{ViewRoot}/Taxonomy.cshtml");

    // Streams the current version of a Document Management controlled document INLINE (no attachment disposition) so the
    // browser previews it (PDF/image) in place. The gateway still enforces auth/tenant on the underlying download.
    [HttpGet("document-preview/{documentId:guid}")]
    public async Task<IActionResult> DocumentPreview(Guid documentId, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;

        var versionId = await ResolveCurrentVersionIdAsync(documentId, ct);
        if (versionId is null) return NotFound();

        var download = await SendGatewayAsync(
            HttpMethod.Get, $"/api/v1/document-management/controlled-documents/{documentId}/versions/{versionId}/download", null, ct);
        if (download is null || !download.IsSuccessStatusCode) return NotFound();

        var bytes = await download.Content.ReadAsByteArrayAsync(ct);
        var contentType = download.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        Response.Headers["Content-Disposition"] = "inline"; // preview, not download
        return File(bytes, contentType);
    }

    private async Task<KnowledgeDocumentRefViewModel?> ResolveDocumentRefAsync(string? fileRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileRef) || !Guid.TryParse(fileRef, out var docId)) return null;
        var data = await LoadDocumentDataAsync(docId, ct);
        if (data is null) return null;

        var version = GetFirstString(data.Value, "currentVersionId", "controlledDocumentVersionId", "versionId");
        return new KnowledgeDocumentRefViewModel
        {
            DocumentId = docId,
            Title = GetFirstString(data.Value, "title", "documentTitle", "name") ?? docId.ToString(),
            FileName = GetFirstString(data.Value, "fileName"),
            HasFile = !string.IsNullOrWhiteSpace(version)
        };
    }

    private async Task<Guid?> ResolveCurrentVersionIdAsync(Guid documentId, CancellationToken ct)
    {
        var data = await LoadDocumentDataAsync(documentId, ct);
        var version = data is null ? null : GetFirstString(data.Value, "currentVersionId", "controlledDocumentVersionId", "versionId");
        return Guid.TryParse(version, out var v) ? v : null;
    }

    private async Task<JsonElement?> LoadDocumentDataAsync(Guid documentId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/v1/document-management/controlled-documents/{documentId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                return data.Clone();
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Knowledge document detail load failed: {DocumentId}", documentId); }
        return null;
    }

    // ---------------- Same-origin browser proxy (FU02 allowlist only) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/knowledge/contract", ReadPermission, ct, ReadFallback);

    // Refreshable Document Reference options (so the picker can update after a document is created in Document Management,
    // without a full page reload). Returns [{ value, label, group, isInactive }].
    [HttpGet("api/document-options")]
    public async Task<IActionResult> DocumentOptionsJson(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        var options = await LoadOptionsAsync("/api/v1/document-management/controlled-documents", ct);
        return Json(options);
    }

    // Brand / Product name options (id -> "code — name") so the content list can show names instead of raw ids.
    [HttpGet("api/brand-options")]
    public async Task<IActionResult> BrandOptionsJson(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        return Json(await LoadOptionsAsync("/api/mdm/brands", ct, idKey: "brandId"));
    }

    [HttpGet("api/product-options")]
    public async Task<IActionResult> ProductOptionsJson(CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        return Json(await LoadOptionsAsync("/api/mdm/products", ct, idKey: "productId"));
    }

    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/contents{Request.QueryString}", ReadPermission, ct, ReadFallback);

    [HttpPost("api/contents/{contentId:guid}/archive")]
    public Task<IActionResult> ArchiveContent(Guid contentId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/contents/{contentId}/archive", null, ManagePermission, ct, ManageFallback);

    [HttpGet("api/subjects")]
    public Task<IActionResult> SubjectList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/subjects{Request.QueryString}", SubjectReadPermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/subjects")]
    public Task<IActionResult> CreateSubject([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/subjects", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/subjects/{subjectId:guid}")]
    public Task<IActionResult> UpdateSubject(Guid subjectId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/subjects/{subjectId}", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/subjects/{subjectId:guid}/archive")]
    public Task<IActionResult> ArchiveSubject(Guid subjectId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/subjects/{subjectId}/archive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/subjects/{subjectId:guid}/unarchive")]
    public Task<IActionResult> UnarchiveSubject(Guid subjectId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/subjects/{subjectId}/unarchive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpGet("api/topics")]
    public Task<IActionResult> TopicList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/topics{Request.QueryString}", SubjectReadPermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/topics")]
    public Task<IActionResult> CreateTopic([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/topics", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/topics/{topicId:guid}")]
    public Task<IActionResult> UpdateTopic(Guid topicId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/topics/{topicId}", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/topics/{topicId:guid}/archive")]
    public Task<IActionResult> ArchiveTopic(Guid topicId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/topics/{topicId}/archive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/topics/{topicId:guid}/unarchive")]
    public Task<IActionResult> UnarchiveTopic(Guid topicId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/topics/{topicId}/unarchive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpGet("api/audience-profiles")]
    public Task<IActionResult> ProfileList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/knowledge/audience-profiles{Request.QueryString}", SubjectReadPermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/audience-profiles")]
    public Task<IActionResult> CreateProfile([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, "/api/crm/knowledge/audience-profiles", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/audience-profiles/{profileId:guid}")]
    public Task<IActionResult> UpdateProfile(Guid profileId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/knowledge/audience-profiles/{profileId}", body, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/audience-profiles/{profileId:guid}/archive")]
    public Task<IActionResult> ArchiveProfile(Guid profileId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/audience-profiles/{profileId}/archive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/audience-profiles/{profileId:guid}/unarchive")]
    public Task<IActionResult> UnarchiveProfile(Guid profileId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/knowledge/audience-profiles/{profileId}/unarchive", null, SubjectManagePermission, ct, ManagePermission, ManageFallback);

    // ---------------- helpers ----------------

    private async Task PopulateContractOptionsAsync(KnowledgeContentEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsKnowledgeContentManagement)
        {
            model.ContractError = "KnowledgeContractUnavailable";
            return;
        }

        model.ContentTypes = contract.Vocabularies.ContentTypes;
        model.ContentStatuses = contract.Vocabularies.ContentStatuses;
        model.ContentSources = contract.Vocabularies.ContentSources;

        await PopulateReferenceOptionsAsync(model, ct);
    }

    // Server-populates the reference picker dropdowns from the gateway. Subject/Topic/AudienceProfile come from the
    // Knowledge taxonomy; Brand/Product from MDM; Campaign from CRM; ConceptType/ConceptNode from the MOD-0162-FU03
    // concept graph. Segment still has no runtime source (MOD-0167 not built) so it is intentionally not loaded — the
    // form renders it disabled.
    private async Task PopulateReferenceOptionsAsync(KnowledgeContentEditViewModel model, CancellationToken ct)
    {
        // Active-only lists. Topics carry their SubjectId as Group so the UI can cascade Topic by the selected Subject.
        var subjects = await LoadOptionsAsync("/api/crm/knowledge/subjects?includeArchived=false", ct, idKey: "subjectId");
        var topics = await LoadOptionsAsync("/api/crm/knowledge/topics?includeArchived=false", ct, groupKey: "subjectId", idKey: "topicId");
        var audiences = await LoadOptionsAsync("/api/crm/knowledge/audience-profiles?includeArchived=false", ct, idKey: "audienceProfileId");
        // AC-UI-3: the concept chain. Types carry their SubjectId and nodes their ConceptTypeId as Group, so the UI can
        // cascade Subject → ConceptType → ConceptNode. Archived rows are dropped by LoadOptionsAsync — an archived node
        // is never offered as a NEW choice (a previously saved one is still preserved below by EnsureSelectedAsync).
        var conceptTypes = await LoadOptionsAsync("/api/crm/knowledge/concept-types?includeArchived=false", ct, groupKey: "subjectId", idKey: "conceptTypeId");
        var conceptNodes = await LoadOptionsAsync("/api/crm/knowledge/concept-nodes?includeArchived=false", ct, groupKey: "conceptTypeId", idKey: "conceptNodeId");
        var brands = await LoadOptionsAsync("/api/mdm/brands", ct, idKey: "brandId");
        var products = await LoadOptionsAsync("/api/mdm/products", ct, idKey: "productId");
        var campaigns = await LoadOptionsAsync("/api/crm/campaigns", ct, idKey: "campaignId");
        // Document Reference (FileRef) is a pointer to a Document Management controlled document. Creating new ones stays
        // in Document Management's governed flow (collection instance based); here we only let the user PICK an existing one.
        var documents = await LoadOptionsAsync("/api/v1/document-management/controlled-documents", ct);

        // On edit, a value saved earlier may now be archived/invalid and thus absent from the active lists above. Keep it
        // visible (fetched name if possible, else raw id) so the record is not silently rebound to a wrong/blank value.
        await EnsureSelectedAsync(subjects, model.SubjectId, "/api/crm/knowledge/subjects/{0}", ct);
        await EnsureSelectedAsync(topics, model.TopicId, "/api/crm/knowledge/topics/{0}", ct, groupKey: "subjectId");
        await EnsureSelectedAsync(audiences, model.AudienceProfileId, "/api/crm/knowledge/audience-profiles/{0}", ct);
        await EnsureSelectedAsync(brands, model.BrandId, "/api/mdm/brands/{0}", ct);
        await EnsureSelectedAsync(products, model.ProductId, "/api/mdm/products/{0}", ct);
        await EnsureSelectedAsync(campaigns, model.CampaignId, "/api/crm/campaigns/{0}", ct);
        // A node saved earlier may now be archived (or belong to a type outside the current subject). Keeping it in the
        // list is what stops a plain Save from silently clearing ConceptNodeId — the backend V17 dirty-check only skips
        // validation for an UNCHANGED value, so the value has to survive the round-trip to stay unchanged.
        await EnsureSelectedAsync(conceptNodes, model.ConceptNodeId, "/api/crm/knowledge/concept-nodes/{0}", ct, groupKey: "conceptTypeId");
        await EnsureSelectedRawAsync(documents, model.FileRef, "/api/v1/document-management/controlled-documents/{0}", ct);

        model.SubjectOptions = subjects;
        model.TopicOptions = topics;
        model.AudienceProfileOptions = audiences;
        model.ConceptTypeOptions = conceptTypes;
        model.ConceptNodeOptions = conceptNodes;
        model.BrandOptions = brands;
        model.ProductOptions = products;
        model.CampaignOptions = campaigns;
        model.DocumentOptions = documents;
    }

    // Same as EnsureSelectedAsync but for a string-valued reference (FileRef may hold a document id or legacy free text).
    private async Task EnsureSelectedRawAsync(List<KnowledgeOptionViewModel> options, string? currentValue, string byIdPathFormat, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentValue)) return;
        if (options.Any(o => string.Equals(o.Value, currentValue, StringComparison.OrdinalIgnoreCase))) return;

        var option = new KnowledgeOptionViewModel { Value = currentValue, Label = currentValue, IsInactive = true };
        if (Guid.TryParse(currentValue, out _))
        {
            var response = await SendGatewayAsync(HttpMethod.Get, string.Format(byIdPathFormat, currentValue), null, ct);
            if (response is not null && response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        var name = GetFirstString(data, "name", "title", "documentTitle");
                        var code = GetFirstString(data, "code", "documentCode");
                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) option.Label = $"{code} — {name}";
                        else if (!string.IsNullOrWhiteSpace(name)) option.Label = name!;
                        else if (!string.IsNullOrWhiteSpace(code)) option.Label = code!;
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Knowledge document ensure-selected parse failed."); }
            }
        }
        options.Insert(0, option);
    }

    // If currentId is set but not already in the active options, fetch it by id and prepend it flagged inactive so an
    // archived/invalid saved reference still renders (and its value survives the round-trip). Falls back to the raw id.
    private async Task EnsureSelectedAsync(
        List<KnowledgeOptionViewModel> options, Guid? currentId, string byIdPathFormat, CancellationToken ct, string? groupKey = null)
    {
        if (currentId is null || currentId == Guid.Empty) return;
        var idStr = currentId.Value.ToString();
        if (options.Any(o => string.Equals(o.Value, idStr, StringComparison.OrdinalIgnoreCase))) return;

        var option = new KnowledgeOptionViewModel { Value = idStr, Label = idStr, IsInactive = true };
        var response = await SendGatewayAsync(HttpMethod.Get, string.Format(byIdPathFormat, idStr), null, ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    var name = GetFirstString(data, "name", "subjectName", "topicName", "profileName", "brandName", "productName", "campaignName", "conceptNodeName", "conceptTypeName");
                    var code = GetFirstString(data, "code", "subjectCode", "topicCode", "profileCode", "brandCode", "productCode", "campaignCode", "conceptNodeCode", "conceptTypeCode");
                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) option.Label = $"{code} — {name}";
                    else if (!string.IsNullOrWhiteSpace(name)) option.Label = name!;
                    else if (!string.IsNullOrWhiteSpace(code)) option.Label = code!;
                    if (groupKey is not null) option.Group = GetFirstString(data, groupKey) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Knowledge ensure-selected parse failed: {Path}", byIdPathFormat);
            }
        }
        options.Insert(0, option);
    }

    // Suggested (editable) content code default for new records: KC-{yyyy}-{6 hex}.
    private static string SuggestContentCode() =>
        $"KC-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    // Tolerant list parser: accepts either { data: [ ... ] } or { data: { items: [ ... ] } }; skips archived rows;
    // resolves id/name/code across the differing field names of the Knowledge, MDM and Campaign endpoints.
    private async Task<List<KnowledgeOptionViewModel>> LoadOptionsAsync(string path, CancellationToken ct, string? groupKey = null, string? idKey = null)
    {
        var options = new List<KnowledgeOptionViewModel>();
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

                // An explicit idKey disambiguates entities that also carry foreign-key id fields (a Topic has BOTH TopicId
                // and SubjectId; a Product/Campaign has BrandId, etc.), which the generic order would otherwise mispick.
                var id = (idKey is not null ? GetFirstString(el, idKey) : null)
                    ?? GetFirstString(el, "id", "brandId", "productId", "campaignId", "subjectId", "topicId", "audienceProfileId", "controlledDocumentId", "documentId");
                if (string.IsNullOrWhiteSpace(id)) continue;

                var name = GetFirstString(el, "name", "subjectName", "topicName", "profileName", "brandName", "productName", "campaignName", "conceptNodeName", "conceptTypeName", "title", "documentTitle");
                var code = GetFirstString(el, "code", "subjectCode", "topicCode", "profileCode", "brandCode", "productCode", "campaignCode", "conceptNodeCode", "conceptTypeCode", "documentCode");
                var label = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) ? $"{code} — {name}"
                    : !string.IsNullOrWhiteSpace(name) ? name!
                    : !string.IsNullOrWhiteSpace(code) ? code!
                    : id!;
                var group = groupKey is not null ? GetFirstString(el, groupKey) ?? string.Empty : string.Empty;
                options.Add(new KnowledgeOptionViewModel { Value = id!, Label = label, Group = group });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge reference option load failed: {Path}", path);
        }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
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

    private async Task<KnowledgeContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/knowledge/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<KnowledgeGatewayResponse<KnowledgeContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<KnowledgeContentDetailViewModel?> LoadContentAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/knowledge/contents/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<KnowledgeGatewayResponse<KnowledgeContentDetailViewModel>>(_json, ct))?.Data;
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
            _logger.LogError(ex, "Knowledge Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content.ReadFromJsonAsync<KnowledgeGatewayResponse<object>>(_json, ct);
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

    private static object ToPayload(KnowledgeContentEditViewModel m, bool includeCode)
    {
        var tags = string.IsNullOrWhiteSpace(m.Tags)
            ? new List<string>()
            : m.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return includeCode
            ? new
            {
                m.ContentCode, m.ContentTitle, m.ContentType, m.ContentStatus, m.SubjectId, m.TopicId,
                m.AudienceProfileId, m.ConceptNodeId, m.BrandId, m.ProductId, m.CampaignId, m.SegmentId,
                m.LanguageCode, m.Summary, m.ContentBodyRef, m.ContentAssetRef, m.FileRef, m.Url, m.ContentVersion,
                m.EffectiveFrom, m.EffectiveTo, m.Source, Tags = tags, ExternalReferences = m.ExternalReferences
            }
            : new
            {
                m.ContentTitle, m.ContentType, m.ContentStatus, m.SubjectId, m.TopicId, m.AudienceProfileId,
                m.ConceptNodeId, m.BrandId, m.ProductId, m.CampaignId, m.SegmentId, m.LanguageCode, m.Summary,
                m.ContentBodyRef, m.ContentAssetRef, m.FileRef, m.Url, m.ContentVersion, m.EffectiveFrom,
                m.EffectiveTo, m.Source, Tags = tags, ExternalReferences = m.ExternalReferences
            };
    }

    private static KnowledgeContentEditViewModel ToEditModel(KnowledgeContentDetailViewModel c) => new()
    {
        ContentId = c.ContentId, ContentCode = c.ContentCode, ContentTitle = c.ContentTitle, ContentType = c.ContentType,
        ContentStatus = c.ContentStatus, SubjectId = c.SubjectId, TopicId = c.TopicId,
        AudienceProfileId = c.AudienceProfileId, ConceptNodeId = c.ConceptNodeId, BrandId = c.BrandId,
        ProductId = c.ProductId, CampaignId = c.CampaignId, SegmentId = c.SegmentId, LanguageCode = c.LanguageCode,
        Summary = c.Summary, ContentBodyRef = c.ContentBodyRef, ContentAssetRef = c.ContentAssetRef, FileRef = c.FileRef,
        Url = c.Url, ContentVersion = c.ContentVersion, EffectiveFrom = c.EffectiveFrom, EffectiveTo = c.EffectiveTo,
        Source = c.Source, Tags = string.Join(", ", c.Tags), ExternalReferences = c.ExternalReferences,
        IsArchived = c.IsArchived
    };

    private static void NormalizeExternalReferences(List<KnowledgeExternalReferenceViewModel> references) =>
        references.RemoveAll(x => string.IsNullOrWhiteSpace(x.SourceSystem) && string.IsNullOrWhiteSpace(x.ExternalId));

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
