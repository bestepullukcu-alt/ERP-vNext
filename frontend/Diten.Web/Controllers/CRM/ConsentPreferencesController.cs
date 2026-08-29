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
/// MOD-0164-FU03 — Consent &amp; Preference Admin UI. UI-only consumer of the FU02 (Diten.CrmService) contract.
/// Every business call is proxied server-side through Gateway 5000; the browser never sees a service URL, a bearer token
/// or a :5061 origin. There is no delete surface — closing a record is the archive endpoint. TenantId is server-resolved
/// and never accepted on a payload. FU02 remains the authoritative validation and permission layer.
/// </summary>
[Authorize]
[Route("CRM/ConsentPreferences")]
public sealed class ConsentPreferencesController : Controller
{
    private const string ConsentReadPermission = "crm.consent.read";
    private const string ConsentManagePermission = "crm.consent.manage";
    private const string ConsentEvaluatePermission = "crm.consent.evaluate";
    private const string PreferenceReadPermission = "crm.preference.read";
    private const string PreferenceManagePermission = "crm.preference.manage";
    // FU02 documented fallback: canonical crm.consent.* / crm.preference.* keys are NOT seeded. The endpoints run on the
    // territory fallback (reads/evaluate → crm.territory.read, writes → crm.territory.model.manage). No new resolver, no
    // seed/grant — the frontend only mirrors the proven backend fallback.
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/ConsentPreferences";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ConsentPreferencesController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ConsentPreferencesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ConsentPreferencesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Shell ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        if (RequirePage(ConsentReadPermission, ReadFallback) is { } denied) return denied;
        return View($"{ViewRoot}/Index.cshtml", BuildIndexModel("consents"));
    }

    [HttpGet("Evaluate")]
    public IActionResult Evaluate()
    {
        if (RequirePage(ConsentEvaluatePermission, ConsentReadPermission, ReadFallback) is { } denied) return denied;
        return View($"{ViewRoot}/Index.cshtml", BuildIndexModel("evaluate"));
    }

    [HttpGet("Subject")]
    public IActionResult Subject()
    {
        if (RequirePage(ConsentReadPermission, ReadFallback) is { } denied) return denied;
        return View($"{ViewRoot}/Index.cshtml", BuildIndexModel("subject"));
    }

    private ConsentPreferenceIndexViewModel BuildIndexModel(string activeTab) => new()
    {
        ActiveTab = activeTab,
        CanReadConsent = HasAnyPermission(ConsentReadPermission, ReadFallback),
        CanManageConsent = HasAnyPermission(ConsentManagePermission, ManageFallback),
        CanEvaluate = HasAnyPermission(ConsentEvaluatePermission, ConsentReadPermission, ReadFallback),
        CanReadPreference = HasAnyPermission(PreferenceReadPermission, ConsentReadPermission, ReadFallback),
        CanManagePreference = HasAnyPermission(PreferenceManagePermission, ManageFallback)
    };

    // ---------------- Consent authoring pages ----------------

    [HttpGet("Consents/Create")]
    public async Task<IActionResult> ConsentCreate(CancellationToken ct)
    {
        if (RequirePage(ConsentManagePermission, ManageFallback) is { } denied) return denied;
        var model = new ConsentEditViewModel { EffectiveFrom = DateTimeOffset.Now };
        await PopulateConsentOptionsAsync(model, ct);
        return View($"{ViewRoot}/Consents/Create.cshtml", model);
    }

    [HttpPost("Consents/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsentCreate(ConsentEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ConsentManagePermission, ManageFallback) is { } denied) return denied;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateConsentOptionsAsync(model, ct);
            return View($"{ViewRoot}/Consents/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/consents", ToConsentCreatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(ConsentDetails), new { consentId = id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateConsentOptionsAsync(model, ct);
        return View($"{ViewRoot}/Consents/Create.cshtml", model);
    }

    [HttpGet("Consents/{consentId:guid}/Edit")]
    public async Task<IActionResult> ConsentEdit(Guid consentId, CancellationToken ct)
    {
        if (RequirePage(ConsentManagePermission, ManageFallback) is { } denied) return denied;
        var consent = await LoadConsentAsync(consentId, ct);
        if (consent is null) return NotFound();
        if (consent.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedRecordReadOnly";
            return RedirectToAction(nameof(ConsentDetails), new { consentId });
        }

        var model = ToConsentEditModel(consent);
        await PopulateConsentOptionsAsync(model, ct);
        return View($"{ViewRoot}/Consents/Edit.cshtml", model);
    }

    [HttpPost("Consents/{consentId:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsentEdit(Guid consentId, ConsentEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ConsentManagePermission, ManageFallback) is { } denied) return denied;
        model.ConsentId = consentId;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateConsentOptionsAsync(model, ct);
            return View($"{ViewRoot}/Consents/Edit.cshtml", model);
        }

        // Only the mutable answer dimensions are sent; the immutable question dimensions are never in the update body.
        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/consents/{consentId}", ToConsentUpdatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(ConsentDetails), new { consentId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulateConsentOptionsAsync(model, ct);
        return View($"{ViewRoot}/Consents/Edit.cshtml", model);
    }

    [HttpGet("Consents/{consentId:guid}")]
    public async Task<IActionResult> ConsentDetails(Guid consentId, CancellationToken ct)
    {
        if (RequirePage(ConsentReadPermission, ReadFallback) is { } denied) return denied;
        var consent = await LoadConsentAsync(consentId, ct);
        if (consent is null) return NotFound();

        await ResolveAuditNamesAsync(consent, ct);
        consent.SubjectName = await ResolveSubjectNameAsync(consent.SubjectType, consent.SubjectId, ct);

        var contract = await LoadContractAsync(ct) ?? new ConsentPreferenceContractViewModel();
        var model = new ConsentDetailPageViewModel
        {
            Consent = consent,
            Contract = contract,
            CanManage = !consent.IsArchived && HasAnyPermission(ConsentManagePermission, ManageFallback)
                && contract.Features.SupportsConsentManagement,
            CanEvaluate = HasAnyPermission(ConsentEvaluatePermission, ConsentReadPermission, ReadFallback)
                && contract.Features.SupportsConsentEvaluation
        };
        return View($"{ViewRoot}/Consents/Details.cshtml", model);
    }

    // ---------------- Preference authoring pages ----------------

    [HttpGet("Preferences/Create")]
    public async Task<IActionResult> PreferenceCreate(CancellationToken ct)
    {
        if (RequirePage(PreferenceManagePermission, ManageFallback) is { } denied) return denied;
        var model = new PreferenceEditViewModel { EffectiveFrom = DateTimeOffset.Now, Priority = 50 };
        await PopulatePreferenceOptionsAsync(model, ct);
        return View($"{ViewRoot}/Preferences/Create.cshtml", model);
    }

    [HttpPost("Preferences/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreferenceCreate(PreferenceEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(PreferenceManagePermission, ManageFallback) is { } denied) return denied;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulatePreferenceOptionsAsync(model, ct);
            return View($"{ViewRoot}/Preferences/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/preferences", ToPreferenceCreatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(PreferenceDetails), new { preferenceId = id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulatePreferenceOptionsAsync(model, ct);
        return View($"{ViewRoot}/Preferences/Create.cshtml", model);
    }

    [HttpGet("Preferences/{preferenceId:guid}/Edit")]
    public async Task<IActionResult> PreferenceEdit(Guid preferenceId, CancellationToken ct)
    {
        if (RequirePage(PreferenceManagePermission, ManageFallback) is { } denied) return denied;
        var preference = await LoadPreferenceAsync(preferenceId, ct);
        if (preference is null) return NotFound();
        if (preference.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedRecordReadOnly";
            return RedirectToAction(nameof(PreferenceDetails), new { preferenceId });
        }

        var model = ToPreferenceEditModel(preference);
        model.SubjectName = await ResolveSubjectNameAsync(model.SubjectType, model.SubjectId ?? Guid.Empty, ct);
        await PopulatePreferenceOptionsAsync(model, ct);
        return View($"{ViewRoot}/Preferences/Edit.cshtml", model);
    }

    [HttpPost("Preferences/{preferenceId:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreferenceEdit(Guid preferenceId, PreferenceEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(PreferenceManagePermission, ManageFallback) is { } denied) return denied;
        model.PreferenceId = preferenceId;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulatePreferenceOptionsAsync(model, ct);
            return View($"{ViewRoot}/Preferences/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/preferences/{preferenceId}", ToPreferenceUpdatePayload(model), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(PreferenceDetails), new { preferenceId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        await PopulatePreferenceOptionsAsync(model, ct);
        return View($"{ViewRoot}/Preferences/Edit.cshtml", model);
    }

    [HttpGet("Preferences/{preferenceId:guid}")]
    public async Task<IActionResult> PreferenceDetails(Guid preferenceId, CancellationToken ct)
    {
        if (RequirePage(PreferenceReadPermission, ConsentReadPermission, ReadFallback) is { } denied) return denied;
        var preference = await LoadPreferenceAsync(preferenceId, ct);
        if (preference is null) return NotFound();

        await ResolveAuditNamesAsync(preference, ct);
        preference.SubjectName = await ResolveSubjectNameAsync(preference.SubjectType, preference.SubjectId, ct);

        var contract = await LoadContractAsync(ct) ?? new ConsentPreferenceContractViewModel();
        var model = new PreferenceDetailPageViewModel
        {
            Preference = preference,
            Contract = contract,
            CanManage = !preference.IsArchived && HasAnyPermission(PreferenceManagePermission, ManageFallback)
                && contract.Features.SupportsPreferenceManagement
        };
        return View($"{ViewRoot}/Preferences/Details.cshtml", model);
    }

    // ---------------- Same-origin browser proxy (FU02 allowlist only; no DELETE) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/consents/contract", ConsentReadPermission, ct, ReadFallback);

    [HttpGet("api/consents")]
    public Task<IActionResult> ConsentList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/consents{Request.QueryString}", ConsentReadPermission, ct, ReadFallback);

    [HttpGet("api/consents/evaluate")]
    public Task<IActionResult> ConsentEvaluate(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/consents/evaluate{Request.QueryString}", ConsentEvaluatePermission, ct, ConsentReadPermission, ReadFallback);

    [HttpGet("api/consents/{consentId:guid}")]
    public Task<IActionResult> ConsentGet(Guid consentId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/consents/{consentId}", ConsentReadPermission, ct, ReadFallback);

    [HttpPost("api/consents/{consentId:guid}/archive")]
    public Task<IActionResult> ConsentArchive(Guid consentId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/consents/{consentId}/archive", null, ConsentManagePermission, ct, ManageFallback);

    [HttpGet("api/preferences")]
    public Task<IActionResult> PreferenceList(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/preferences{Request.QueryString}", PreferenceReadPermission, ct, ConsentReadPermission, ReadFallback);

    [HttpGet("api/preferences/{preferenceId:guid}")]
    public Task<IActionResult> PreferenceGet(Guid preferenceId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/preferences/{preferenceId}", PreferenceReadPermission, ct, ConsentReadPermission, ReadFallback);

    [HttpPost("api/preferences/{preferenceId:guid}/archive")]
    public Task<IActionResult> PreferenceArchive(Guid preferenceId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/preferences/{preferenceId}/archive", null, PreferenceManagePermission, ct, ManageFallback);

    /// <summary>
    /// MOD-0164-FU03 SCOPE EXTENSION (approved by the user 2026-08-03): a read-only subject picker for the consent
    /// Create form. When SubjectType has a master list (contact → /api/crm/contacts, account → /api/crm/accounts) this
    /// resolves display names so the operator selects a subject by name instead of pasting a GUID. It is read-only,
    /// Gateway-only and never writes; the Gateway still enforces the source module's own read permission (crm.contact.read
    /// / crm.account.read). Subject types without a list endpoint (hcp/hco/account-contact-link) return no options and the
    /// client falls back to GUID entry. This is beyond the FU03 consent/preference allowlist — documented as an extension.
    /// </summary>
    [HttpGet("api/subjects")]
    public async Task<IActionResult> SubjectOptions([FromQuery] string? subjectType, [FromQuery] string? search, CancellationToken ct)
    {
        if (RequireJson(ConsentReadPermission, ReadFallback) is { } denied) return denied;

        var (path, textField, codeField) = subjectType?.Trim().ToLowerInvariant() switch
        {
            "contact" => ("/api/crm/contacts?page=1&pageSize=200", "displayName", (string?)null),
            "account" => ("/api/crm/accounts?page=1&pageSize=200", "accountName", "accountCode"),
            _ => (null, null, null)
        };
        if (path is null) return Json(new { results = Array.Empty<object>() });

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return Json(new { results = Array.Empty<object>() });

        var raw = await response.Content.ReadAsStringAsync(ct);
        return Json(new { results = ParseSubjectItems(raw, textField!, codeField, search) });
    }

    /// <summary>
    /// Read-only display-name resolution for the consent list SubjectId column (part of the same user-approved scope
    /// extension as the picker). Returns up to 200 id→name pairs for a subject type so the browser can replace raw GUIDs
    /// with names (e.g. a contact GUID → "Dr. Beste Pullukçu", an account GUID → "Medicana Hospital"). Name only (no code
    /// prefix). Gateway-only, GET, no writes; the source module's read permission still applies. Unresolvable ids stay as
    /// the GUID on the client.
    /// </summary>
    [HttpGet("api/subjects/resolve")]
    public async Task<IActionResult> SubjectResolve([FromQuery] string? subjectType, CancellationToken ct)
    {
        if (RequireJson(ConsentReadPermission, ReadFallback) is { } denied) return denied;

        var (path, textField) = subjectType?.Trim().ToLowerInvariant() switch
        {
            "contact" => ("/api/crm/contacts?page=1&pageSize=200", "displayName"),
            "account" => ("/api/crm/accounts?page=1&pageSize=200", "accountName"),
            _ => (null, (string?)null)
        };
        if (path is null) return Json(new { results = Array.Empty<object>() });

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return Json(new { results = Array.Empty<object>() });

        var raw = await response.Content.ReadAsStringAsync(ct);
        return Json(new { results = ParseSubjectItems(raw, textField!, null, null, 200) });
    }

    private static List<object> ParseSubjectItems(string raw, string textField, string? codeField, string? search, int cap = 30)
    {
        var results = new List<object>();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
            if (!data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return results;

            var term = search?.Trim();
            foreach (var el in items.EnumerateArray())
            {
                if (!el.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString() ?? idEl.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                var name = GetStr(el, textField);
                var code = codeField is null ? null : GetStr(el, codeField);
                var text = string.IsNullOrWhiteSpace(code) ? name : $"{code} — {name}";
                if (string.IsNullOrWhiteSpace(text)) text = id;

                if (!string.IsNullOrWhiteSpace(term)
                    && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0
                    && id.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                results.Add(new { id, text });
                if (results.Count >= cap) break;
            }
        }
        catch { /* a malformed upstream payload yields no options; the client keeps GUID entry */ }
        return results;
    }

    private static string GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    // Best-effort single-subject name resolution for server-rendered pages (Details/Edit). contact/account only;
    // other subject types (and any miss/error) return null and the view keeps the GUID.
    private async Task<string?> ResolveSubjectNameAsync(string? subjectType, Guid subjectId, CancellationToken ct)
    {
        var (path, textField) = subjectType?.Trim().ToLowerInvariant() switch
        {
            "contact" => ("/api/crm/contacts?page=1&pageSize=200", "displayName"),
            "account" => ("/api/crm/accounts?page=1&pageSize=200", "accountName"),
            _ => (null, (string?)null)
        };
        if (path is null || subjectId == Guid.Empty) return null;

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;

        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;

            var target = subjectId.ToString();
            foreach (var el in items.EnumerateArray())
            {
                if (!el.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString() ?? idEl.ToString();
                if (!string.Equals(id, target, StringComparison.OrdinalIgnoreCase)) continue;
                var name = GetStr(el, textField!);
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }
        catch { /* keep GUID */ }
        return null;
    }

    // ---------------- Contract / load helpers ----------------

    private async Task PopulateConsentOptionsAsync(ConsentEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsConsentManagement)
        {
            model.ContractError = "ConsentContractUnavailable";
            model.SubjectTypes = ConsentVocabularyFallback.SubjectTypes;
            model.Channels = ConsentVocabularyFallback.ConsentChannels;
            model.Purposes = ConsentVocabularyFallback.Purposes;
            model.ScopeTypes = ConsentVocabularyFallback.ScopeTypes;
            model.LegalBases = ConsentVocabularyFallback.LegalBases;
            model.ConsentStatuses = ConsentVocabularyFallback.ConsentStatuses;
            model.Sources = ConsentVocabularyFallback.Sources;
            model.EvidenceRefTypes = ConsentVocabularyFallback.EvidenceRefTypes;
            model.EvidenceSourceModules = ConsentVocabularyFallback.EvidenceSourceModules;
            return;
        }

        var v = contract.Vocabulary;
        model.SubjectTypes = Or(v.SubjectTypes, ConsentVocabularyFallback.SubjectTypes);
        model.Channels = Or(v.Channels, ConsentVocabularyFallback.ConsentChannels);
        model.Purposes = Or(v.Purposes, ConsentVocabularyFallback.Purposes);
        model.ScopeTypes = Or(v.ScopeTypes, ConsentVocabularyFallback.ScopeTypes);
        model.LegalBases = Or(v.LegalBases, ConsentVocabularyFallback.LegalBases);
        model.ConsentStatuses = Or(v.ConsentStatuses, ConsentVocabularyFallback.ConsentStatuses);
        model.Sources = Or(v.Sources, ConsentVocabularyFallback.Sources);
        model.EvidenceRefTypes = Or(v.EvidenceRefTypes, ConsentVocabularyFallback.EvidenceRefTypes);
        model.EvidenceSourceModules = Or(v.EvidenceSourceModules, ConsentVocabularyFallback.EvidenceSourceModules);
    }

    private async Task PopulatePreferenceOptionsAsync(PreferenceEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsPreferenceManagement)
        {
            model.ContractError = "ConsentContractUnavailable";
            model.SubjectTypes = ConsentVocabularyFallback.SubjectTypes;
            model.PreferenceChannels = ConsentVocabularyFallback.PreferenceChannels;
            model.PreferenceTypes = ConsentVocabularyFallback.PreferenceTypes;
            model.Sources = ConsentVocabularyFallback.Sources;
            return;
        }

        var v = contract.Vocabulary;
        model.SubjectTypes = Or(v.SubjectTypes, ConsentVocabularyFallback.SubjectTypes);
        model.PreferenceChannels = Or(v.PreferenceChannels, ConsentVocabularyFallback.PreferenceChannels);
        model.PreferenceTypes = Or(v.PreferenceTypes, ConsentVocabularyFallback.PreferenceTypes);
        model.Sources = Or(v.Sources, ConsentVocabularyFallback.Sources);
    }

    private static IReadOnlyList<string> Or(List<string> primary, IReadOnlyList<string> fallback) =>
        primary is { Count: > 0 } ? primary : fallback;

    private async Task<ConsentPreferenceContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/consents/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<ConsentPreferenceContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<ConsentDetailViewModel?> LoadConsentAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/consents/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<ConsentDetailViewModel>>(_json, ct))?.Data;
    }

    private async Task<PreferenceDetailViewModel?> LoadPreferenceAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/preferences/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<PreferenceDetailViewModel>>(_json, ct))?.Data;
    }

    // Best-effort audit-name resolution: turns the CreatedBy/UpdatedBy/ArchivedBy user GUIDs into display names via the
    // AuthService (/api/users/{id}, gated by auth.users.read). If the caller lacks that permission or the lookup fails,
    // the name stays null and the view falls back to the raw GUID. Distinct ids are resolved once.
    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(IEnumerable<string?> userIds, CancellationToken ct)
    {
        var ids = userIds
            .Where(s => Guid.TryParse(s, out _))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var name = await ResolveUserDisplayNameAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(name)) names[id] = name!;
        }
        return names;
    }

    private async Task ResolveAuditNamesAsync(PreferenceDetailViewModel p, CancellationToken ct)
    {
        var names = await ResolveUserNamesAsync(new[] { p.CreatedBy, p.UpdatedBy, p.ArchivedBy }, ct);
        if (p.CreatedBy is not null && names.TryGetValue(p.CreatedBy, out var cn)) p.CreatedByName = cn;
        if (p.UpdatedBy is not null && names.TryGetValue(p.UpdatedBy, out var un)) p.UpdatedByName = un;
        if (p.ArchivedBy is not null && names.TryGetValue(p.ArchivedBy, out var an)) p.ArchivedByName = an;
    }

    private async Task ResolveAuditNamesAsync(ConsentDetailViewModel c, CancellationToken ct)
    {
        var names = await ResolveUserNamesAsync(new[] { c.CreatedBy, c.UpdatedBy, c.ArchivedBy }, ct);
        if (c.CreatedBy is not null && names.TryGetValue(c.CreatedBy, out var cn)) c.CreatedByName = cn;
        if (c.UpdatedBy is not null && names.TryGetValue(c.UpdatedBy, out var un)) c.UpdatedByName = un;
        if (c.ArchivedBy is not null && names.TryGetValue(c.ArchivedBy, out var an)) c.ArchivedByName = an;
    }

    // Tiered, best-effort resolution:
    //   1) Full identity via /api/users/{id} (auth.users.read) → "First Last".
    //   2) Masked identity via /api/users/{id}/lookup-validation (auth.users.lookup-validation) → "D***a a***@diten.com".
    //   3) null → the view falls back to the raw GUID.
    private async Task<string?> ResolveUserDisplayNameAsync(string userId, CancellationToken ct)
    {
        var full = await SendGatewayAsync(HttpMethod.Get, $"/api/users/{userId}", null, ct);
        if (full is not null && full.IsSuccessStatusCode)
        {
            try
            {
                var user = (await full.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<AuditUserDto>>(_json, ct))?.Data;
                if (user is not null)
                {
                    var name = $"{user.FirstName} {user.LastName}".Trim();
                    var display = string.IsNullOrWhiteSpace(name) ? user.Email : name;
                    if (!string.IsNullOrWhiteSpace(display)) return display;
                }
            }
            catch { /* fall through to masked */ }
        }

        var masked = await SendGatewayAsync(HttpMethod.Get, $"/api/users/{userId}/lookup-validation", null, ct);
        if (masked is not null && masked.IsSuccessStatusCode)
        {
            try
            {
                var m = (await masked.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<AuditUserLookupDto>>(_json, ct))?.Data;
                var display = $"{m?.MaskedName} {m?.MaskedEmail}".Trim();
                if (!string.IsNullOrWhiteSpace(display)) return display;
            }
            catch { /* fall through to GUID */ }
        }

        return null;
    }

    private sealed record AuditUserDto(string? FirstName, string? LastName, string? Email);
    private sealed record AuditUserLookupDto(string? MaskedName, string? MaskedEmail);

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
            _logger.LogError(ex, "Consent/Preference Gateway request failed: {Method} {Path}", method, path);
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
            var envelope = await response.Content.ReadFromJsonAsync<ConsentPreferenceGatewayResponse<object>>(_json, ct);
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

    // ---------------- Payload builders (NO TenantId, NO immutable dims on update) ----------------

    private static object ToConsentCreatePayload(ConsentEditViewModel m) => new
    {
        m.SubjectType,
        m.SubjectId,
        m.Channel,
        m.Purpose,
        m.LegalBasis,
        m.ConsentStatus,
        m.EffectiveFrom,
        m.Source,
        ScopeType = string.IsNullOrWhiteSpace(m.ScopeType) ? null : m.ScopeType,
        ScopeId = m.ScopeId,
        EffectiveTo = m.EffectiveTo,
        EvidenceRef = BuildEvidenceRef(m),
        WithdrawalReason = string.IsNullOrWhiteSpace(m.WithdrawalReason) ? null : m.WithdrawalReason,
        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes,
        ExternalReferences = m.ExternalReferences
    };

    private static object ToConsentUpdatePayload(ConsentEditViewModel m) => new
    {
        m.LegalBasis,
        m.ConsentStatus,
        m.EffectiveFrom,
        m.Source,
        EffectiveTo = m.EffectiveTo,
        EvidenceRef = BuildEvidenceRef(m),
        WithdrawalReason = string.IsNullOrWhiteSpace(m.WithdrawalReason) ? null : m.WithdrawalReason,
        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes,
        ExternalReferences = m.ExternalReferences
    };

    private static object? BuildEvidenceRef(ConsentEditViewModel m)
    {
        if (string.IsNullOrWhiteSpace(m.EvidenceRefType) || m.EvidenceRefId is not { } refId || refId == Guid.Empty
            || string.IsNullOrWhiteSpace(m.EvidenceSourceModule))
        {
            return null;
        }
        return new { RefType = m.EvidenceRefType, RefId = refId, SourceModule = m.EvidenceSourceModule, RefCode = m.EvidenceRefCode };
    }

    private static object ToPreferenceCreatePayload(PreferenceEditViewModel m) => new
    {
        m.SubjectType,
        m.SubjectId,
        m.Channel,
        m.PreferenceType,
        m.PreferenceValue,
        m.Priority,
        m.EffectiveFrom,
        m.Source,
        EffectiveTo = m.EffectiveTo,
        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes,
        ExternalReferences = m.ExternalReferences
    };

    private static object ToPreferenceUpdatePayload(PreferenceEditViewModel m) => new
    {
        m.PreferenceValue,
        m.Priority,
        m.EffectiveFrom,
        m.Source,
        EffectiveTo = m.EffectiveTo,
        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes,
        ExternalReferences = m.ExternalReferences
    };

    private static ConsentEditViewModel ToConsentEditModel(ConsentDetailViewModel c) => new()
    {
        ConsentId = c.ConsentId,
        SubjectType = c.SubjectType,
        SubjectId = c.SubjectId,
        Channel = c.Channel,
        Purpose = c.Purpose,
        ScopeType = c.ScopeType,
        ScopeId = c.ScopeId,
        LegalBasis = c.LegalBasis,
        ConsentStatus = c.ConsentStatus,
        Source = c.Source,
        EffectiveFrom = c.EffectiveFrom,
        EffectiveTo = c.EffectiveTo,
        WithdrawalReason = c.WithdrawalReason,
        Notes = c.Notes,
        EvidenceRefType = c.EvidenceRef?.RefType,
        EvidenceRefId = c.EvidenceRef?.RefId,
        EvidenceSourceModule = c.EvidenceRef?.SourceModule,
        EvidenceRefCode = c.EvidenceRef?.RefCode,
        ExternalReferences = c.ExternalReferences,
        IsArchived = c.IsArchived
    };

    private static PreferenceEditViewModel ToPreferenceEditModel(PreferenceDetailViewModel p) => new()
    {
        PreferenceId = p.PreferenceId,
        SubjectType = p.SubjectType,
        SubjectId = p.SubjectId,
        Channel = p.Channel,
        PreferenceType = p.PreferenceType,
        PreferenceValue = p.PreferenceValue,
        Priority = p.Priority,
        Source = p.Source,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo,
        Notes = p.Notes,
        ExternalReferences = p.ExternalReferences,
        IsArchived = p.IsArchived
    };

    private static void NormalizeExternalReferences(List<ConsentExternalReferenceViewModel> references) =>
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

/// <summary>FU02 runtime canonical vocabulary. Used only as a fallback when GET /consents/contract cannot be read; the
/// live contract vocabulary always wins. Consent channels never include <c>all</c>; preference channels add it.</summary>
internal static class ConsentVocabularyFallback
{
    public static readonly IReadOnlyList<string> SubjectTypes = ["contact", "account", "account-contact-link", "hcp", "hco"];
    public static readonly IReadOnlyList<string> ConsentChannels =
        ["visit", "email", "sms", "phone", "whatsapp", "portal", "digital-detailing", "training", "other"];
    public static readonly IReadOnlyList<string> PreferenceChannels =
        ["all", "visit", "email", "sms", "phone", "whatsapp", "portal", "digital-detailing", "training", "other"];
    public static readonly IReadOnlyList<string> Purposes =
        ["campaign", "medical-visit", "product-information", "training", "marketing", "service", "compliance", "research", "other"];
    public static readonly IReadOnlyList<string> LegalBases =
        ["explicit-consent", "contract", "legal-obligation", "legitimate-interest", "public-interest", "vital-interest", "other"];
    public static readonly IReadOnlyList<string> ConsentStatuses =
        ["granted", "denied", "withdrawn", "restricted", "unknown", "expired"];
    public static readonly IReadOnlyList<string> ScopeTypes = ["brand", "product", "topic", "campaign", "business-unit"];
    public static readonly IReadOnlyList<string> Sources =
        ["subject-declared", "field-capture", "portal", "consent-center", "legacy-import", "contract-document", "manual", "other"];
    public static readonly IReadOnlyList<string> PreferenceTypes =
        ["preferred-channel", "do-not-contact", "do-not-visit", "preferred-visit-window", "language-preference", "content-preference", "frequency-cap", "topic-interest"];
    public static readonly IReadOnlyList<string> EvidenceRefTypes = ["document", "record", "signature", "attachment"];
    public static readonly IReadOnlyList<string> EvidenceSourceModules = ["MOD-0028", "MOD-0029"];
}
