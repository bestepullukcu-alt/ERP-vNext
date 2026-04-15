using Diten.Web.Config;
using Diten.Web.Models.EnterpriseStrategy;
using Diten.Web.Services.EnterpriseStrategy;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("management-governance/enterprise-strategy-business-performance")]
public sealed class EnterpriseStrategyBusinessPerformanceController : Controller
{
    private readonly bool _isGoalHierarchyUiEnabled;

    public EnterpriseStrategyBusinessPerformanceController(IConfiguration configuration)
    {
        _isGoalHierarchyUiEnabled = configuration.GetValue<bool>("FeatureFlags:EnableGoalHierarchyUi", false);
    }

    [HttpGet("")]
    public IActionResult Overview()
    {
        SetViewData(ViewData, "Overview", "overview", "Strategy-owned goals, objectives, and alignment with delivery-owned execution references.");
        return View("Overview");
    }

    [HttpGet("goals")]
    public IActionResult Goals()
    {
        SetViewData(ViewData, "Goals", "goals", string.Empty);
        return View("Goals");
    }

    [HttpGet("goals/new")]
    public IActionResult GoalCreate()
    {
        SetViewData(ViewData, "Create Goal", "goals", "Strategic Goal workspace for draft, validation, and publish orchestration.");
        return View("GoalCreate");
    }

    [HttpGet("goals/new-stepper")]
    public IActionResult GoalCreateStepper()
    {
        SetViewData(ViewData, "Create Goal Wizard", "goals", "Stepper-based strategic goal draft workspace with staged entry flow.");
        return View("GoalCreateStepper");
    }

    [HttpGet("goals/{goalId}/edit")]
    public IActionResult GoalEdit(string goalId)
    {
        ViewData["GoalEditId"] = goalId;
        SetViewData(ViewData, "Edit Goal", "goals", "Strategic Goal workspace for draft, validation, and publish orchestration.");
        return View("GoalCreate");
    }

    [HttpGet("goals/hierarchy")]
    public IActionResult GoalHierarchy()
    {
        if (!_isGoalHierarchyUiEnabled)
        {
            return RedirectToAction(nameof(Goals));
        }

        SetViewData(ViewData, "Goal Hierarchy", "goals", "Hierarchy view of Goal -> Objective -> aligned Initiative -> aligned Project references.");
        return View("GoalHierarchy");
    }

    [HttpGet("goals/{goalId}")]
    public IActionResult GoalDetail(string goalId)
    {
        SetViewData(ViewData, "Goal Detail", "goals", "Owned goal detail, metrics, evidence, and audit history.", new[] { goalId });
        ViewData["GoalId"] = goalId;
        return View("GoalDetail");
    }

    [HttpGet("objectives")]
    public IActionResult Objectives()
    {
        SetViewData(ViewData, "Objectives", "objectives", "Objectives are system-of-record owned in this subdomain.");
        return View("Objectives");
    }

    [HttpGet("objectives/new")]
    public IActionResult ObjectiveCreate()
    {
        SetViewData(ViewData, "Create Objective", "objectives", "Create a draft strategic objective linked to a parent goal and measurable outcome.");
        return View("ObjectiveCreate");
    }

    [HttpGet("objectives/{objectiveId}/edit")]
    public IActionResult ObjectiveEdit(string objectiveId)
    {
        ViewData["ObjectiveEditId"] = objectiveId;
        SetViewData(ViewData, "Edit Objective", "objectives", "Update a draft strategic objective while preserving Goal-aligned planning context.");
        return View("ObjectiveCreate");
    }

    [HttpGet("objectives/alignment")]
    public IActionResult ObjectiveAlignment()
    {
        SetViewData(ViewData, "Objective Alignment", "objectives", "Objective-centric alignment and downstream delivery reference coverage view.");
        return View("ObjectiveAlignment");
    }

    [HttpGet("objectives/{objectiveId}")]
    public IActionResult ObjectiveDetail(string objectiveId)
    {
        SetViewData(ViewData, "Objective Detail", "objectives", "Owned objective detail, alignment, traceability, and audit.", new[] { objectiveId });
        ViewData["ObjectiveId"] = objectiveId;
        return View("ObjectiveDetail");
    }

    [HttpGet("initiatives")]
    public IActionResult Initiatives() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives");

    [HttpGet("initiatives/new")]
    public IActionResult InitiativeCreate([FromQuery] string? parentObjectiveId = null)
        => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/new{(string.IsNullOrWhiteSpace(parentObjectiveId) ? string.Empty : $"?parentObjectiveId={Uri.EscapeDataString(parentObjectiveId)}")}");

    [HttpGet("initiatives/{initiativeId}")]
    public IActionResult InitiativeDetail(string initiativeId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/{Uri.EscapeDataString(initiativeId)}");

    [HttpGet("initiatives/{initiativeId}/edit")]
    public IActionResult InitiativeEdit(string initiativeId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/{Uri.EscapeDataString(initiativeId)}/edit");

    [HttpGet("projects")]
    public IActionResult Projects() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/projects");

    [HttpGet("projects/{projectId}")]
    public IActionResult ProjectDetail(string projectId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/projects/{Uri.EscapeDataString(projectId)}");

    [HttpGet("planning")]
    public IActionResult Planning() => RedirectToAction(nameof(PlanningCycles));

    [HttpGet("planning/cycles")]
    public IActionResult PlanningCycles()
    {
        SetViewData(ViewData, "Planning & Periods", "planning", "MVP planning-cycle and strategy-period management with local ES&BP lookups.");
        return View("PlanningCycles");
    }

    [HttpGet("planning/cycles/create")]
    public IActionResult PlanningCycleCreate()
    {
        SetViewData(ViewData, "Create Planning Cycle", "planning", "Create a governed planning cycle with effective horizon, owner accountability, and readiness validation.");
        return View("PlanningCycleCreate");
    }

    [HttpGet("planning/cycles/{cycleId}")]
    public IActionResult PlanningCycleDetail(string cycleId)
    {
        SetViewData(ViewData, "Planning & Periods", "planning", "Planning cycle metadata and linked strategy periods.");
        ViewData["PlanningCycleId"] = cycleId;
        return View("PlanningCycleDetail");
    }

    [HttpGet("planning/strategy-periods")]
    public IActionResult StrategyPeriods()
    {
        SetViewData(ViewData, "Planning & Periods", "planning", "MVP strategy-period management scoped to planning cycles.");
        return View("StrategyPeriods");
    }

    [HttpGet("planning/strategy-periods/create")]
    public IActionResult StrategyPeriodCreate([FromQuery] string? planningCycleId = null)
    {
        SetViewData(ViewData, "Create Strategy Period", "planning", "Create a scope-aware strategy period with parent-cycle horizon checks and readiness validation.");
        if (!string.IsNullOrWhiteSpace(planningCycleId))
        {
            ViewData["PrefillPlanningCycleId"] = planningCycleId.Trim();
        }
        return View("StrategyPeriodCreate");
    }

    [HttpGet("planning/strategy-periods/{periodId}")]
    public IActionResult StrategyPeriodDetail(string periodId)
    {
        SetViewData(ViewData, "Planning & Periods", "planning", "Strategy period metadata, status, and lightweight usage context.");
        ViewData["StrategyPeriodId"] = periodId;
        return View("StrategyPeriodDetail");
    }

    [HttpGet("connections")]
    public IActionResult Connections()
    {
        SetViewData(ViewData, "Strategy Alignment Register", "connections", "Lineage and strategy connections are owned in this subdomain.");
        return View("Connections");
    }

    [HttpGet("library/catalog")]
    public IActionResult LibraryCatalog() => RedirectToAction(nameof(Goals));

    [HttpGet("library/projects")]
    public IActionResult LibraryProjects() => RedirectToAction(nameof(Goals));

    [HttpGet("library/blueprints/{id}")]
    public IActionResult LibraryBlueprintDetail(string id) => RedirectToAction(nameof(Goals));

    [HttpGet("library/instantiate")]
    public IActionResult LibraryInstantiateWizard() => RedirectToAction(nameof(Goals));

    [HttpGet("library/import")]
    public IActionResult LibraryImportWorkbench() => RedirectToAction(nameof(Goals));

    [HttpGet("library/governance")]
    public IActionResult LibraryGovernance() => RedirectToAction(nameof(Goals));

    [HttpGet("library/usage")]
    public IActionResult LibraryUsageAnalytics() => RedirectToAction(nameof(Goals));

    [HttpGet("kpis")]
    public IActionResult KpiCatalog()
    {
        SetViewData(ViewData, "KPI Catalog", "kpis", "Central strategic KPI register and scorecard metric definitions.");
        return View("KpiCatalog");
    }

    [HttpGet("kpis/library")]
    public IActionResult KpiLibraryCatalog()
    {
        SetViewData(ViewData, "KPI Library Catalog", "kpis", "Reusable governed KPI template library.");
        return View("KpiLibraryCatalog");
    }

    [HttpGet("kpis/library/templates/{id}")]
    public IActionResult KpiTemplateDetail(string id)
    {
        ViewData["KpiTemplateId"] = id;
        SetViewData(ViewData, "KPI Template Detail", "kpis", "Template definition and governance metadata.");
        return View("KpiTemplateDetail");
    }

    [HttpGet("kpis/threshold-models")]
    public IActionResult KpiThresholdModels()
    {
        SetViewData(ViewData, "Threshold Models", "kpis", "Governed KPI threshold profiles.");
        return View("KpiThresholdModels");
    }

    [HttpGet("kpis/scorecard-packs")]
    public IActionResult KpiScorecardPacks()
    {
        SetViewData(ViewData, "Scorecard Packs", "kpis", "Reusable KPI scorecard pack definitions.");
        return View("KpiScorecardPacks");
    }

    [HttpGet("kpis/scorecard-packs/{id}")]
    public IActionResult KpiScorecardPackDetail(string id)
    {
        ViewData["ScorecardPackId"] = id;
        SetViewData(ViewData, "Scorecard Pack Detail", "kpis", "Pack composition and governance metadata.");
        return View("KpiScorecardPackDetail");
    }

    [HttpGet("kpis/governance")]
    public IActionResult KpiGovernance()
    {
        SetViewData(ViewData, "KPI Governance", "kpis", "Lifecycle governance, quality exceptions, and audit visibility.");
        return View("KpiGovernance");
    }

    [HttpGet("kpis/new")]
    public IActionResult KpiCreate()
    {
        SetViewData(ViewData, "Create KPI", "kpis", "Create KPI definition with measurement, ownership, and governance metadata.");
        return View("KpiEditor");
    }

    [HttpGet("kpis/{kpiId}")]
    public IActionResult KpiDetail(string kpiId)
    {
        SetViewData(ViewData, "KPI Detail", "kpis", "KPI definition, usage context, and governance traceability.");
        ViewData["KpiId"] = kpiId;
        return View("KpiDetail");
    }

    [HttpGet("kpis/{kpiId}/edit")]
    public IActionResult KpiEdit(string kpiId)
    {
        SetViewData(ViewData, "Edit KPI", "kpis", "Update KPI definition and ownership metadata.");
        ViewData["KpiId"] = kpiId;
        return View("KpiEditor");
    }

    [HttpGet("kpis/ownership")]
    public IActionResult KpiOwnership()
    {
        SetViewData(ViewData, "Metric Ownership", "kpis", "Ownership accountability and stewardship coverage for KPIs.");
        return View("KpiOwnership");
    }

    [HttpGet("kpis/scorecard")]
    public IActionResult KpiScorecard()
    {
        SetViewData(ViewData, "Scorecard Dashboard", "kpis", "Executive KPI monitoring and strategic scorecard rollup.");
        return View("KpiScorecard");
    }

    [HttpGet("cascade/builder")]
    public IActionResult CascadeBuilder()
    {
        SetViewData(ViewData, "Cascade Builder", "cascade", "Top-down strategic target cascade to objective execution layers.");
        return View("CascadeBuilder");
    }

    [HttpGet("cascade/target-allocation")]
    public IActionResult TargetAllocation()
    {
        SetViewData(ViewData, "Target Allocation", "cascade", "Allocate parent-level targets across objectives and companies.");
        return View("TargetAllocation");
    }

    [HttpGet("cascade/consolidation")]
    public IActionResult ConsolidationView()
    {
        SetViewData(ViewData, "Consolidation View", "cascade", "Bottom-up consolidation of objective and company performance.");
        return View("ConsolidationView");
    }

    [HttpGet("cascade/variance")]
    public IActionResult VarianceAnalysis()
    {
        SetViewData(ViewData, "Variance Analysis", "cascade", "Variance diagnostics across goals, objectives, and KPI scorecards.");
        return View("VarianceAnalysis");
    }

    [HttpGet("reviews/calendar")]
    public IActionResult ReviewCalendar()
    {
        SetViewData(ViewData, "Review Calendar", "reviews", "Strategic review cadence, ownership, and session readiness.");
        return View("ReviewCalendar");
    }

    [HttpGet("reviews/pack")]
    public IActionResult ReviewPack()
    {
        SetViewData(ViewData, "Review Pack", "reviews", "Structured strategic review pack with scorecard and cascade highlights.");
        return View("ReviewPack");
    }

    [HttpGet("reviews/decisions")]
    public IActionResult DecisionsActions()
    {
        SetViewData(ViewData, "Decisions & Actions", "reviews", "Track strategic decisions and follow-up action closure.");
        return View("DecisionsActions");
    }

    [HttpGet("reviews/history")]
    public IActionResult ReviewHistory()
    {
        SetViewData(ViewData, "Review History", "reviews", "Historical review trail with decisions and closure outcomes.");
        return View("ReviewHistory");
    }

    [HttpGet("connections/gaps")]
    public IActionResult ConnectionsGaps()
    {
        SetViewData(ViewData, "Strategy Alignment Register - Gap View", "connections", "Operational remediation view for alignment and planning gaps.");
        return View("ConnectionsGap");
    }

    [HttpGet("connections/lineage")]
    public IActionResult ConnectionsLineage()
    {
        SetViewData(ViewData, "Strategy Alignment Register - Lineage View", "connections", "Focused upstream/downstream lineage context for selected alignment rows.");
        return View("ConnectionsLineage");
    }

    [HttpGet("connections/graph")]
    public IActionResult ConnectionsGraph()
    {
        SetViewData(ViewData, "Strategy Alignment Register - Graph View", "connections", "Secondary exploratory graph view for strategy linkage.");
        return View("ConnectionsGraph");
    }

    private static void SetViewData(Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary viewData, string title, string activeTab, string subtitle, string[]? defaultBreadcrumbs = null)
    {
        viewData["Title"] = title;
        viewData["ActiveTab"] = activeTab.ToLowerInvariant();
        viewData["Subtitle"] = subtitle;
        var breadcrumbs = new System.Collections.Generic.List<string> { "Management & Governance", "Enterprise Strategy & Business Performance", title };
        if (defaultBreadcrumbs != null)
        {
            breadcrumbs.AddRange(defaultBreadcrumbs);
        }
        viewData["Breadcrumbs"] = breadcrumbs.ToArray();
    }

}
