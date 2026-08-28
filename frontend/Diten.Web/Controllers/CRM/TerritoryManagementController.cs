using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Views.CRM.TerritoryManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0151 FU02 — Territory Hierarchy UI / Territory Model Viewer. Golden Reference Compact server-rendered surfaces
/// over the FU01 backend (TerritoryModel + TerritoryNode). All traffic goes through the Gateway (5000); CrmService
/// (5061) is never called directly. Reference dropdowns are sourced live from MOD-0048 published values — no local
/// fallback list. Authorization is defence-in-depth: the tenant-shell menu is UX-gated by <c>crm.territory.read</c>,
/// this controller enforces a per-action MVC guard, and the authoritative enforcement stays in CrmService
/// (<c>[HasPermission("crm.territory.*")]</c>). FU02 exposes ONLY model+node draft management — no assignment, rule,
/// resource, workflow, evidence, import/export or delete surface (later FUs).
/// </summary>
[Authorize]
[Route("CRM/TerritoryManagement")]
public sealed class TerritoryManagementController : Controller
{
    private const string ReadPermission = "crm.territory.read";
    private const string ModelReadPermission = "crm.territory.model.read";
    private const string ModelManagePermission = "crm.territory.model.manage";
    private const string NodeReadPermission = "crm.territory.node.read";
    private const string NodeManagePermission = "crm.territory.node.manage";

    private const string TerritoryLevelSetCode = "territory-level";
    private const string PlanningCenterTypeSetCode = "planning-center-type";
    private const string MicroZoneLevel = "microzone";
    private const string ViewRoot = "~/Views/CRM/TerritoryManagement";

    // FU02A scope selectors — MOD-0048 published-values only, NO hardcoded fallback.
    //  - Country: the COUNTRY_CODES platform set (Global scope). Territory only; MOD-0149 Accounts/Contacts still bind
    //    "country". territory_models.CountryScope was migrated lowercase->UPPERCASE (2026-08-28), so existing rows match.
    //    F-COUNTRY-SOT (open, non-blocking): COUNTRY_CODES currently holds a 6-code set (TR BY UZ TM GE AZ) — republish
    //    it as the COMPLETE uppercase ISO list to offer more than 6 countries to NEW models. LoadReferenceOptionsAsync
    //    drops scope_key on the service's global-refusal signal (COUNTRY_CODES is Global; "country" was tenant-scoped).
    //  - Business Unit: the ACTUAL business-unit VALUE set (alpha/beta), NOT "business-scope-type" (which is the
    //    classification/type set). scopeType is fixed to "business-unit" when serializing to the Gateway.
    private const string CountrySetCode = "COUNTRY_CODES";
    private const string BusinessUnitSetCode = "business-unit";
    private const string BusinessUnitScopeType = "business-unit";

    // FU03 assignment rules — reference-driven selectors, no hardcoded rule-type/policy list.
    private const string TerritoryRuleTypeSetCode = "territory-rule-type";
    private const string TerritoryConflictPolicySetCode = "territory-conflict-policy";

    // FU04 resource assignments — role/coverage selectors are reference-driven too.
    private const string TerritoryResourceRoleSetCode = "territory-resource-role";
    private const string TerritoryCoverageScopeSetCode = "territory-coverage-scope";

    // FU03 rule criteria — the SAME sets the MOD-0149 Account form binds to, so a rule can only reference values an
    // account can actually hold.
    private const string CitySetCode = "city";
    private const string DistrictSetCode = "district";
    private const string AccountTypeSetCode = "account-type";
    private const string AccountCategorySetCode = "account-category";
    private const string AccountStatusSetCode = "account-status";

    /// <summary>The published rule types the FU03 engine evaluates. Offering a published-but-unimplemented type in
    /// the form would only produce a backend 400, so the selector is narrowed to these.</summary>
    private static readonly string[] Fu03SupportedRuleTypes = ["geography", "account-type", "account-list"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<TerritoryManagementResources> _localizer;
    private readonly ILogger<TerritoryManagementController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TerritoryManagementController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<TerritoryManagementResources> localizer,
        ILogger<TerritoryManagementController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    // ---- Landing (contract + model list) ----

    // FU09A live/read-only diagnostics. These same-origin JSON proxies reuse the HttpOnly access token and forward
    // exclusively through Gateway 5000. They add no page, menu, mutation, route planning or visit planning surface.
    [HttpGet("Readiness/ContractJson")]
    [HttpGet("Fu09a/Contract")]
    public Task<IActionResult> ReadinessContractJson(CancellationToken cancellationToken)
        => ProxyReadinessGetAsync("/api/crm/territory-management/contract", cancellationToken);

    [HttpGet("Readiness/Accounts/{accountId:guid}/CoverageJson")]
    [HttpGet("Fu09a/Accounts/{accountId:guid}/Coverage")]
    public Task<IActionResult> AccountCoverageReadinessJson(
        Guid accountId, DateTimeOffset? effectiveAt, string? businessUnit, CancellationToken cancellationToken)
        => ProxyReadinessGetAsync(
            $"/api/crm/territory-management/readiness/accounts/{accountId}/coverage-readiness" +
            QueryString.Create(new Dictionary<string, string?>
            {
                ["effectiveAt"] = effectiveAt?.ToString("O"),
                ["businessUnit"] = businessUnit
            }).ToUriComponent(), cancellationToken);

    [HttpGet("Readiness/Contacts/{contactId:guid}/CoverageJson")]
    [HttpGet("Fu09a/Contacts/{contactId:guid}/Coverage")]
    public Task<IActionResult> ContactCoverageReadinessJson(
        Guid contactId, DateTimeOffset? effectiveAt, string? businessUnit, string? date, string? weekday,
        CancellationToken cancellationToken)
        => ProxyReadinessGetAsync(
            $"/api/crm/territory-management/readiness/contacts/{contactId}/territory-coverage" +
            QueryString.Create(new Dictionary<string, string?>
            {
                ["effectiveAt"] = effectiveAt?.ToString("O"),
                ["businessUnit"] = businessUnit,
                ["date"] = date,
                ["weekday"] = weekday
            }).ToUriComponent(), cancellationToken);

    [HttpGet("Readiness/RouteCandidatesJson")]
    [HttpGet("Fu09a/Candidates")]
    public Task<IActionResult> RouteCandidatesReadinessJson(
        DateTimeOffset? effectiveAt, string? businessUnit, Guid? territoryModelId, Guid? territoryNodeId,
        string? resourceId, Guid? accountId, Guid? contactId, string? date, string? weekday,
        bool includeNonReady, CancellationToken cancellationToken)
        => ProxyReadinessGetAsync(
            "/api/crm/territory-management/readiness/route-candidates" +
            QueryString.Create(new Dictionary<string, string?>
            {
                ["effectiveAt"] = effectiveAt?.ToString("O"),
                ["businessUnit"] = businessUnit,
                ["territoryModelId"] = territoryModelId?.ToString(),
                ["territoryNodeId"] = territoryNodeId?.ToString(),
                ["resourceId"] = resourceId,
                ["accountId"] = accountId?.ToString(),
                ["contactId"] = contactId?.ToString(),
                ["date"] = date,
                ["weekday"] = weekday,
                ["includeNonReady"] = includeNonReady.ToString().ToLowerInvariant()
            }).ToUriComponent(), cancellationToken);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        // The Territory Models list is now rendered client-side by the Golden Reference DataTable
        // (index.js pulls /api/crm/territory-models over the Gateway), so the landing view only needs
        // the permission flags for UX gating. Contract readiness is surfaced elsewhere, not on this grid.
        var vm = new TerritoryIndexPageViewModel
        {
            CanManageModel = PermissionClaims.HasPermission(User, ModelManagePermission),
            CanReadNode = PermissionClaims.HasPermission(User, NodeReadPermission)
        };

        return View($"{ViewRoot}/Index.cshtml", vm);
    }

    // ---- TerritoryModel create/edit ----

    [HttpGet("Models/Create")]
    public async Task<IActionResult> CreateModel()
    {
        if (RequirePage(ModelManagePermission) is { } denied)
            return denied;

        var model = new TerritoryModelEditViewModel
        {
            ModelCode = CreateDefaultModelCode()
        };
        await PopulateModelScopeOptionsAsync(model);
        return View($"{ViewRoot}/ModelForm.cshtml", model);
    }

    [HttpPost("Models/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModel([FromForm] TerritoryModelEditViewModel model)
    {
        if (RequirePage(ModelManagePermission) is { } denied)
        {
            return denied;
        }

        await PopulateModelScopeOptionsAsync(model);

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/ModelForm.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/ModelForm.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models", ToModelPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory model create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/ModelForm.cshtml", model);
    }

    [HttpGet("Models/{id:guid}/Edit")]
    public async Task<IActionResult> EditModel(Guid id)
    {
        if (RequirePage(ModelManagePermission) is { } denied)
        {
            return denied;
        }

        var detail = await LoadModelDetailAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        var model = ToModelEdit(detail);
        await PopulateModelScopeOptionsAsync(model);
        return View($"{ViewRoot}/ModelForm.cshtml", model);
    }

    [HttpPost("Models/{id:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModel(Guid id, [FromForm] TerritoryModelEditViewModel model)
    {
        if (RequirePage(ModelManagePermission) is { } denied)
        {
            return denied;
        }

        model.Id = id;
        await PopulateModelScopeOptionsAsync(model);

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/ModelForm.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/ModelForm.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{id}", ToModelPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory model edit failed for {ModelId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/ModelForm.cshtml", model);
    }

    // ---- TerritoryModel slim offcanvas (AJAX create/edit + edit-load) ----
    // These back the Golden Reference "slim" offcanvas on the landing grid: JSON in / JSON out, no page
    // navigation. The full-page ModelForm actions above stay for direct links; both proxy the same Gateway.

    [HttpGet("Models/{id:guid}/Json")]
    public async Task<IActionResult> GetModelJson(Guid id)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false });

        var d = await LoadModelDetailAsync(id);
        if (d is null)
            return Json(new { success = false });

        return Json(new
        {
            success = true,
            data = new
            {
                id = d.Id,
                modelCode = d.ModelCode,
                name = d.Name,
                status = d.Status,
                versionNumber = d.VersionNumber,
                countryScope = d.CountryScope,
                // FU02A: business-unit scope codes persisted by the backend, for edit prefill.
                businessUnitScopes = d.BusinessScopes
                    .Where(s => string.Equals(s.ScopeType, BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.ScopeCode)
                    .ToArray(),
                effectiveFrom = d.EffectiveFrom.UtcDateTime.ToString("yyyy-MM-dd"),
                effectiveTo = d.EffectiveTo?.UtcDateTime.ToString("yyyy-MM-dd"),
                changeReason = d.ChangeReason
            }
        });
    }

    /// <summary>FU02A scope selector source. Country + Business Unit options come from MOD-0048 published-values
    /// through the Gateway (tenant scope_key). Returns EMPTY + a not-ready flag when a set is unpublished — never a
    /// hardcoded country / business-unit list. <c>business-scope-type</c> (the classification set) is intentionally
    /// NOT surfaced here; the user selects real business-unit values, not scope types.</summary>
    [HttpGet("Models/lookups")]
    public async Task<IActionResult> ModelLookups()
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new
            {
                countries = Array.Empty<object>(), countryReady = false,
                businessUnits = Array.Empty<object>(), businessUnitReady = false
            });

        var countries = await LoadReferenceOptionsAsync(CountrySetCode);
        var businessUnits = await LoadReferenceOptionsAsync(BusinessUnitSetCode);

        return Json(new
        {
            countries = countries.Select(x => new { value = x.Value, text = x.Text }),
            countryReady = countries.Count > 0,
            businessUnits = businessUnits.Select(x => new { value = x.Value, text = x.Text }),
            businessUnitReady = businessUnits.Count > 0
        });
    }

    [HttpPost("Models/CreateJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModelJson([FromForm] TerritoryModelEditViewModel model)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models", ToModelPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory model create (json) failed.");
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("Models/{id:guid}/EditJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModelJson(Guid id, [FromForm] TerritoryModelEditViewModel model)
    {
        model.Id = id;

        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{id}", ToModelPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory model edit (json) failed for {ModelId}.", id);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    // ---- Model detail / hierarchy viewer ----

    [HttpGet("Models/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        var detail = await LoadModelDetailAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        var vm = new TerritoryModelDetailPageViewModel
        {
            Model = detail,
            CanManageModel = PermissionClaims.HasPermission(User, ModelManagePermission),
            CanManageNode = PermissionClaims.HasPermission(User, NodeManagePermission),
            Contract = await LoadContractAsync()
        };

        if (PermissionClaims.HasPermission(User, NodeReadPermission))
        {
            var hierarchy = await LoadHierarchyAsync(id);
            vm.NodesUnavailable = hierarchy is null;
            vm.Nodes = hierarchy is null ? [] : BuildOrderedTree(hierarchy.Nodes);
        }

        if (vm.CanManageNode)
        {
            vm.NodeLevelOptions = await LoadReferenceOptionsAsync(TerritoryLevelSetCode);
            vm.PlanningCenterTypeOptions = await LoadReferenceOptionsAsync(PlanningCenterTypeSetCode);
            vm.AnchorAccountOptions = await LoadAnchorAccountOptionsAsync(detail.CountryScope);
        }

        return View($"{ViewRoot}/Details.cshtml", vm);
    }

    // ---- TerritoryNode create/edit ----

    [HttpGet("Models/{modelId:guid}/Nodes/Create")]
    public IActionResult CreateNode(Guid modelId)
    {
        if (RequirePage(NodeManagePermission) is { } denied)
        {
            return denied;
        }

        return RedirectToAction(nameof(Details), new { id = modelId });
    }

    [HttpPost("Models/{modelId:guid}/Nodes/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNode(Guid modelId, [FromForm] TerritoryNodeEditViewModel model)
    {
        if (RequirePage(NodeManagePermission) is { } denied)
        {
            return denied;
        }

        model.ModelId = modelId;
        if (!ModelState.IsValid)
        {
            await PopulateNodeOptionsAsync(model);
            return View($"{ViewRoot}/NodeForm.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            await PopulateNodeOptionsAsync(model);
            return View($"{ViewRoot}/NodeForm.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes", ToNodePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Details), new { id = modelId });
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory node create failed for model {ModelId}.", modelId);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        await PopulateNodeOptionsAsync(model);
        return View($"{ViewRoot}/NodeForm.cshtml", model);
    }

    [HttpPost("Models/{modelId:guid}/Nodes/CreateJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNodeJson(Guid modelId, [FromForm] TerritoryNodeEditViewModel model)
    {
        if (!PermissionClaims.HasPermission(User, NodeManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        model.ModelId = modelId;
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });
        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes", ToNodePayload(model), _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory node create (json) failed for model {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpGet("Models/{modelId:guid}/Nodes/{nodeId:guid}/Edit")]
    public IActionResult EditNode(Guid modelId, Guid nodeId)
    {
        if (RequirePage(NodeManagePermission) is { } denied)
        {
            return denied;
        }
        return RedirectToAction(nameof(Details), new { id = modelId });
    }

    [HttpPost("Models/{modelId:guid}/Nodes/{nodeId:guid}/EditJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNodeJson(Guid modelId, Guid nodeId, [FromForm] TerritoryNodeEditViewModel model)
    {
        if (!PermissionClaims.HasPermission(User, NodeManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        model.ModelId = modelId;
        model.Id = nodeId;
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });
        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes/{nodeId}", ToNodePayload(model), _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory node edit (json) failed for {ModelId}/{NodeId}.", modelId, nodeId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("Models/{modelId:guid}/Nodes/{nodeId:guid}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNode(Guid modelId, Guid nodeId, [FromForm] TerritoryNodeEditViewModel model)
    {
        if (RequirePage(NodeManagePermission) is { } denied)
        {
            return denied;
        }

        model.ModelId = modelId;
        model.Id = nodeId;
        if (!ModelState.IsValid)
        {
            await PopulateNodeOptionsAsync(model);
            return View($"{ViewRoot}/NodeForm.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            await PopulateNodeOptionsAsync(model);
            return View($"{ViewRoot}/NodeForm.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes/{nodeId}", ToNodePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = modelId });
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory node edit failed for {ModelId}/{NodeId}.", modelId, nodeId);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        await PopulateNodeOptionsAsync(model);
        return View($"{ViewRoot}/NodeForm.cshtml", model);
    }

    // ======================================================================================================
    // FU03 / FU04 dedicated pages (pack §18 surfaces #6 and #8)
    //
    // Assignment Preview and Resource Assignment are their OWN screens in the pack, not sections of the model
    // viewer. They are reached from the models list Actions menu; the Details page keeps only the hierarchy.
    // ======================================================================================================

    [HttpGet("Models/{id:guid}/AssignmentRules")]
    public async Task<IActionResult> AssignmentRulesPage(Guid id)
    {
        if (RequirePage(ModelReadPermission) is { } denied)
        {
            return denied;
        }

        var vm = await LoadModelPageAsync(id);
        if (vm is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/AssignmentRules.cshtml", vm);
    }

    /// <summary>Preview screen (pack §18 surface #6). Without a ruleId it evaluates the whole model — which is the
    /// only way conflicts can appear, since a conflict is by definition two rules claiming one account for different
    /// nodes. With a ruleId it answers the narrower "what does THIS rule catch?".</summary>
    [HttpGet("Models/{id:guid}/AssignmentRules/Preview")]
    [HttpGet("Models/{id:guid}/AssignmentRules/{ruleId:guid}/Preview")]
    public async Task<IActionResult> AssignmentPreviewPage(Guid id, Guid? ruleId)
    {
        if (RequirePage(ModelReadPermission) is { } denied)
        {
            return denied;
        }

        var vm = await LoadRuleScopedPageAsync(id, ruleId);
        if (vm is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/AssignmentPreview.cshtml", vm);
    }

    /// <summary>Account assignment history (pack §18 surface #7). Scoped to one rule when a ruleId is given.</summary>
    [HttpGet("Models/{id:guid}/AssignmentRules/History")]
    [HttpGet("Models/{id:guid}/AssignmentRules/{ruleId:guid}/History")]
    public async Task<IActionResult> AssignmentHistoryPage(Guid id, Guid? ruleId)
    {
        if (RequirePage(ModelReadPermission) is { } denied)
        {
            return denied;
        }

        var vm = await LoadRuleScopedPageAsync(id, ruleId);
        if (vm is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/AssignmentHistory.cshtml", vm);
    }

    /// <summary>Page context plus, when scoped to a rule, that rule's identity for the heading and the filters.
    /// A ruleId that does not belong to this model simply resolves to null, so the page falls back to model scope.</summary>
    private async Task<TerritoryRuleScopedPageViewModel?> LoadRuleScopedPageAsync(Guid modelId, Guid? ruleId)
    {
        var detail = await LoadModelDetailAsync(modelId);
        if (detail is null)
        {
            return null;
        }

        TerritoryAssignmentRuleViewModel? rule = null;
        if (ruleId is { } id && AddAuthHeaders())
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-rules/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryAssignmentRuleViewModel>>(_jsonOptions);
                    rule = payload?.Data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Assignment rule {RuleId} could not be loaded for {ModelId}.", id, modelId);
            }
        }

        return new TerritoryRuleScopedPageViewModel
        {
            Model = detail,
            CanManageModel = PermissionClaims.HasPermission(User, ModelManagePermission),
            CanManageNode = PermissionClaims.HasPermission(User, NodeManagePermission),
            Rule = rule
        };
    }

    [HttpGet("Models/{id:guid}/ResourceAssignments")]
    public async Task<IActionResult> ResourceAssignmentsPage(Guid id)
    {
        if (RequirePage(ModelReadPermission) is { } denied)
        {
            return denied;
        }

        var vm = await LoadModelPageAsync(id);
        if (vm is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/ResourceAssignments.cshtml", vm);
    }

    /// <summary>Minimal page context both dedicated screens need: the model itself plus the permission flags the
    /// partials use to decide what is editable. The tables load their own data over the Json proxies.</summary>
    private async Task<TerritoryModelDetailPageViewModel?> LoadModelPageAsync(Guid id)
    {
        var detail = await LoadModelDetailAsync(id);
        if (detail is null)
        {
            return null;
        }

        return new TerritoryModelDetailPageViewModel
        {
            Model = detail,
            CanManageModel = PermissionClaims.HasPermission(User, ModelManagePermission),
            CanManageNode = PermissionClaims.HasPermission(User, NodeManagePermission)
        };
    }

    // ======================================================================================================
    // FU03 — Assignment rules + preview (Gateway-only proxies)
    //
    // These surfaces describe and simulate assignment; they never apply one. There is deliberately NO action here
    // that posts to an apply/assign endpoint — that endpoint does not exist in CrmService either (FU05).
    // ======================================================================================================

    [HttpGet("Models/{modelId:guid}/AssignmentRules/Json")]
    public async Task<IActionResult> AssignmentRulesJson(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-rules");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryAssignmentRuleListViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory assignment rule list failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Rule-type and conflict-policy options come from MOD-0048 published values through the Gateway.
    /// Returns EMPTY + a not-ready flag when a set is unpublished — never a hardcoded rule-type list.</summary>
    [HttpGet("Models/{modelId:guid}/AssignmentRules/lookups")]
    public async Task<IActionResult> AssignmentRuleLookups(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new
            {
                ruleTypes = Array.Empty<object>(), ruleTypeReady = false,
                conflictPolicies = Array.Empty<object>(), conflictPolicyReady = false,
                nodes = Array.Empty<object>()
            });

        var ruleTypes = await LoadReferenceOptionsAsync(TerritoryRuleTypeSetCode);
        var conflictPolicies = await LoadReferenceOptionsAsync(TerritoryConflictPolicySetCode);
        var hierarchy = await LoadHierarchyAsync(modelId);

        // Criteria options come from the SAME MOD-0048 sets the MOD-0149 Account form binds to, so a rule can only
        // ever be written against values an account could actually carry. Unpublished set → empty list + not-ready
        // flag; never a hardcoded city/type list.
        var countries = await LoadReferenceOptionsAsync(CountrySetCode);
        var cities = await LoadReferenceOptionsAsync(CitySetCode);
        var districts = await LoadReferenceOptionsAsync(DistrictSetCode);
        var accountTypes = await LoadReferenceOptionsAsync(AccountTypeSetCode);
        var accountCategories = await LoadReferenceOptionsAsync(AccountCategorySetCode);
        var accountStatuses = await LoadReferenceOptionsAsync(AccountStatusSetCode);

        static object[] Options(IReadOnlyList<ReferenceOptionViewModel> source)
            => source.Select(x => (object)new { value = x.Value, text = x.Text }).ToArray();

        return Json(new
        {
            // Only the rule types the FU03 engine evaluates are offered; the rest stay published-but-not-selectable
            // so the form cannot create a rule the backend would reject.
            ruleTypes = ruleTypes
                .Where(x => Fu03SupportedRuleTypes.Contains(x.Value, StringComparer.OrdinalIgnoreCase))
                .Select(x => new { value = x.Value, text = x.Text }),
            ruleTypeReady = ruleTypes.Count > 0,
            conflictPolicies = conflictPolicies.Select(x => new { value = x.Value, text = x.Text }),
            conflictPolicyReady = conflictPolicies.Count > 0,
            nodes = (hierarchy?.Nodes ?? [])
                .Select(n => new
                {
                    value = n.Id,
                    text = $"{n.TerritoryCode} — {n.Name} ({n.TerritoryLevel})",
                    // Effective window lets the account-apply canvas floor its EffectiveFrom to the target node's
                    // start, so an assignment can't begin before the node is effective (backend 409 otherwise).
                    effectiveFrom = n.EffectiveFrom.ToString("yyyy-MM-dd"),
                    effectiveTo = n.EffectiveTo?.ToString("yyyy-MM-dd")
                }),

            criteria = new
            {
                countryRefs = Options(countries),
                cityRefs = Options(cities),
                districtRefs = Options(districts),
                accountTypes = Options(accountTypes),
                accountCategories = Options(accountCategories),
                accountStatuses = Options(accountStatuses)
            },
            criteriaNotReady = new[]
                {
                    countries.Count == 0 ? CountrySetCode : null,
                    cities.Count == 0 ? CitySetCode : null,
                    districts.Count == 0 ? DistrictSetCode : null,
                    accountTypes.Count == 0 ? AccountTypeSetCode : null,
                    accountCategories.Count == 0 ? AccountCategorySetCode : null,
                    accountStatuses.Count == 0 ? AccountStatusSetCode : null
                }.Where(x => x is not null).ToArray()
        });
    }

    [HttpPost("Models/{modelId:guid}/AssignmentRules/SaveJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAssignmentRuleJson(Guid modelId, [FromForm] TerritoryAssignmentRuleEditViewModel model)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var payload = ToRulePayload(model);
            var response = model.Id is { } ruleId
                ? await _httpClient.PutAsJsonAsync(
                    $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-rules/{ruleId}", payload, _jsonOptions)
                : await _httpClient.PostAsJsonAsync(
                    $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-rules", payload, _jsonOptions);

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory assignment rule save failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Soft-delete only — there is no hard delete anywhere in MOD-0151.</summary>
    [HttpPost("Models/{modelId:guid}/AssignmentRules/{ruleId:guid}/DeleteJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssignmentRuleJson(Guid modelId, Guid ruleId)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-rules/{ruleId}/delete-draft",
                new { reason = "ui-delete-draft", correlationId = $"ui-territory-{Guid.NewGuid():N}" }, _jsonOptions);

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory assignment rule delete failed for {ModelId}/{RuleId}.", modelId, ruleId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Runs the preview. Read-only: the Gateway endpoint behind it persists nothing.</summary>
    [HttpPost("Models/{modelId:guid}/AssignmentPreview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignmentPreview(Guid modelId, [FromForm] Guid? ruleId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-preview",
                new { ruleId, correlationId = $"ui-territory-preview-{Guid.NewGuid():N}" }, _jsonOptions);

            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryAssignmentPreviewViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory assignment preview failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private static TerritoryAssignmentRuleSavePayload ToRulePayload(TerritoryAssignmentRuleEditViewModel m)
        => new()
        {
            RuleCode = m.RuleCode.Trim(),
            Name = m.Name.Trim(),
            TerritoryId = m.TerritoryId,
            RuleType = m.RuleType.Trim(),
            ConflictPolicy = m.ConflictPolicy.Trim(),
            Priority = m.Priority,
            IsEnabled = m.IsEnabled,
            Criteria = new TerritoryRuleCriteriaPayload
            {
                CountryRefs = CleanCodes(m.CountryRefs),
                CityRefs = CleanCodes(m.CityRefs),
                DistrictRefs = CleanCodes(m.DistrictRefs),
                AccountTypes = CleanCodes(m.AccountTypes),
                AccountCategories = CleanCodes(m.AccountCategories),
                AccountStatuses = CleanCodes(m.AccountStatuses)
            },
            EffectiveFrom = ToOffset(m.EffectiveFrom),
            EffectiveTo = ToOffset(m.EffectiveTo),
            CorrelationId = $"ui-territory-rule-{Guid.NewGuid():N}"
        };

    /// <summary>A multi-select posts one entry per selection; drop blanks and duplicates before sending.</summary>
    private static List<string> CleanCodes(IEnumerable<string>? values)
        => values is null
            ? []
            : values.Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    [HttpGet("Models/{modelId:guid}/AccountAssignments/Json")]
    public async Task<IActionResult> AccountAssignmentsJson(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission) || !AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/account-assignments");
        if (!response.IsSuccessStatusCode) return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<AccountTerritoryAssignmentListViewModel>>(_jsonOptions);
        return Json(new { success = true, data = payload?.Data });
    }

    [HttpPost("Models/{modelId:guid}/AccountAssignments/ApplyJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyAccountAssignmentsJson(Guid modelId, [FromForm] AccountAssignmentApplyForm model)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission) || !AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        try
        {
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(model.SelectedRowsJson, _jsonOptions) ?? [];
            var detail = await LoadModelDetailAsync(modelId);
            var body = new
            {
                model.PreviewRunId,
                selectedRows = rows,
                businessScopes = detail?.BusinessScopes ?? [],
                model.EffectiveFrom,
                model.EffectiveTo,
                model.ConflictPolicy,
                model.Override,
                model.OverrideReason,
                correlationId = $"ui-territory-account-apply-{Guid.NewGuid():N}"
            };
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/assignment-preview/apply", body, _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, status = (int)response.StatusCode, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory account assignment apply failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    // ======================================================================================================
    // FU04 — Resource assignments (Gateway-only proxies for the Details page)
    //
    // These surfaces assign PEOPLE to territory nodes. They never assign accounts: no action here posts to an
    // account-assignment/apply endpoint, and no such endpoint exists in CrmService either (FU05).
    // ======================================================================================================

    [HttpGet("Models/{modelId:guid}/ResourceAssignments/Json")]
    public async Task<IActionResult> ResourceAssignmentsJson(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryResourceAssignmentListViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory resource assignment list failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpGet("Models/{modelId:guid}/ResourceAssignments/CurrentJson")]
    public async Task<IActionResult> CurrentResourceResponsibilitiesJson(Guid modelId)
        => await GetResourceAssignmentProjectionAsync(
            modelId, $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-responsibilities/current");

    [HttpGet("Models/{modelId:guid}/ResourceAssignments/HistoryJson")]
    public async Task<IActionResult> ResourceAssignmentHistoryJson(Guid modelId)
        => await GetResourceAssignmentProjectionAsync(
            modelId, $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments/history");

    /// <summary>FU04B — read-only plan-vs-current projection. Gateway-only; the tenant comes from the JWT, never
    /// from a payload. No mutation surface is proxied here.</summary>
    [HttpGet("Models/{modelId:guid}/PlanVsCurrent/Json")]
    public async Task<IActionResult> PlanVsCurrentJson(
        Guid modelId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] Guid? territoryNodeId,
        [FromQuery] string? businessUnit, [FromQuery] string? positionCode, [FromQuery] string? resourceId,
        [FromQuery] string? diffType)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        var query = new List<string>();
        if (effectiveAt is { } at) query.Add($"effectiveAt={Uri.EscapeDataString(at.ToString("o"))}");
        if (territoryNodeId is { } node) query.Add($"territoryNodeId={node}");
        if (!string.IsNullOrWhiteSpace(businessUnit)) query.Add($"businessUnit={Uri.EscapeDataString(businessUnit)}");
        if (!string.IsNullOrWhiteSpace(positionCode)) query.Add($"positionCode={Uri.EscapeDataString(positionCode)}");
        if (!string.IsNullOrWhiteSpace(resourceId)) query.Add($"resourceId={Uri.EscapeDataString(resourceId)}");
        if (!string.IsNullOrWhiteSpace(diffType)) query.Add($"diffType={Uri.EscapeDataString(diffType)}");
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignment-plan-vs-current{suffix}");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<
                GatewayResponse<TerritoryPlanVsCurrentViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory plan-vs-current failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private async Task<IActionResult> GetResourceAssignmentProjectionAsync(Guid modelId, string url)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
            var payload = await response.Content.ReadFromJsonAsync<
                GatewayResponse<List<TerritoryResourceAssignmentViewModel>>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data ?? [] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory resource projection failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Role / coverage-scope options come from MOD-0048 published values; node and business-unit options come
    /// from the model itself. The resource (person) list is attempted against the platform/HCM read endpoints and
    /// degrades to a not-ready flag when they are unavailable — never a hardcoded or seeded employee list.</summary>
    [HttpGet("Models/{modelId:guid}/ResourceAssignments/lookups")]
    public async Task<IActionResult> ResourceAssignmentLookups(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new
            {
                positions = Array.Empty<object>(), positionReady = false,
                coverageScopes = Array.Empty<object>(), coverageScopeReady = false,
                nodes = Array.Empty<object>(), businessUnits = Array.Empty<object>(),
                resources = Array.Empty<object>(), resourceLookupReady = false
            });

        var positions = await LoadPositionOptionsAsync();
        var coverageScopes = await LoadReferenceOptionsAsync(TerritoryCoverageScopeSetCode);
        var hierarchy = await LoadHierarchyAsync(modelId);
        var detail = await LoadModelDetailAsync(modelId);
        var resources = await LoadResourceOptionsAsync();

        return Json(new
        {
            // Position replaces the former MOD-0048 role: options come from the Organization/Platform positions.
            positions = positions.Select(p => new { value = p.Id, code = p.Code, name = p.Name, text = $"{p.Code} — {p.Name}" }),
            positionReady = positions.Count > 0,
            coverageScopes = coverageScopes.Select(x => new { value = x.Value, text = x.Text }),
            coverageScopeReady = coverageScopes.Count > 0,
            nodes = (hierarchy?.Nodes ?? [])
                .Select(n => new { value = n.Id, text = $"{n.TerritoryCode} — {n.Name} ({n.TerritoryLevel})", level = n.TerritoryLevel }),
            // An assignment may never widen the model's own business scope, so the selector offers exactly that set.
            businessUnits = (detail?.BusinessScopes ?? [])
                .Where(s => string.Equals(s.ScopeType, BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
                .Select(s => new { value = s.ScopeCode, text = s.ScopeCode }),
            resources = resources.Select(r => new
            {
                value = r.Value,
                text = r.Text,
                displayName = r.DisplayName,
                email = r.Email,
                resourceType = "user"
            }),
            resourceLookupReady = resources.Count > 0
        });
    }

    [HttpPost("Models/{modelId:guid}/ResourceAssignments/SaveJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResourceAssignmentJson(Guid modelId, [FromForm] TerritoryResourceAssignmentEditViewModel model)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var payload = ToResourceAssignmentPayload(model);
            var response = model.Id is { } assignmentId
                ? await _httpClient.PutAsJsonAsync(
                    $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments/{assignmentId}", payload, _jsonOptions)
                : await _httpClient.PostAsJsonAsync(
                    $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments", payload, _jsonOptions);

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory resource assignment save failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Ends an assignment (status=ended + validTo). This is the correct way to remove responsibility from
    /// someone; the record and its history stay.</summary>
    [HttpPost("Models/{modelId:guid}/ResourceAssignments/{assignmentId:guid}/EndJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndResourceAssignmentJson(
        Guid modelId, Guid assignmentId, [FromForm] DateTimeOffset? effectiveDate, [FromForm] string? reason)
        => await PostResourceAssignmentActionAsync(modelId, assignmentId, "end",
            new { endDate = effectiveDate, reason, correlationId = $"ui-territory-resource-{Guid.NewGuid():N}" });

    [HttpPost("Models/{modelId:guid}/ResourceAssignments/{assignmentId:guid}/ReplaceJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceResourceAssignmentJson(
        Guid modelId, Guid assignmentId, [FromForm] string? resourceId, [FromForm] string? resourceDisplayName,
        [FromForm] Guid? positionId, [FromForm] string? positionCode, [FromForm] string? positionTitle,
        [FromForm] DateTimeOffset? effectiveDate, [FromForm] string? reason)
        => await PostResourceAssignmentActionAsync(modelId, assignmentId, "replace", new
        {
            resource = new { resourceId, resourceType = "person", displayName = resourceDisplayName, email = (string?)null },
            positionId,
            positionCode,
            positionTitle,
            positionType = "person-position",
            positionSourceSystem = "organization-directory",
            effectiveDate,
            reason,
            correlationId = $"ui-territory-resource-{Guid.NewGuid():N}"
        });

    [HttpPost("Models/{modelId:guid}/ResourceAssignments/{assignmentId:guid}/TransferJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferResourceAssignmentJson(
        Guid modelId, Guid assignmentId, [FromForm] Guid? targetTerritoryId,
        [FromForm] string? coverageScope, [FromForm] List<string>? businessUnitScopeCodes,
        [FromForm] DateTimeOffset? effectiveDate, [FromForm] string? reason)
        => await PostResourceAssignmentActionAsync(modelId, assignmentId, "transfer", new
        {
            targetTerritoryId,
            coverageScope,
            businessUnitScopeCodes,
            effectiveDate,
            reason,
            correlationId = $"ui-territory-resource-{Guid.NewGuid():N}"
        });

    /// <summary>Soft-deletes a still-proposed assignment (never a hard delete).</summary>
    [HttpPost("Models/{modelId:guid}/ResourceAssignments/{assignmentId:guid}/DeleteJson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResourceAssignmentJson(Guid modelId, Guid assignmentId)
        => await PostResourceAssignmentActionAsync(modelId, assignmentId, "delete-draft",
            new { reason = "ui-delete-draft", correlationId = $"ui-territory-resource-{Guid.NewGuid():N}" });

    [HttpPost("Models/{modelId:guid}/ResourceAssignments/ValidateConflicts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateResourceConflicts(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments/validate-conflicts", new { }, _jsonOptions);

            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryResourceConflictReportViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory resource conflict validation failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private async Task<IActionResult> PostResourceAssignmentActionAsync(Guid modelId, Guid assignmentId, string action, object body)
    {
        if (!PermissionClaims.HasPermission(User, ModelManagePermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/resource-assignments/{assignmentId}/{action}", body, _jsonOptions);

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory resource assignment '{Action}' failed for {ModelId}/{AssignmentId}.", action, modelId, assignmentId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    /// <summary>Tenant-scoped Auth user lookup used until the HR employee directory is available. The endpoint returns
    /// a plain paginated result rather than the standard Response envelope. No manual person/employee fallback is
    /// exposed by the UI: Employee remains a disabled future resource type.</summary>
    private async Task<IReadOnlyList<ResourceOptionViewModel>> LoadResourceOptionsAsync()
    {
        if (!AddAuthHeaders())
            return [];

        var url = $"{_gatewayUrl}/api/users?page=1&pageSize=1000";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content.ReadFromJsonAsync<ResourceLookupPage>(_jsonOptions);
            return (payload?.Items ?? [])
                .Where(i => !string.IsNullOrWhiteSpace(i.Id) && i.IsActive is not false)
                .Select(i =>
                {
                    var name = string.Join(' ', new[] { i.FirstName, i.LastName }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                    var text = !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(i.Email)
                        ? $"{name} ({i.Email})"
                        : !string.IsNullOrWhiteSpace(name) ? name : i.Email ?? i.Id!;
                    return new ResourceOptionViewModel(i.Id!, text, name.Length > 0 ? name : i.Email ?? i.Id!, i.Email);
                })
                .OrderBy(x => x.Text)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Auth user lookup source unavailable: {Url}", url);
            return [];
        }
    }

    private sealed record PositionOption(Guid Id, string Code, string Name);
    private sealed record ResourceOptionViewModel(string Value, string Text, string DisplayName, string? Email);

    /// <summary>Organization/Platform positions feed for the resource-assignment form (replaces the MOD-0048 role
    /// list). Non-archived only. Returns an EMPTY list when the endpoint is unreachable/forbidden — the form then
    /// surfaces a controlled "positions not available" state, never a hardcoded fallback.</summary>
    private async Task<IReadOnlyList<PositionOption>> LoadPositionOptionsAsync()
    {
        if (!AddAuthHeaders())
            return [];

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/platform/positions?pageSize=500");
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            // Tolerate the common envelopes: { data: { items: [] } }, { data: [] }, { data: { data: [] } }, or [].
            JsonElement array;
            if (root.ValueKind == JsonValueKind.Array)
            {
                array = root;
            }
            else if (root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Array) array = data;
                else if (data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array) array = items;
                else if (data.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Array) array = inner;
                else return [];
            }
            else
            {
                return [];
            }

            var result = new List<PositionOption>();
            foreach (var el in array.EnumerateArray())
            {
                var id = PositionString(el, "id");
                if (!Guid.TryParse(id, out var gid) || gid == Guid.Empty)
                    continue;
                if (PositionBool(el, "isArchived"))
                    continue;

                result.Add(new PositionOption(gid, PositionString(el, "code") ?? string.Empty, PositionString(el, "name") ?? string.Empty));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Position lookup source unavailable.");
            return [];
        }
    }

    // Case-insensitive property reads: the positions endpoint may serialize camelCase or PascalCase.
    private static string? PositionString(JsonElement el, string name)
    {
        foreach (var prop in el.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                return prop.Value.GetString();
        return null;
    }

    private static bool PositionBool(JsonElement el, string name)
    {
        foreach (var prop in el.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)
                && (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False))
                return prop.Value.GetBoolean();
        return false;
    }

    private static TerritoryResourceAssignmentSavePayload ToResourceAssignmentPayload(TerritoryResourceAssignmentEditViewModel m)
        => new()
        {
            TerritoryId = m.TerritoryId,
            Resource = new TerritoryResourceRefPayload
            {
                ResourceId = m.ResourceId.Trim(),
                ResourceType = string.IsNullOrWhiteSpace(m.ResourceType) ? "person" : m.ResourceType.Trim(),
                DisplayName = m.ResourceDisplayName.Trim(),
                Email = Trim(m.ResourceEmail)
            },
            PositionId = m.PositionId,
            PositionCode = m.PositionCode.Trim(),
            PositionName = Trim(m.PositionName),
            PositionType = Trim(m.PositionType),
            PositionSourceSystem = Trim(m.PositionSourceSystem),
            CoverageScope = Trim(m.CoverageScope),
            BusinessUnitScopeCodes = m.BusinessUnitScopeCodes?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? [],
            IsPrimary = m.IsPrimary,
            AssignmentSource = Trim(m.AssignmentSource),
            ValidFrom = ToOffset(m.ValidFrom),
            ValidTo = ToOffset(m.ValidTo),
            ChangeReason = Trim(m.ChangeReason),
            CorrelationId = $"ui-territory-resource-{Guid.NewGuid():N}"
        };

    // ======================================================================================================
    // Loaders (Gateway-only)
    // ======================================================================================================

    private async Task<TerritoryContractViewModel?> LoadContractAsync()
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-management/contract");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryContractViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory contract load failed.");
            return null;
        }
    }

    private async Task<TerritoryModelListViewModel?> LoadModelsAsync()
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models?page=1&pageSize=200");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryModelListViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory models load failed.");
            return null;
        }
    }

    private async Task<TerritoryModelDetailViewModel?> LoadModelDetailAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryModelDetailViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory model load failed for {ModelId}.", id);
            return null;
        }
    }

    private async Task<TerritoryHierarchyViewModel?> LoadHierarchyAsync(Guid modelId)
    {
        if (!AddAuthHeaders())
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/nodes");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryHierarchyViewModel>>(_jsonOptions);
            return payload?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Territory hierarchy load failed for {ModelId}.", modelId);
            return null;
        }
    }

    // FU02A: server-rendered ModelForm scope option sources (Country single + Business Unit multi). Reference-data
    // driven (MOD-0048 published-values); returns empty when unpublished so the form surfaces a not-ready notice.
    private async Task PopulateModelScopeOptionsAsync(TerritoryModelEditViewModel model)
    {
        model.CountryOptions = await LoadReferenceOptionsAsync(CountrySetCode);
        model.BusinessUnitOptions = await LoadReferenceOptionsAsync(BusinessUnitSetCode);
    }

    private async Task PopulateNodeOptionsAsync(TerritoryNodeEditViewModel model)
    {
        model.LevelOptions = await LoadReferenceOptionsAsync(TerritoryLevelSetCode);
        model.ParentOptions = await LoadParentNodeOptionsAsync(model.ModelId, model.Id);
        if (model.LevelOptions.Count == 0)
        {
            model.ReferenceDependencyMessage = _localizer["ReferenceDataUnavailable"].Value;
        }
    }

    /// <summary>Parent candidates = existing nodes in the same model, excluding the node being edited. Full cycle
    /// prevention stays a backend invariant.</summary>
    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadParentNodeOptionsAsync(Guid modelId, Guid? excludeId)
    {
        var hierarchy = await LoadHierarchyAsync(modelId);
        if (hierarchy is null)
            return [];

        return hierarchy.Nodes
            .Where(n => excludeId is null || n.Id != excludeId)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.TerritoryCode, StringComparer.OrdinalIgnoreCase)
            .Select(n => new ReferenceOptionViewModel(n.Id.ToString(), $"{n.TerritoryCode} — {n.Name} ({n.TerritoryLevel})"))
            .ToList();
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

        // A TENANT-scoped set REQUIRES scope_key; a GLOBAL one REFUSES it (400 "scope_key_not_allowed_for_global").
        // Nothing tells the consumer which shape a set has before asking, and the sets bound here are mixed:
        // `city` / `district` / `business-unit` … are tenant-scoped, while COUNTRY_CODES is Global. So ask the tenant
        // way first and retry once WITHOUT the key on the service's own refusal — the same two-step the MOD-0165-FU07
        // equivalence gate uses. A tenant-scoped set fails the keyless retry too ("scope_key_required"), so this can
        // never widen a read past its scope.
        var baseUrl = $"{_gatewayUrl}/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}/published-values";
        var urls = new[] { $"{baseUrl}?scope_key={Uri.EscapeDataString(tenantId)}", baseUrl };

        try
        {
            foreach (var url in urls)
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Reference set '{SetCode}' returned {Status}; rendering without options.", setCode, response.StatusCode);
                    continue;
                }

                var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<PublishedValuesModel>>(_jsonOptions);
                var items = payload?.Data?.Items;
                if (items is null)
                    return [];

                return items
                    .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Value))
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new ReferenceOptionViewModel(x.Value!, string.IsNullOrWhiteSpace(x.Text) ? x.Value! : x.Text!))
                    .ToList();
            }

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' load failed; rendering without options.", setCode);
            return [];
        }
    }

    private async Task<IReadOnlyList<ReferenceOptionViewModel>> LoadAnchorAccountOptionsAsync(string? countryScope)
    {
        if (string.IsNullOrWhiteSpace(countryScope) || !AddAuthHeaders())
            return [];

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/accounts?page=1&pageSize=200");
            if (!response.IsSuccessStatusCode)
                return [];
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<AccountPagedResult<AccountListItemViewModel>>>(_jsonOptions);
            return (payload?.Data?.Items ?? [])
                .Where(a => string.Equals(a.CountryRef, countryScope, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.AccountName)
                .Select(a => new ReferenceOptionViewModel(a.Id.ToString(), $"{a.AccountCode} — {a.AccountName}"))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anchor account options could not be loaded for country {CountryScope}.", countryScope);
            return [];
        }
    }

    // ======================================================================================================
    // Mapping + helpers
    // ======================================================================================================

    private static DateTimeOffset ToOffset(DateTime value)
        => new(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)) : null;

    private static string CreateDefaultModelCode()
        => $"TM-{DateTime.Now:yyyyMMdd-HHmmss}";

    private static TerritoryModelEditViewModel ToModelEdit(TerritoryModelDetailViewModel d) => new()
    {
        Id = d.Id,
        ModelCode = d.ModelCode,
        Name = d.Name,
        CountryScope = d.CountryScope,
        DivisionScope = d.DivisionScope,
        BusinessUnitScopes = d.BusinessScopes
            .Where(s => string.Equals(s.ScopeType, BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ScopeCode)
            .ToList(),
        EffectiveFrom = d.EffectiveFrom.UtcDateTime.Date,
        EffectiveTo = d.EffectiveTo?.UtcDateTime.Date,
        ChangeReason = d.ChangeReason
    };

    private static TerritoryModelSavePayload ToModelPayload(TerritoryModelEditViewModel m) => new()
    {
        ModelCode = m.ModelCode.Trim(),
        Name = m.Name.Trim(),
        // FU02A: CountryScope is a published country value code (single select), not free text.
        CountryScope = string.IsNullOrWhiteSpace(m.CountryScope) ? null : m.CountryScope.Trim(),
        // FU02A: legacy Division Scope is retired from the UI — never sent.
        DivisionScope = null,
        BasedOnModelId = m.BasedOnModelId,
        // FU02A: Business Unit multi-select → passive businessScopes[{scopeType:"business-unit", scopeCode}].
        // De-duplicated; blanks dropped; scopeType is ALWAYS business-unit (no brand/product scope from this form).
        BusinessScopes = BuildBusinessUnitScopes(m.BusinessUnitScopes),
        EffectiveFrom = ToOffset(m.EffectiveFrom),
        EffectiveTo = ToOffset(m.EffectiveTo),
        ChangeReason = string.IsNullOrWhiteSpace(m.ChangeReason) ? null : m.ChangeReason.Trim(),
        CorrelationId = $"ui-territory-{Guid.NewGuid():N}"
    };

    private static List<TerritoryBusinessScopePayload>? BuildBusinessUnitScopes(List<string>? selected)
    {
        if (selected is null || selected.Count == 0)
            return null;

        var codes = selected
            .Select(c => c?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return codes.Count == 0
            ? null
            : codes.Select(c => new TerritoryBusinessScopePayload(BusinessUnitScopeType, c)).ToList();
    }

    private static TerritoryNodeEditViewModel ToNodeEdit(TerritoryNodeViewModel n) => new()
    {
        ModelId = n.ModelId,
        Id = n.Id,
        ParentTerritoryId = n.ParentTerritoryId,
        TerritoryCode = n.TerritoryCode,
        Name = n.Name,
        TerritoryLevel = n.TerritoryLevel,
        CountryCode = n.CountryCode,
        DivisionCode = n.DivisionCode,
        RegionCode = n.RegionCode,
        AreaCode = n.AreaCode,
        ZoneCode = n.ZoneCode,
        MicroZoneCode = n.MicroZoneCode,
        EffectiveFrom = n.EffectiveFrom.UtcDateTime.Date,
        EffectiveTo = n.EffectiveTo?.UtcDateTime.Date,
        SortOrder = n.SortOrder,
        AnchorAccountId = n.MicroZoneProfile?.AnchorAccountId,
        ClusterNotes = n.MicroZoneProfile?.ClusterNotes,
        PlanningCenterType = n.MicroZoneProfile?.PlanningCenterType
    };

    private static TerritoryNodeSavePayload ToNodePayload(TerritoryNodeEditViewModel m)
    {
        var isMicroZone = string.Equals(m.TerritoryLevel?.Trim(), MicroZoneLevel, StringComparison.OrdinalIgnoreCase);
        // MicroZoneProfile is ONLY sent for a microzone node (backend rejects it otherwise; UI must not send it).
        MicroZoneProfileInputPayload? profile = null;
        if (isMicroZone && (m.AnchorAccountId is not null
                            || !string.IsNullOrWhiteSpace(m.ClusterNotes)
                            || !string.IsNullOrWhiteSpace(m.PlanningCenterType)))
        {
            profile = new MicroZoneProfileInputPayload
            {
                AnchorAccountId = m.AnchorAccountId,
                ClusterNotes = string.IsNullOrWhiteSpace(m.ClusterNotes) ? null : m.ClusterNotes.Trim(),
                PlanningCenterType = string.IsNullOrWhiteSpace(m.PlanningCenterType) ? null : m.PlanningCenterType.Trim()
            };
        }

        return new TerritoryNodeSavePayload
        {
            ParentTerritoryId = m.ParentTerritoryId,
            TerritoryCode = m.TerritoryCode.Trim(),
            Name = m.Name.Trim(),
            TerritoryLevel = m.TerritoryLevel.Trim(),
            CountryCode = Trim(m.CountryCode),
            DivisionCode = Trim(m.DivisionCode),
            RegionCode = Trim(m.RegionCode),
            AreaCode = Trim(m.AreaCode),
            ZoneCode = Trim(m.ZoneCode),
            MicroZoneCode = Trim(m.MicroZoneCode),
            EffectiveFrom = ToOffset(m.EffectiveFrom),
            EffectiveTo = ToOffset(m.EffectiveTo),
            SortOrder = m.SortOrder,
            MicroZoneProfile = profile,
            CorrelationId = $"ui-territory-{Guid.NewGuid():N}"
        };
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Orders a flat node list into a parent-before-children tree with a Depth for indented rendering.</summary>
    private static List<TerritoryNodeViewModel> BuildOrderedTree(List<TerritoryNodeViewModel> nodes)
    {
        // Group children by parent. A root node's ParentTerritoryId is null, and Dictionary rejects a null key
        // (ArgumentNullException) — so use ToLookup, which allows a null key and returns an empty sequence for
        // any parent that has no children. Ordering is applied before grouping; ToLookup preserves group order.
        var byParent = nodes
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.TerritoryCode, StringComparer.OrdinalIgnoreCase)
            .ToLookup(n => n.ParentTerritoryId);

        var ids = new HashSet<Guid>(nodes.Select(n => n.Id));
        var ordered = new List<TerritoryNodeViewModel>();

        void Walk(Guid? parentId, int depth)
        {
            foreach (var child in byParent[parentId])
            {
                child.Depth = depth;
                ordered.Add(child);
                Walk(child.Id, depth + 1);
            }
        }

        // Roots = nodes whose parent is null OR whose parent is not in this model's set (defensive).
        Walk(null, 0);
        foreach (var orphan in nodes.Where(n => n.ParentTerritoryId is { } p && !ids.Contains(p)))
        {
            if (ordered.Contains(orphan))
                continue;
            orphan.Depth = 0;
            ordered.Add(orphan);
            Walk(orphan.Id, 1);
        }

        return ordered.Count == nodes.Count ? ordered : nodes;
    }

    private List<string> CollectModelErrors() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();

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

    // ======================================================================================================
    // FU08 — Import / Export (Gateway-only proxies)
    //
    // Every call goes to /api/crm/territory-models/... through the Gateway; nothing here talks to :5061 and no
    // request carries a TenantId in its body — tenancy rides the JWT claim + X-Tenant-Id header like everywhere else.
    // The upload proxy defaults to dryRun=true and apply is a SEPARATE action, mirroring the backend routes so a
    // stray preview click can never write.
    // ======================================================================================================

    [HttpGet("Models/{id:guid}/ImportExport")]
    public async Task<IActionResult> ImportExportPage(Guid id)
    {
        if (RequirePage(ModelReadPermission) is { } denied)
        {
            return denied;
        }

        var vm = await LoadModelPageAsync(id);
        if (vm is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/ImportExport.cshtml", vm);
    }

    /// <summary>Streams the XLSX export/template straight through. The file is never buffered to disk or persisted.</summary>
    [HttpGet("Models/{modelId:guid}/Export")]
    public Task<IActionResult> ExportWorkbook(Guid modelId)
        => ProxyFileAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/export", "territory-export.xlsx");

    [HttpGet("Models/{modelId:guid}/ImportTemplate")]
    public Task<IActionResult> ImportTemplate(Guid modelId)
        => ProxyFileAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/import-template", "territory-import-template.xlsx");

    [HttpPost("Models/{modelId:guid}/ImportDryRun")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ImportDryRun(Guid modelId, IFormFile? file, bool strictMode = false)
        => ProxyImportAsync(modelId, file, dryRun: true, strictMode);

    [HttpPost("Models/{modelId:guid}/ImportApply")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ImportApply(Guid modelId, IFormFile? file, bool strictMode = false)
        => ProxyImportAsync(modelId, file, dryRun: false, strictMode);

    [HttpGet("Models/{modelId:guid}/ImportRuns/Json")]
    public async Task<IActionResult> ImportRunsJson(Guid modelId)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/crm/territory-models/{modelId}/import-runs");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryImportRunListViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory import run history failed for {ModelId}.", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private async Task<IActionResult> ProxyImportAsync(Guid modelId, IFormFile? file, bool dryRun, bool strictMode)
    {
        // Apply needs the manage permission; a preview only needs read. Fail closed on both.
        var required = dryRun ? ModelReadPermission : ModelManagePermission;
        if (!PermissionClaims.HasPermission(User, required))
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        if (file is null || file.Length == 0)
            return Json(new { success = false, errors = new[] { _localizer["ImportSelectFile"].Value } });

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, errors = new[] { _localizer["ImportXlsxOnly"].Value } });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", Path.GetFileName(file.FileName));

            var route = dryRun ? "import-file" : "import-file/apply";
            var query = dryRun ? $"?dryRun=true&strictMode={strictMode}" : $"?strictMode={strictMode}";
            var response = await _httpClient.PostAsync(
                $"{_gatewayUrl}/api/crm/territory-models/{modelId}/{route}{query}", content);

            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<TerritoryImportPreviewViewModel>>(_jsonOptions);
            return Json(new { success = true, data = payload?.Data });
        }
        catch (Exception ex)
        {
            // The file NAME can carry operator context; log the model, never the file.
            _logger.LogError(ex, "Territory import ({Mode}) failed for {ModelId}.", dryRun ? "dry-run" : "apply", modelId);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    private async Task<IActionResult> ProxyFileAsync(string url, string fallbackName)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return StatusCode(StatusCodes.Status403Forbidden);

        if (!AddAuthHeaders())
            return StatusCode(StatusCodes.Status401Unauthorized);

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = string.Join(" ", await ExtractGatewayErrorsAsync(response));
                return RedirectToAction(nameof(Index));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? fallbackName;

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory workbook download failed.");
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
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

    private async Task<IActionResult> ProxyReadinessGetAsync(string path, CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, ModelReadPermission))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!AddAuthHeaders())
            return StatusCode(StatusCodes.Status401Unauthorized);

        try
        {
            using var response = await _httpClient.GetAsync($"{_gatewayUrl}{path}", cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Territory FU09A readiness proxy failed for {Path}.", path.Split('?')[0]);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private IActionResult? RequirePage(string permission) =>
        PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);
}
