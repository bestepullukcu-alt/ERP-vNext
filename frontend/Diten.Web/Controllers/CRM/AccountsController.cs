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
/// MOD-0149 Customer 360 / Account Hierarchy — Golden Reference Compact vertical (17 form fields → full pages).
/// All backend traffic goes through the Gateway (5000); CrmService (5061) is never called directly.
/// Reference dropdowns are sourced live from MOD-0048 published values — there is no local fallback list.
/// <para>Authorization is defence-in-depth: the tenant-shell menu is UX-gated by <c>crm.account.read</c>, this
/// controller enforces a per-action MVC guard (repo standard: <see cref="PermissionClaims"/> inline check →
/// bare 403 re-executed to the friendly /Home/Status/403 page for pages, JSON 403 for API actions), and the
/// authoritative enforcement stays in CrmService (`[HasPermission("crm.account.*")]`).</para>
/// </summary>
[Authorize]
[Route("CRM/Accounts")]
public sealed class AccountsController : Controller
{
    // Per-action permission keys. Verbatim MOD-0018 catalog keys — never `crm.account.360.read`.
    private const string ReadPermission = "crm.account.read";
    private const string CreatePermission = "crm.account.create";
    private const string UpdatePermission = "crm.account.update";

    // Account 360 related-projection read gates (defence-in-depth mirror of the CrmService [HasPermission] on the
    // MOD-0150 FU03/FU04 projection endpoints). Verbatim catalog keys — never `crm.account.360.related.read`.
    private const string RelatedContactReadPermission = "crm.account-contact.read";
    private const string RelatedAccountReadPermission = "crm.account-relationship.read";

    // Relationship management (Add/Edit/End) gates — verbatim MOD-0018 catalog keys.
    private const string RelatedContactManagePermission = "crm.account-contact.manage";
    private const string RelatedAccountManagePermission = "crm.account-relationship.manage";

    // MOD-0151 territory coverage read gate (defence-in-depth mirror of the CrmService [HasPermission] on the
    // /territory-coverage-summary endpoint). Verbatim catalog key.
    private const string TerritoryCoverageReadPermission = "crm.territory.model.read";

    private const string ContactRoleSetCode = "contact-role";
    private const string RelationshipTypeSetCode = "account-relationship-type";
    private const string RelationshipStatusSetCode = "account-relationship-status";

    /// <summary>Status value written by an End action (historical close). Kept lowercase to match MOD-0048 values.</summary>
    private const string EndedStatus = "ended";

    private const string AccountTypeSetCode = "account-type";
    private const string AccountStatusSetCode = "account-status";
    private const string AccountCategorySetCode = "account-category";
    private const string CountrySetCode = "country";
    private const string CitySetCode = "city";
    private const string DistrictSetCode = "district";
    private const string ViewRoot = "~/Views/CRM/Accounts";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<Diten.Web.Views.CRM.Accounts.AccountIndex> _localizer;
    private readonly ILogger<AccountsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AccountsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<Diten.Web.Views.CRM.Accounts.AccountIndex> localizer,
        ILogger<AccountsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    /// <summary>Index renders the shell only; the grid and filter chips load via the DataTable and /lookups.</summary>
    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        if (RequirePage(CreatePermission) is { } denied)
        {
            return denied;
        }

        var model = new AccountEditViewModel();
        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] AccountEditViewModel model)
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
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/accounts",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account create failed.");
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
    public async Task<IActionResult> Edit(Guid id, [FromForm] AccountEditViewModel model)
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
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/crm/accounts/{id}",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account edit failed for {AccountId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        await PopulateOptionsAsync(model);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    /// <summary>Account 360 read view. Coverage is a read-only projection owned by MOD-0151.</summary>
    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        // Page-level read gate; the CrmService /overview endpoint separately enforces `crm.account.overview.read`.
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        var overview = await LoadOverviewAsync(id);
        if (overview?.Account is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        // Related Contacts (FU03) + Related Accounts (FU04) are permission-gated read-only 360 projections. They are
        // loaded separately so a missing permission or an unavailable projection endpoint degrades those sections only
        // — the core Account Details page still renders.
        await PopulateRelatedProjectionsAsync(id, overview);

        return View($"{ViewRoot}/Details.cshtml", overview);
    }

    /// <summary>Fills the Account 360 related-contacts / related-accounts sections. Each is gated by its own catalog
    /// permission (defence-in-depth) and fails soft: unauthorized → section message, endpoint down → dependency message,
    /// 200-empty → empty state. Never fabricates data.</summary>
    private async Task PopulateRelatedProjectionsAsync(Guid accountId, AccountOverviewViewModel overview)
    {
        overview.RelatedContactsAuthorized = PermissionClaims.HasPermission(User, RelatedContactReadPermission);
        overview.RelatedContactsCanManage = PermissionClaims.HasPermission(User, RelatedContactManagePermission);
        IReadOnlyList<AccountRelatedContactViewModel>? relatedContacts = null;
        if (overview.RelatedContactsAuthorized)
        {
            relatedContacts = await LoadRelatedContactsAsync(accountId);
            overview.RelatedContacts = relatedContacts?.ToList() ?? [];
            overview.RelatedContactsUnavailable = relatedContacts is null;
        }

        // Golden Slim: the Related Contacts create/edit surface is a canvas rendered inside this page, so its options
        // are loaded with the page. ReportsTo carries every already-linked contact; the edited contact is excluded
        // client-side and the backend rejects self-report / cycles regardless. The projection fetched just above is
        // handed over so the manager list does not re-request the same endpoint.
        if (overview.RelatedContactsCanManage)
        {
            var contactForm = new AccountContactLinkEditViewModel { AccountId = accountId, AccountName = overview.Account?.AccountName };
            await PopulateContactLinkOptionsAsync(contactForm, relatedContacts);
            overview.ContactLinkForm = contactForm;
        }

        // MOD-0151 current territory coverage (read-only projection). Gated by crm.territory.model.read and fails
        // soft: unauthorized → section message, endpoint down → dependency message, 200-empty → empty state.
        overview.TerritoryCoverageAuthorized = PermissionClaims.HasPermission(User, TerritoryCoverageReadPermission);
        if (overview.TerritoryCoverageAuthorized)
        {
            var assignments = await LoadTerritoryAssignmentsAsync(accountId);
            overview.TerritoryAssignments = assignments?.ToList() ?? [];
            overview.TerritoryCoverageUnavailable = assignments is null;
        }

        overview.RelatedAccountsAuthorized = PermissionClaims.HasPermission(User, RelatedAccountReadPermission);
        overview.RelatedAccountsCanManage = PermissionClaims.HasPermission(User, RelatedAccountManagePermission);
        if (overview.RelatedAccountsAuthorized)
        {
            var accounts = await LoadRelatedAccountsAsync(accountId);
            overview.RelatedAccounts = accounts?.ToList() ?? [];
            overview.RelatedAccountsUnavailable = accounts is null;
        }

        // Golden Slim: the Related Accounts create/edit surface is an offcanvas rendered inside this page, so its
        // reference options are loaded with the page (not by a separate form route).
        if (overview.RelatedAccountsCanManage)
        {
            var form = new AccountRelationshipEditViewModel { AccountId = accountId, AccountName = overview.Account?.AccountName };
            await PopulateRelationshipOptionsAsync(form);
            overview.RelationshipForm = form;
        }
    }

    /// <summary>MOD-0150 FU03 Account 360 Related Contacts projection. Returns null on any non-success/error (controlled
    /// dependency → caller shows a message), an empty list when the projection is reachable but empty.</summary>
    private async Task<IReadOnlyList<AccountRelatedContactViewModel>?> LoadRelatedContactsAsync(Guid accountId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/related-contacts");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Related contacts for {AccountId} returned {Status}; rendering dependency message.",
                    accountId, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<List<AccountRelatedContactViewModel>>>(_jsonOptions);
            return payload?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Related contacts load failed for {AccountId}.", accountId);
            return null;
        }
    }

    /// <summary>MOD-0150 FU04 Account 360 Related Accounts projection (inverse label pre-resolved). Returns null on any
    /// non-success/error (controlled dependency), an empty list when reachable but empty.</summary>
    private async Task<IReadOnlyList<AccountRelatedAccountViewModel>?> LoadRelatedAccountsAsync(Guid accountId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/related-accounts");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Related accounts for {AccountId} returned {Status}; rendering dependency message.",
                    accountId, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<List<AccountRelatedAccountViewModel>>>(_jsonOptions);
            return payload?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Related accounts load failed for {AccountId}.", accountId);
            return null;
        }
    }

    /// <summary>MOD-0151 territory assignment history (current + ended, newest first). Returns null on any
    /// non-success/error (controlled dependency → caller shows a message), an empty list when reachable but the account
    /// has never been assigned. The current row(s) are flagged client-side from status + effective window + endedAt.</summary>
    private async Task<IReadOnlyList<TerritoryCoverageAssignmentViewModel>?> LoadTerritoryAssignmentsAsync(Guid accountId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/territory-assignments");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Territory assignments for {AccountId} returned {Status}; rendering dependency message.",
                    accountId, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<TerritoryAssignmentListModel>>(_jsonOptions);
            return payload?.Data?.Items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory assignments load failed for {AccountId}.", accountId);
            return null;
        }
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

        var accountTypes = await LoadReferenceOptionsAsync(AccountTypeSetCode);
        var statuses = await LoadReferenceOptionsAsync(AccountStatusSetCode);

        return Json(new
        {
            accountTypes = accountTypes.Select(x => new { value = x.Value, text = x.Text }),
            statuses = statuses.Select(x => new { value = x.Value, text = x.Text })
        });
    }

    /// <summary>Country-scope + territory-node filter source for the Accounts grid. Sourced from MOD-0151 Territory
    /// Management (territory models carry the FU02A CountryScope; nodes belong to a model), so the chips list EVERY
    /// distinct country scope / node — not only the ones assigned accounts happen to have. Cascade metadata (each node's
    /// country scope) is included so the Territory Node chip can narrow to the selected country. Gated by
    /// crm.territory.model.read at the Gateway; returns empty lists (never a hardcoded fallback) when unavailable.</summary>
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

        // node id → name + owning model's country scope (for the cascade). The chip value is the node id (the backend
        // filters coverage by node id precisely); the name is display only. Distinct on the node id.
        var nodeTriples = new List<(Guid Id, string Name, string? CountryScope)>();
        foreach (var model in models)
        {
            var hierarchy = await LoadTerritoryNodesAsync(model.Id);
            if (hierarchy is null)
            {
                continue;
            }

            foreach (var node in hierarchy.Nodes.Where(n => n.Id != Guid.Empty && !string.IsNullOrWhiteSpace(n.Name)))
            {
                nodeTriples.Add((node.Id, node.Name.Trim(), model.CountryScope?.Trim()));
            }
        }

        var nodes = nodeTriples
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new { id = x.Id, name = x.Name, countryScope = x.CountryScope })
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

    private async Task PopulateOptionsAsync(AccountEditViewModel model)
    {
        model.AccountTypeOptions = await LoadReferenceOptionsAsync(AccountTypeSetCode);
        model.AccountStatusOptions = await LoadReferenceOptionsAsync(AccountStatusSetCode);
        model.AccountCategoryOptions = await LoadReferenceOptionsAsync(AccountCategorySetCode);
        model.CountryOptions = await LoadReferenceOptionsAsync(CountrySetCode);
        model.CityOptions = await LoadReferenceOptionsAsync(CitySetCode);
        model.DistrictOptions = await LoadReferenceOptionsAsync(DistrictSetCode);
        model.ParentAccountOptions = await LoadParentAccountOptionsAsync(model.Id);

        // account-category is optional in the module pack; only the required sets gate the form.
        if (model.AccountTypeOptions.Count == 0 || model.AccountStatusOptions.Count == 0)
        {
            model.ReferenceDependencyMessage = _localizer["ReferenceDataUnavailable"].Value;
        }
    }

    /// <summary>
    /// Reads MOD-0048 published values through the Gateway. Returns an EMPTY list when unavailable — never a
    /// hardcoded fallback; the caller surfaces a controlled dependency message instead.
    /// </summary>
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
                _logger.LogWarning(
                    "Reference set '{SetCode}' returned {Status}; rendering without options (controlled dependency).",
                    setCode, response.StatusCode);
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
                .Select(x => new ReferenceOptionViewModel(
                    x.Value!,
                    string.IsNullOrWhiteSpace(x.Text) ? x.Value! : x.Text!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' load failed; rendering without options.", setCode);
            return [];
        }
    }

    /// <summary>Parent candidates for the hierarchy. The record being edited is excluded to avoid an obvious self-parent;
    /// full cycle prevention stays a backend invariant.</summary>
    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadParentAccountOptionsAsync(Guid? excludeId)
    {
        if (!AddAuthHeaders())
            return [];

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts?page=1&pageSize=200");
            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<AccountPagedResult<AccountListItemViewModel>>>(_jsonOptions);

            var items = payload?.Data?.Items ?? [];

            return items
                .Where(x => excludeId is null || x.Id != excludeId)
                .OrderBy(x => x.AccountName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ReferenceOptionViewModel(
                    x.Id.ToString(),
                    string.IsNullOrWhiteSpace(x.AccountCode) ? x.AccountName : $"{x.AccountCode} — {x.AccountName}"))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent account options load failed.");
            return [];
        }
    }

    private async Task<AccountDetailViewModel?> LoadDetailAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<AccountDetailViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account load failed for {AccountId}.", id);
            return null;
        }
    }

    private async Task<AccountOverviewViewModel?> LoadOverviewAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{id}/overview");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<AccountOverviewViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account overview load failed for {AccountId}.", id);
            return null;
        }
    }

    private static AccountEditViewModel ToEditModel(AccountDetailViewModel model) => new()
    {
        Id = model.Id,
        AccountName = model.AccountName,
        AccountCode = model.AccountCode,
        AccountType = model.AccountType,
        AccountCategory = model.AccountCategory,
        ParentAccountId = model.ParentAccountId,
        Status = model.Status,
        CountryRef = model.CountryRef,
        CityRef = model.CityRef,
        DistrictRef = model.DistrictRef,
        AddressLine = model.AddressLine,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        ResponsiblePersonName = model.ResponsiblePersonName,
        ResponsiblePersonPhone = model.ResponsiblePersonPhone,
        ResponsiblePersonEmail = model.ResponsiblePersonEmail,
        ExternalReference = model.ExternalReferences.FirstOrDefault()?.ExternalId,
        Notes = model.Notes,
        LogoDataUri = model.LogoDataUri
    };

    private static AccountSavePayload ToPayload(AccountEditViewModel model) => new()
    {
        AccountName = model.AccountName,
        AccountCode = string.IsNullOrWhiteSpace(model.AccountCode) ? null : model.AccountCode.Trim(),
        AccountType = model.AccountType,
        AccountCategory = model.AccountCategory,
        ParentAccountId = model.ParentAccountId,
        Status = model.Status,
        CountryRef = model.CountryRef,
        CityRef = model.CityRef,
        DistrictRef = model.DistrictRef,
        AddressLine = model.AddressLine,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        ResponsiblePersonName = model.ResponsiblePersonName,
        ResponsiblePersonPhone = model.ResponsiblePersonPhone,
        ResponsiblePersonEmail = model.ResponsiblePersonEmail,
        Notes = model.Notes,
        LogoDataUri = string.IsNullOrWhiteSpace(model.LogoDataUri) ? null : model.LogoDataUri,
        ExternalReference = string.IsNullOrWhiteSpace(model.ExternalReference)
            ? null
            : new ExternalReferenceInputPayload { ExternalId = model.ExternalReference.Trim() }
    };

    // =====================================================================================================
    // Relationship management (Add / Edit / End). Gateway-only, permission-gated (crm.account-contact.manage /
    // crm.account-relationship.manage). Historical lifecycle: "End" NEVER deletes — it PUTs Status=ended + ValidTo so
    // downstream sales/visit/order/forecast context is preserved. The DELETE endpoints are never called from the UI.
    // =====================================================================================================

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)) : null;

    // ---- Related Contacts management ----

    /// <summary>Legacy standalone Add page. The Golden Slim surface for this section is the Related Contacts canvas on
    /// Details (≤8 form fields ⇒ separate create/edit pages are forbidden), so the route now only re-opens it.</summary>
    [HttpGet("{accountId:guid}/Contacts/Add")]
    public IActionResult AddContactLink(Guid accountId)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, contactLink = "new" });
    }

    [HttpPost("{accountId:guid}/Contacts/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContactLink(Guid accountId, [FromForm] AccountContactLinkEditViewModel model)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.LinkId = null;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            try
            {
                var payload = new LinkContactPayload
                {
                    ContactId = model.ContactId,
                    RoleCode = model.RoleCode,
                    IsPrimary = model.IsPrimary,
                    ValidFrom = ToOffset(model.ValidFrom),
                    ValidTo = ToOffset(model.ValidTo),
                    Notes = model.Notes,
                    CrossCountryReason = string.IsNullOrWhiteSpace(model.CrossCountryReason) ? null : model.CrossCountryReason.Trim(),
                    ReportsToContactId = model.ReportsToContactId
                };
                var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/contacts", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Add contact link failed for {AccountId}.", accountId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    /// <summary>Edit prefill + End summary source for the Golden Slim canvas. The 360 projection row carries no
    /// validity window or CrossCountryReason, so the canvas reads the link itself before opening.</summary>
    [HttpGet("{accountId:guid}/Contacts/{linkId:guid}/Json")]
    public async Task<IActionResult> GetContactLinkJson(Guid accountId, Guid linkId)
    {
        if (RequireJson(RelatedContactManagePermission) is { } denied)
            return denied;

        var link = await LoadContactLinkAsync(accountId, linkId);
        if (link is null)
            return Json(new { success = false, errors = new[] { _sharedLocalizer["GatewayError"].Value } });

        return Json(new
        {
            success = true,
            data = new
            {
                id = link.Id,
                contactId = link.ContactId,
                roleCode = link.RoleCode,
                isPrimary = link.IsPrimary,
                status = link.Status,
                validFrom = link.ValidFrom?.UtcDateTime.ToString("yyyy-MM-dd"),
                validTo = link.ValidTo?.UtcDateTime.ToString("yyyy-MM-dd"),
                notes = link.Notes,
                reportsToContactId = link.ReportsToContactId
            }
        });
    }

    /// <summary>Legacy standalone Edit page — superseded by the Details canvas (see <see cref="AddContactLink"/>).</summary>
    [HttpGet("{accountId:guid}/Contacts/{linkId:guid}/Edit")]
    public IActionResult EditContactLink(Guid accountId, Guid linkId)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, contactLink = linkId });
    }

    [HttpPost("{accountId:guid}/Contacts/{linkId:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditContactLink(Guid accountId, Guid linkId, [FromForm] AccountContactLinkEditViewModel model)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.LinkId = linkId;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            try
            {
                var payload = new UpdateContactLinkPayload
                {
                    RoleCode = model.RoleCode,
                    IsPrimary = model.IsPrimary,
                    ValidFrom = ToOffset(model.ValidFrom),
                    ValidTo = ToOffset(model.ValidTo),
                    Notes = model.Notes,
                    CrossCountryReason = string.IsNullOrWhiteSpace(model.CrossCountryReason) ? null : model.CrossCountryReason.Trim(),
                    ReportsToContactId = model.ReportsToContactId
                    // Status intentionally omitted — Edit never changes lifecycle; use End Link.
                };
                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/contacts/{linkId}", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit contact link failed for {AccountId}/{LinkId}.", accountId, linkId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    /// <summary>Legacy standalone End page — superseded by the End canvas on Details.</summary>
    [HttpGet("{accountId:guid}/Contacts/{linkId:guid}/End")]
    public IActionResult EndContactLink(Guid accountId, Guid linkId)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, endContactLink = linkId });
    }

    [HttpPost("{accountId:guid}/Contacts/{linkId:guid}/End")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndContactLink(Guid accountId, Guid linkId, [FromForm] AccountContactLinkEndViewModel model)
    {
        if (RequirePage(RelatedContactManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.LinkId = linkId;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            // Load the current link to preserve RoleCode/IsPrimary while transitioning Status→ended (never delete).
            var link = await LoadContactLinkAsync(accountId, linkId);
            if (link is null)
            {
                ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
                return RelatedSectionSaveFailed(accountId);
            }

            try
            {
                var payload = new UpdateContactLinkPayload
                {
                    RoleCode = link.RoleCode,
                    IsPrimary = link.IsPrimary,
                    ValidFrom = link.ValidFrom,
                    ValidTo = ToOffset(model.EndDate),
                    Notes = string.IsNullOrWhiteSpace(model.Notes) ? link.Notes : model.Notes,
                    Status = EndedStatus
                };
                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/contacts/{linkId}", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _localizer["LinkEndedMessage"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "End contact link failed for {AccountId}/{LinkId}.", accountId, linkId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    // ---- Related Accounts management ----

    /// <summary>Legacy standalone Add page. The Golden Slim surface for this module is the Related Accounts offcanvas
    /// on Details (≤8 form fields ⇒ separate create/edit pages are forbidden), so the route now only re-opens it.</summary>
    [HttpGet("{accountId:guid}/Relationships/Add")]
    public IActionResult AddRelationship(Guid accountId)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, relationship = "new" });
    }

    [HttpPost("{accountId:guid}/Relationships/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRelationship(Guid accountId, [FromForm] AccountRelationshipEditViewModel model)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.RelationshipId = null;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            try
            {
                var payload = new CreateRelationshipPayload
                {
                    TargetAccountId = model.TargetAccountId,
                    RelationshipType = model.RelationshipType,
                    Status = model.Status,
                    ValidFrom = ToOffset(model.ValidFrom),
                    ValidTo = ToOffset(model.ValidTo),
                    Notes = model.Notes,
                    CrossCountryReason = string.IsNullOrWhiteSpace(model.CrossCountryReason) ? null : model.CrossCountryReason.Trim()
                };
                var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/relationships", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Add relationship failed for {AccountId}.", accountId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    /// <summary>Edit prefill source for the Golden Slim offcanvas. The 360 projection row carries no
    /// <c>CrossCountryReason</c>, so the offcanvas reads the relationship itself before opening.</summary>
    [HttpGet("{accountId:guid}/Relationships/{relationshipId:guid}/Json")]
    public async Task<IActionResult> GetRelationshipJson(Guid accountId, Guid relationshipId)
    {
        if (RequireJson(RelatedAccountManagePermission) is { } denied)
            return denied;

        var relationship = await LoadRelationshipAsync(accountId, relationshipId);
        if (relationship is null)
            return Json(new { success = false, errors = new[] { _sharedLocalizer["GatewayError"].Value } });

        return Json(new
        {
            success = true,
            data = new
            {
                id = relationship.Id,
                targetAccountId = relationship.TargetAccountId,
                relationshipType = relationship.RelationshipType,
                status = relationship.Status,
                validFrom = relationship.ValidFrom?.UtcDateTime.ToString("yyyy-MM-dd"),
                validTo = relationship.ValidTo?.UtcDateTime.ToString("yyyy-MM-dd"),
                notes = relationship.Notes
            }
        });
    }

    /// <summary>Legacy standalone Edit page — superseded by the Details offcanvas (see <see cref="AddRelationship"/>).</summary>
    [HttpGet("{accountId:guid}/Relationships/{relationshipId:guid}/Edit")]
    public IActionResult EditRelationship(Guid accountId, Guid relationshipId)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, relationship = relationshipId });
    }

    [HttpPost("{accountId:guid}/Relationships/{relationshipId:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRelationship(Guid accountId, Guid relationshipId, [FromForm] AccountRelationshipEditViewModel model)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.RelationshipId = relationshipId;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            try
            {
                var payload = new UpdateRelationshipPayload
                {
                    RelationshipType = model.RelationshipType,
                    Status = model.Status,
                    ValidFrom = ToOffset(model.ValidFrom),
                    ValidTo = ToOffset(model.ValidTo),
                    Notes = model.Notes,
                    CrossCountryReason = string.IsNullOrWhiteSpace(model.CrossCountryReason) ? null : model.CrossCountryReason.Trim()
                };
                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/relationships/{relationshipId}", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit relationship failed for {AccountId}/{RelationshipId}.", accountId, relationshipId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    /// <summary>Both 360 sections (Related Contacts, Related Accounts) submit their canvases over AJAX and stay on
    /// Details; a non-AJAX post (JS disabled) keeps the
    /// original redirect. Success/failure shapes mirror the golden reference (<c>{ success, errors[] }</c>).</summary>
    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private IActionResult RelatedSectionSaved(Guid accountId) =>
        IsAjaxRequest()
            ? Json(new { success = true })
            : RedirectToAction(nameof(Details), new { id = accountId });

    private IActionResult RelatedSectionSaveFailed(Guid accountId)
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? _sharedLocalizer["GatewayError"].Value : e.ErrorMessage)
            .ToList();

        if (IsAjaxRequest())
            return Json(new { success = false, errors });

        TempData["ErrorMessage"] = errors.Count > 0 ? errors[0] : _sharedLocalizer["GatewayError"].Value;
        return RedirectToAction(nameof(Details), new { id = accountId });
    }

    /// <summary>Legacy standalone End page — superseded by the End canvas on Details (see <see cref="AddRelationship"/>).</summary>
    [HttpGet("{accountId:guid}/Relationships/{relationshipId:guid}/End")]
    public IActionResult EndRelationship(Guid accountId, Guid relationshipId)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        return RedirectToAction(nameof(Details), new { id = accountId, endRelationship = relationshipId });
    }

    [HttpPost("{accountId:guid}/Relationships/{relationshipId:guid}/End")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndRelationship(Guid accountId, Guid relationshipId, [FromForm] AccountRelationshipEndViewModel model)
    {
        if (RequirePage(RelatedAccountManagePermission) is { } denied)
            return denied;

        model.AccountId = accountId;
        model.RelationshipId = relationshipId;

        if (ModelState.IsValid && AddAuthHeaders())
        {
            var relationship = await LoadRelationshipAsync(accountId, relationshipId);
            if (relationship is null)
            {
                ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
                return RelatedSectionSaveFailed(accountId);
            }

            try
            {
                var payload = new UpdateRelationshipPayload
                {
                    RelationshipType = relationship.RelationshipType,
                    Status = EndedStatus,
                    ValidFrom = relationship.ValidFrom,
                    ValidTo = ToOffset(model.EndDate),
                    Notes = string.IsNullOrWhiteSpace(model.Notes) ? relationship.Notes : model.Notes
                };
                var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/relationships/{relationshipId}", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = _localizer["RelationshipEndedMessage"].Value;
                    return RelatedSectionSaved(accountId);
                }

                AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "End relationship failed for {AccountId}/{RelationshipId}.", accountId, relationshipId);
                AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
            }
        }
        else if (ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
        }

        return RelatedSectionSaveFailed(accountId);
    }

    // ---- management loaders ----

    /// <summary>Fills the Related Contacts canvas options. <paramref name="knownRelatedContacts"/> lets a caller that has
    /// already fetched the related-contacts projection (the Details page renders it as a table) hand it over instead of
    /// forcing a second identical gateway call; pass null when the projection is not on hand.</summary>
    private async Task PopulateContactLinkOptionsAsync(
        AccountContactLinkEditViewModel model,
        IReadOnlyList<AccountRelatedContactViewModel>? knownRelatedContacts = null)
    {
        model.RoleOptions = await LoadReferenceOptionsAsync(ContactRoleSetCode);
        model.ContactOptions = await LoadContactOptionsAsync();
        model.ReportsToOptions = await LoadReportsToOptionsAsync(model.AccountId, model.ContactId, knownRelatedContacts);
        if (model.RoleOptions.Count == 0)
        {
            model.ReferenceDependencyMessage = _localizer["ReferenceDataUnavailable"].Value;
        }
    }

    /// <summary>Manager candidates for the in-account hierarchy = contacts ALREADY linked to this account (org chart is
    /// per-account), excluding the contact being linked. Sourced from the existing related-contacts projection — no new
    /// endpoint, no local fallback. Empty when none/unavailable.</summary>
    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadReportsToOptionsAsync(
        Guid accountId,
        Guid excludeContactId,
        IReadOnlyList<AccountRelatedContactViewModel>? knownRelatedContacts = null)
    {
        var related = knownRelatedContacts ?? await LoadRelatedContactsAsync(accountId);
        if (related is null)
            return [];

        return related
            .Where(c => c.ContactId != excludeContactId)
            .GroupBy(c => c.ContactId)
            .Select(g => g.First())
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ReferenceOptionViewModel(c.ContactId.ToString(), c.DisplayName))
            .ToList();
    }

    private async Task PopulateRelationshipOptionsAsync(AccountRelationshipEditViewModel model)
    {
        model.RelationshipTypeOptions = await LoadReferenceOptionsAsync(RelationshipTypeSetCode);
        model.RelationshipStatusOptions = await LoadReferenceOptionsAsync(RelationshipStatusSetCode);
        model.TargetAccountOptions = await LoadParentAccountOptionsAsync(model.AccountId);
        if (model.RelationshipTypeOptions.Count == 0 || model.RelationshipStatusOptions.Count == 0)
        {
            model.ReferenceDependencyMessage = _localizer["ReferenceDataUnavailable"].Value;
        }
    }

    /// <summary>Contact picker source. Uses the existing contacts list (Gateway) — no new endpoint, no local fallback.</summary>
    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadContactOptionsAsync()
    {
        if (!AddAuthHeaders())
            return [];

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/contacts?page=1&pageSize=200");
            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<AccountPagedResult<ContactListItemViewModel>>>(_jsonOptions);
            var items = payload?.Data?.Items ?? [];
            return items
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ReferenceOptionViewModel(x.Id.ToString(), x.DisplayName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contact options load failed.");
            return [];
        }
    }

    private async Task<AccountContactLinkDetailViewModel?> LoadContactLinkAsync(Guid accountId, Guid linkId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/contacts/{linkId}");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<AccountContactLinkDetailViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact link load failed for {AccountId}/{LinkId}.", accountId, linkId);
            return null;
        }
    }

    private async Task<AccountRelationshipDetailViewModel?> LoadRelationshipAsync(Guid accountId, Guid relationshipId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts/{accountId}/relationships/{relationshipId}");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<AccountRelationshipDetailViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relationship load failed for {AccountId}/{RelationshipId}.", accountId, relationshipId);
            return null;
        }
    }

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

    /// <summary>Page-action permission gate. Missing → bare 403, re-executed to the friendly /Home/Status/403 page
    /// (repo standard; a ForbidResult would 302 to the cookie scheme's — currently unmapped — AccessDenied path).</summary>
    private IActionResult? RequirePage(string permission) =>
        PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    /// <summary>JSON-action permission gate. Missing → 403 with a message body (repo standard for API actions).</summary>
    private IActionResult? RequireJson(string permission) =>
        PermissionClaims.HasPermission(User, permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
