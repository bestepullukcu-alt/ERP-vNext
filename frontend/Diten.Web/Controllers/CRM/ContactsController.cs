using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0150 FU02 Contact & Relationship Management — Contact Foundation Golden Reference Compact vertical.
/// All backend traffic goes through the Gateway (5000); CrmService (5061) is never called directly. Reference
/// dropdowns are sourced live from MOD-0048 published values — no local fallback. Defence-in-depth authorization:
/// tenant-shell menu UX gate → this per-action MVC guard → CrmService `[HasPermission("crm.contact.*")]`.
/// </summary>
[Authorize]
[Route("CRM/Contacts")]
public sealed class ContactsController : Controller
{
    private const string ReadPermission = "crm.contact.read";
    private const string CreatePermission = "crm.contact.create";
    private const string UpdatePermission = "crm.contact.update";
    // MOD-0150 Import/Export Task 1 — existing MOD-0018 keys only; no new permission is introduced.
    private const string ImportPermission = "crm.contact.import";
    private const string ExportPermission = "crm.contact.export";
    private const string AccountContactReadPermission = "crm.account-contact.read";
    private const string AccountReadPermission = "crm.account.read";
    private const string AccountContactManagePermission = "crm.account-contact.manage";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private const string ContactTypeSetCode = "contact-type";
    private const string ContactStatusSetCode = "contact-status";
    // MOD-0150 Contact Location Hardening — same MOD-0048 location sets MOD-0149 Account uses (no CRM local list).
    private const string CountrySetCode = "country";
    private const string CitySetCode = "city";
    private const string DistrictSetCode = "district";
    // MOD-0150 pack §10 — optional professional reference sets (title/specialty/department), MOD-0048-sourced.
    private const string ProfessionalTitleSetCode = "professional-title";
    private const string MedicalSpecialtySetCode = "medical-specialty";
    private const string DepartmentTypeSetCode = "department-type";
    // Phone dial code + preferred language + gender (MOD-0048-sourced, optional).
    private const string PhoneCountryCodeSetCode = "phone-country-code";
    private const string PreferredLanguageSetCode = "preferred-language";
    private const string GenderSetCode = "gender";
    // MOD-0150 FU07 — availability type/source dropdowns come from MOD-0048 published values (no local list).
    private const string AvailabilityTypeSetCode = "contact-availability-type";
    private const string AvailabilitySourceSetCode = "contact-availability-source";
    private const string ViewRoot = "~/Views/CRM/Contacts";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<Diten.Web.Views.CRM.Contacts.ContactIndex> _localizer;
    private readonly ILogger<ContactsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ContactsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<Diten.Web.Views.CRM.Contacts.ContactIndex> localizer,
        ILogger<ContactsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        // Toolbar capability gating (MOD-0150 Import/Export Task 1). The Gateway/CrmService still enforce the same
        // permissions — this only decides whether the action is offered at all.
        ViewData["CanImport"] = PermissionClaims.HasPermission(User, ImportPermission);
        ViewData["CanExport"] = PermissionClaims.HasPermission(User, ExportPermission);
        ViewData["CanReadAccountContacts"] = PermissionClaims.HasPermission(User, AccountContactReadPermission);
        ViewData["CanReadAccounts"] = PermissionClaims.HasPermission(User, AccountReadPermission);
        return View($"{ViewRoot}/Index.cshtml");
    }

    /// <summary>
    /// MOD-0150 Import/Export Task 1 — streams the empty XLSX import template produced by CrmService through the
    /// Gateway. No parsing happens here; the file is passed through untouched.
    /// </summary>
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate([FromQuery] bool includeAccounts = false, CancellationToken cancellationToken = default)
    {
        if (RequirePage(ImportPermission) is { } denied)
        {
            return denied;
        }

        var accounts = includeAccounts && PermissionClaims.HasPermission(User, AccountReadPermission);
        return await StreamWorkbookAsync(
            $"/api/crm/contacts/import-template?format=xlsx&includeAccounts={accounts.ToString().ToLowerInvariant()}",
            "contacts-template.xlsx",
            cancellationToken);
    }

    /// <summary>
    /// MOD-0150 Import/Export Task 1 — streams the existing-data XLSX export (optionally with related account links,
    /// historical links, notes and the account lookup sheet). Option flags the caller is not entitled to are dropped
    /// here and re-checked server-side, so a missing permission can never widen the exported personal-data scope.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] bool includeLinks = false,
        [FromQuery] bool includeHistorical = false,
        [FromQuery] bool includeNotes = false,
        [FromQuery] bool includeAccounts = false,
        [FromQuery] string? contactType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? country = null,
        CancellationToken cancellationToken = default)
    {
        if (RequirePage(ExportPermission) is { } denied)
        {
            return denied;
        }

        var links = includeLinks && PermissionClaims.HasPermission(User, AccountContactReadPermission);
        var accounts = includeAccounts && PermissionClaims.HasPermission(User, AccountReadPermission);

        var query = new List<string>
        {
            "format=xlsx",
            $"includeLinks={links.ToString().ToLowerInvariant()}",
            $"includeHistorical={(links && includeHistorical).ToString().ToLowerInvariant()}",
            $"includeNotes={includeNotes.ToString().ToLowerInvariant()}",
            $"includeAccounts={accounts.ToString().ToLowerInvariant()}"
        };

        AppendFilter(query, "contactType", contactType);
        AppendFilter(query, "status", status);
        AppendFilter(query, "country", country);

        return await StreamWorkbookAsync(
            $"/api/crm/contacts/export?{string.Join("&", query)}", "contacts-export.xlsx", cancellationToken);
    }

    /// <summary>
    /// MOD-0150 Import/Export Task 2 — the import workspace (upload → dry-run preview → apply). A full compact page,
    /// not an offcanvas: the preview is a table the user has to read before approving a write.
    /// </summary>
    [HttpGet("Import")]
    public IActionResult Import()
    {
        if (RequirePage(ImportPermission) is { } denied)
        {
            return denied;
        }

        // Shown in the page so the user knows up front which rows they will be allowed to execute.
        ViewData["CanManageLinks"] = PermissionClaims.HasPermission(User, AccountContactManagePermission);
        ViewData["CanCreateContact"] = PermissionClaims.HasPermission(User, CreatePermission);
        ViewData["CanUpdateContact"] = PermissionClaims.HasPermission(User, UpdatePermission);
        return View($"{ViewRoot}/Import.cshtml");
    }

    /// <summary>Dry-run: validates the uploaded workbook and returns the preview. Writes nothing.</summary>
    [HttpPost("import/preview")]
    [RequestSizeLimit(MaxUploadBytes)]
    public Task<IActionResult> ImportPreview(IFormFile? file, [FromForm] bool strictMode = false, CancellationToken cancellationToken = default)
        => ForwardImportAsync(file, $"/api/crm/contacts/import-file?dryRun=true&strictMode={strictMode.ToString().ToLowerInvariant()}", cancellationToken);

    /// <summary>Apply: same validation, then writes the rows that passed.</summary>
    [HttpPost("import/apply")]
    [RequestSizeLimit(MaxUploadBytes)]
    public Task<IActionResult> ImportApply(IFormFile? file, [FromForm] bool strictMode = false, CancellationToken cancellationToken = default)
        => ForwardImportAsync(file, $"/api/crm/contacts/import-file/apply?strictMode={strictMode.ToString().ToLowerInvariant()}", cancellationToken);

    /// <summary>Streams the upload to the Gateway as multipart and passes the envelope back untouched. The file is
    /// never written to disk here and its name is never logged (it can carry personal data).</summary>
    private async Task<IActionResult> ForwardImportAsync(IFormFile? file, string path, CancellationToken cancellationToken)
    {
        if (RequireJson(ImportPermission) is { } denied)
        {
            return denied;
        }

        if (file is null || file.Length == 0)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { errors = new[] { _localizer["ImportSelectFile"].Value } });
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { errors = new[] { _localizer["ImportOnlyXlsx"].Value } });
        }

        if (!AddAuthHeaders())
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        }

        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(XlsxContentType);
            // A fixed, PII-free upload name: the backend only needs the extension.
            content.Add(fileContent, "file", "import.xlsx");

            using var response = await _httpClient.PostAsync($"{_gatewayUrl}{path}", content, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return Content(payload, "application/json") is var result && response.IsSuccessStatusCode
                ? result
                : StatusCode((int)response.StatusCode, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact workbook import failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { _sharedLocalizer["GatewayError"].Value } });
        }
    }

    private static void AppendFilter(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    /// <summary>Passes a Gateway-produced workbook straight through to the browser. On failure the user goes back to
    /// the list with a localized message — the raw gateway body (which could echo filter input) is never rendered.</summary>
    private async Task<IActionResult> StreamWorkbookAsync(string path, string fallbackFileName, CancellationToken cancellationToken)
    {
        if (!AddAuthHeaders())
        {
            TempData["ErrorMessage"] = _sharedLocalizer["Unauthorized"].Value;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{_gatewayUrl}{path}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errors = await ExtractGatewayErrorsAsync(response);
                _logger.LogWarning("Contact workbook request failed with {Status}.", response.StatusCode);
                TempData["ErrorMessage"] = errors.FirstOrDefault() ?? _sharedLocalizer["GatewayError"].Value;
                return RedirectToAction(nameof(Index));
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                           ?? fallbackFileName;

            return File(content, XlsxContentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact workbook request failed.");
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        if (RequirePage(CreatePermission) is { } denied)
        {
            return denied;
        }

        var model = new ContactEditViewModel();
        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] ContactEditViewModel model)
    {
        if (RequirePage(CreatePermission) is { } denied)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            await PopulateOptionsAsync(model);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/crm/contacts", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] ContactEditViewModel model)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        model.Id = id;

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            await PopulateOptionsAsync(model);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/crm/contacts/{id}", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact edit failed for {ContactId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    /// <summary>Contact 360 read view. Linked accounts (FU03) + consent/preference (MOD-0164/FU05) are read-only placeholders.</summary>
    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        var overview = await LoadOverviewAsync(id);
        if (overview?.Contact is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        // MOD-0150 FU07 — availability is a separate aggregate (never a Contact field), so it is loaded as its own
        // read-only 360 panel. An unavailable seam leaves the panel empty; it never blocks the page.
        overview.Availability = await LoadContactAvailabilityAsync(id) ?? [];

        return View($"{ViewRoot}/Details.cshtml", overview);
    }

    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        var model = await LoadDetailAsync(id);
        return model is null
            ? Json(new { success = false })
            : Json(new { success = true, data = model });
    }

    /// <summary>Filter/column dropdown source. Values come from MOD-0048 published values only.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        var contactTypes = await LoadReferenceOptionsAsync(ContactTypeSetCode);
        var statuses = await LoadReferenceOptionsAsync(ContactStatusSetCode);

        return Json(new
        {
            contactTypes = contactTypes.Select(x => new { value = x.Value, text = x.Text }),
            statuses = statuses.Select(x => new { value = x.Value, text = x.Text })
        });
    }

    /// <summary>Country-scope + territory-node filter source for the Contacts grid. A contact's territory is derived
    /// from its linked accounts, so the chips list EVERY distinct country scope / node from MOD-0151 Territory
    /// Management (not only the ones linked contacts happen to have). Each node carries its owning model's country
    /// scope so the Territory Node chip can cascade. Empty (never hardcoded) when the territory data is unavailable.</summary>
    [HttpGet("territory-lookups")]
    public async Task<IActionResult> TerritoryLookups()
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        var models = await LoadTerritoryModelsAsync();

        var countryScopes = models
            .Select(m => m.CountryScope)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nodePairs = new List<(string Name, string? CountryScope)>();
        foreach (var model in models)
        {
            var hierarchy = await LoadTerritoryNodesAsync(model.Id);
            if (hierarchy is null)
            {
                continue;
            }

            foreach (var node in hierarchy.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Name)))
            {
                nodePairs.Add((node.Name.Trim(), model.CountryScope?.Trim()));
            }
        }

        var nodes = nodePairs
            .DistinctBy(x => (x.Name.ToLowerInvariant(), (x.CountryScope ?? string.Empty).ToLowerInvariant()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new { name = x.Name, countryScope = x.CountryScope })
            .ToList();

        return Json(new { countryScopes, nodes });
    }

    private async Task<IReadOnlyList<TerritoryModelListItemViewModel>> LoadTerritoryModelsAsync()
    {
        if (!AddAuthHeaders())
        {
            return [];
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models?page=1&pageSize=200");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Territory models returned {Status}; territory filter renders empty.", response.StatusCode);
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<TerritoryModelListViewModel>>(_jsonOptions);
            return payload?.Data?.Items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory models load failed.");
            return [];
        }
    }

    private async Task<TerritoryHierarchyViewModel?> LoadTerritoryNodesAsync(Guid modelId)
    {
        if (!AddAuthHeaders())
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<TerritoryHierarchyViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory nodes load failed for model {ModelId}.", modelId);
            return null;
        }
    }

    private async Task PopulateOptionsAsync(ContactEditViewModel model)
    {
        model.ContactTypeOptions = await LoadReferenceOptionsAsync(ContactTypeSetCode);
        model.ContactStatusOptions = await LoadReferenceOptionsAsync(ContactStatusSetCode);
        // Optional location sets — an empty list is tolerated (Country is not required); no local fallback.
        model.CountryOptions = await LoadReferenceOptionsAsync(CountrySetCode);
        model.CityOptions = await LoadReferenceOptionsAsync(CitySetCode);
        model.DistrictOptions = await LoadReferenceOptionsAsync(DistrictSetCode);
        // Optional professional sets — empty tolerated (unpublished set → empty dropdown; stored value preserved via fallback-option).
        model.ProfessionalTitleOptions = await LoadReferenceOptionsAsync(ProfessionalTitleSetCode);
        model.SpecialtyOptions = await LoadReferenceOptionsAsync(MedicalSpecialtySetCode);
        model.DepartmentOptions = await LoadReferenceOptionsAsync(DepartmentTypeSetCode);
        // Phone dial code + preferred language — MOD-0048-sourced, empty tolerated (no CRM local fallback).
        model.PhoneCountryCodeOptions = await LoadReferenceOptionsAsync(PhoneCountryCodeSetCode);
        model.PreferredLanguageOptions = await LoadReferenceOptionsAsync(PreferredLanguageSetCode);
        model.GenderOptions = await LoadReferenceOptionsAsync(GenderSetCode);

        if (model.ContactTypeOptions.Count == 0 || model.ContactStatusOptions.Count == 0)
        {
            model.ReferenceDependencyMessage = _localizer["ReferenceDataUnavailable"].Value;
        }
    }

    /// <summary>Reads MOD-0048 published values through the Gateway. Returns an EMPTY list when unavailable — never a
    /// hardcoded fallback; the caller surfaces a controlled dependency message instead.</summary>
    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadReferenceOptionsAsync(string setCode)
    {
        if (!AddAuthHeaders())
            return [];

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return [];

        try
        {
            var url = $"{_gatewayUrl}/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}"
                      + $"/published-values?scope_key={Uri.EscapeDataString(tenantId)}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reference set '{SetCode}' returned {Status}; rendering without options.", setCode, response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<PublishedValuesModel>>(_jsonOptions);
            var items = payload?.Data?.Items;
            if (items is null)
                return [];

            return items
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Value))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Text ?? x.Value, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ReferenceOptionViewModel(x.Value!, string.IsNullOrWhiteSpace(x.Text) ? x.Value! : x.Text!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' load failed; rendering without options.", setCode);
            return [];
        }
    }

    private async Task<ContactDetailViewModel?> LoadDetailAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/contacts/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<ContactDetailViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact load failed for {ContactId}.", id);
            return null;
        }
    }

    private async Task<ContactOverviewViewModel?> LoadOverviewAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/contacts/{id}/overview");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<ContactOverviewViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact overview load failed for {ContactId}.", id);
            return null;
        }
    }

    private static ContactEditViewModel ToEditModel(ContactDetailViewModel model) => new()
    {
        Id = model.Id,
        FirstName = model.FirstName,
        LastName = model.LastName,
        DisplayName = model.DisplayName,
        ContactType = model.ContactType,
        Status = model.Status,
        Gender = model.Gender,
        PhotoDataUri = model.PhotoDataUri,
        ProfessionalTitle = model.ProfessionalTitle,
        Specialty = model.Specialty,
        Department = model.Department,
        Phone = model.Phone,
        Email = model.Email,
        ExternalReference = model.ExternalReferences.FirstOrDefault()?.ExternalId,
        Notes = model.Notes,
        CountryRef = model.CountryRef,
        CityRef = model.CityRef,
        DistrictRef = model.DistrictRef,
        AddressLine = model.AddressLine,
        PostalCode = model.PostalCode,
        PreferredLanguage = model.PreferredLanguage,
        PhoneCountryCode = model.PhoneCountryCode
    };

    private static ContactSavePayload ToPayload(ContactEditViewModel model) => new()
    {
        FirstName = model.FirstName.Trim(),
        LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim(),
        DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName.Trim(),
        ContactType = model.ContactType,
        Status = model.Status,
        Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim(),
        PhotoDataUri = string.IsNullOrWhiteSpace(model.PhotoDataUri) ? null : model.PhotoDataUri.Trim(),
        ProfessionalTitle = model.ProfessionalTitle,
        Specialty = model.Specialty,
        Department = model.Department,
        Phone = model.Phone,
        Email = model.Email,
        Notes = model.Notes,
        CountryRef = string.IsNullOrWhiteSpace(model.CountryRef) ? null : model.CountryRef.Trim(),
        CityRef = string.IsNullOrWhiteSpace(model.CityRef) ? null : model.CityRef.Trim(),
        DistrictRef = string.IsNullOrWhiteSpace(model.DistrictRef) ? null : model.DistrictRef.Trim(),
        AddressLine = string.IsNullOrWhiteSpace(model.AddressLine) ? null : model.AddressLine.Trim(),
        PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim(),
        PreferredLanguage = string.IsNullOrWhiteSpace(model.PreferredLanguage) ? null : model.PreferredLanguage.Trim(),
        PhoneCountryCode = string.IsNullOrWhiteSpace(model.PhoneCountryCode) ? null : model.PhoneCountryCode.Trim(),
        ExternalReference = string.IsNullOrWhiteSpace(model.ExternalReference)
            ? null
            : new ContactExternalReferenceInputPayload { ExternalId = model.ExternalReference.Trim() }
    };

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            message = _sharedLocalizer["GatewayError"].Value;

        return [message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            var errors = payload?.Errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (errors?.Count > 0)
                return errors;
        }
        catch
        {
            // Non-envelope error body; fall through to the raw payload below.
        }

        var raw = await response.Content.ReadAsStringAsync();
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    // ---------------------------------------------------------------------------------------------------------
    // MOD-0150 FU07 — Contact Availability & Visit Preference (AccountContactLink-scoped master data).
    // Every call goes through the Gateway (5000) on the routes that ride the existing /api/crm/contacts wildcard.
    // This surface answers "when can this person be visited HERE" — it never builds a route or a visit plan.
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Availability management page for one contact: one panel per linked account/location.</summary>
    [HttpGet("Availability/{contactId:guid}")]
    public async Task<IActionResult> Availability(Guid contactId, string? lookupDate)
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        var model = await BuildAvailabilityPageAsync(contactId, lookupDate);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/Availability.cshtml", model);
    }

    [HttpPost("Availability/{contactId:guid}/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAvailability(Guid contactId, ContactAvailabilityFormViewModel form)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        var payload = new
        {
            weekday = form.Weekday,
            startTime = form.StartTime,
            endTime = form.EndTime,
            availabilityType = form.AvailabilityType,
            source = form.Source,
            averageVisitDurationMinutes = form.AverageVisitDurationMinutes,
            effectiveFrom = form.EffectiveFrom,
            effectiveTo = form.EffectiveTo,
            notes = form.Notes,
            preference = new
            {
                preferredVisitStartTime = form.PreferredVisitStartTime,
                preferredVisitEndTime = form.PreferredVisitEndTime,
                avoidVisitStartTime = form.AvoidVisitStartTime,
                avoidVisitEndTime = form.AvoidVisitEndTime,
                appointmentRequired = form.AppointmentRequired,
                appointmentLeadTimeDays = form.AppointmentLeadTimeDays
            }
        };

        await PostAvailabilityAsync($"/api/crm/contacts/links/{form.AccountContactLinkId}/availability", payload);

        return RedirectToAction(nameof(Availability), new { contactId });
    }

    [HttpPost("Availability/{contactId:guid}/{availabilityId:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAvailability(Guid contactId, Guid availabilityId)
        => await AvailabilityLifecycleAsync(contactId, $"/api/crm/contacts/availability/{availabilityId}/deactivate");

    [HttpPost("Availability/{contactId:guid}/{availabilityId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveAvailability(Guid contactId, Guid availabilityId)
        => await AvailabilityLifecycleAsync(contactId, $"/api/crm/contacts/availability/{availabilityId}/archive");

    [HttpPost("Availability/{contactId:guid}/exceptions/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAvailabilityException(Guid contactId, ContactAvailabilityExceptionFormViewModel form)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        var payload = new
        {
            date = form.Date,
            isAvailable = form.IsAvailable,
            source = form.Source,
            startTime = form.StartTime,
            endTime = form.EndTime,
            reason = form.Reason,
            notes = form.Notes
        };

        await PostAvailabilityAsync($"/api/crm/contacts/links/{form.AccountContactLinkId}/availability-exceptions", payload);

        return RedirectToAction(nameof(Availability), new { contactId });
    }

    [HttpPost("Availability/{contactId:guid}/exceptions/{exceptionId:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAvailabilityException(Guid contactId, Guid exceptionId)
        => await AvailabilityLifecycleAsync(contactId, $"/api/crm/contacts/availability-exceptions/{exceptionId}/deactivate");

    /// <summary>JSON read seam used by the Contact 360 availability card.</summary>
    [HttpGet("availability-data/{contactId:guid}")]
    public async Task<IActionResult> AvailabilityData(Guid contactId)
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        var links = await LoadContactAvailabilityAsync(contactId);
        return Json(new { success = links is not null, data = links ?? [] });
    }

    private async Task<IActionResult> AvailabilityLifecycleAsync(Guid contactId, string path)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        await PostAvailabilityAsync(path, new { });
        return RedirectToAction(nameof(Availability), new { contactId });
    }

    /// <summary>POSTs to the Gateway and surfaces the backend's own controlled message (400/409) to the operator —
    /// the UI never invents a success or silently swallows a conflict.</summary>
    private async Task PostAvailabilityAsync(string path, object payload)
    {
        if (!AddAuthHeaders())
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return;
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}{path}", payload, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return;
            }

            var errors = await ExtractGatewayErrorsAsync(response);
            TempData["ErrorMessage"] = string.Join(" ", errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact availability call failed for {Path}.", path);
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
        }
    }

    private async Task<ContactAvailabilityPageViewModel?> BuildAvailabilityPageAsync(Guid contactId, string? lookupDate)
    {
        var contact = await LoadDetailAsync(contactId);
        if (contact is null)
        {
            return null;
        }

        var links = await LoadContactAvailabilityAsync(contactId) ?? [];

        return new ContactAvailabilityPageViewModel
        {
            ContactId = contactId,
            ContactDisplayName = contact.DisplayName,
            CanManage = PermissionClaims.HasPermission(User, UpdatePermission),
            Links = links,
            LookupDate = lookupDate,
            Lookup = string.IsNullOrWhiteSpace(lookupDate) ? null : await LoadAvailabilityLookupAsync(contactId, lookupDate),
            AvailabilityTypes = (await LoadReferenceOptionsAsync(AvailabilityTypeSetCode)).ToList(),
            AvailabilitySources = (await LoadReferenceOptionsAsync(AvailabilitySourceSetCode)).ToList()
        };
    }

    private async Task<List<LinkAvailabilityViewModel>?> LoadContactAvailabilityAsync(Guid contactId)
    {
        if (!AddAuthHeaders())
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/contacts/{contactId}/availability");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<List<LinkAvailabilityViewModel>>>(_jsonOptions);
            return payload?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact availability load failed for {ContactId}.", contactId);
            return null;
        }
    }

    private async Task<ContactAvailabilityLookupViewModel?> LoadAvailabilityLookupAsync(Guid contactId, string date)
    {
        if (!AddAuthHeaders())
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_gatewayUrl}/api/crm/contacts/availability-lookup?date={Uri.EscapeDataString(date)}&contactId={contactId}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<ContactAvailabilityLookupViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact availability lookup failed for {ContactId} on {Date}.", contactId, date);
            return null;
        }
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private IActionResult? RequirePage(string permission) =>
        PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission) =>
        PermissionClaims.HasPermission(User, permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
