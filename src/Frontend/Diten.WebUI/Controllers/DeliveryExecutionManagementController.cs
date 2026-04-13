using Diten.WebUI.Config;
using Diten.WebUI.Models.EnterpriseStrategy;
using Diten.WebUI.Services.EnterpriseStrategy;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebUI.Controllers;

[Route("management-governance/delivery-execution")]
public sealed class DeliveryExecutionManagementController : Controller
{
    private readonly IEnterpriseStrategyFrontendAdapter _adapter;

    public DeliveryExecutionManagementController(IEnterpriseStrategyFrontendAdapter adapter)
    {
        _adapter = adapter;
    }

    [HttpGet("/management-governance/delivery-execution-management")]
    public IActionResult LegacyOverviewRedirect() => Redirect(DeliveryExecutionManagementRegistry.BaseRoute);

    [HttpGet("/management-governance/delivery-execution-management/initiatives")]
    public IActionResult LegacyInitiativesRedirect() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives");

    [HttpGet("/management-governance/delivery-execution-management/initiatives/new")]
    public IActionResult LegacyInitiativeCreateRedirect([FromQuery] string? parentObjectiveId = null)
        => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/new{(string.IsNullOrWhiteSpace(parentObjectiveId) ? string.Empty : $"?parentObjectiveId={Uri.EscapeDataString(parentObjectiveId)}")}");

    [HttpGet("/management-governance/delivery-execution-management/initiatives/{initiativeId}")]
    public IActionResult LegacyInitiativeDetailRedirect(string initiativeId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/{Uri.EscapeDataString(initiativeId)}");

    [HttpGet("/management-governance/delivery-execution-management/initiatives/{initiativeId}/edit")]
    public IActionResult LegacyInitiativeEditRedirect(string initiativeId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/initiatives/{Uri.EscapeDataString(initiativeId)}/edit");

    [HttpGet("/management-governance/delivery-execution-management/projects")]
    public IActionResult LegacyProjectsRedirect() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/projects");

    [HttpGet("/management-governance/delivery-execution-management/projects/new")]
    public IActionResult LegacyProjectCreateRedirect([FromQuery] string? parentInitiativeId = null)
        => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/projects/new{(string.IsNullOrWhiteSpace(parentInitiativeId) ? string.Empty : $"?parentInitiativeId={Uri.EscapeDataString(parentInitiativeId)}")}");

    [HttpGet("/management-governance/delivery-execution-management/projects/{projectId}")]
    public IActionResult LegacyProjectDetailRedirect(string projectId) => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/projects/{Uri.EscapeDataString(projectId)}");

    [HttpGet("/management-governance/delivery-execution-management/programs")]
    public IActionResult LegacyProgramsRedirect() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/programs");

    [HttpGet("/management-governance/delivery-execution-management/delivery-map")]
    public IActionResult LegacyDeliveryMapRedirect() => Redirect($"{DeliveryExecutionManagementRegistry.BaseRoute}/delivery-map");

    [HttpGet("")]
    public IActionResult Overview() => View("Overview", BuildVm("Overview", "overview", "Native home for delivery execution, initiatives, projects, and dependency visibility."));

    [HttpGet("initiatives")]
    public IActionResult Initiatives() => View("Initiatives", BuildVm("Initiatives", "initiatives", "Delivery-owned initiative orchestration, CRUD, and downstream traceability."));

    [HttpGet("initiatives/new")]
    public IActionResult InitiativeCreate([FromQuery] string? parentObjectiveId = null)
    {
        if (!string.IsNullOrWhiteSpace(parentObjectiveId))
            ViewData["PrefillParentObjectiveId"] = parentObjectiveId.Trim();

        return View("InitiativeCreate", BuildVm("Create Initiative", "initiatives", "Create a delivery initiative linked to a parent objective with contribution planning and readiness validation."));
    }

    [HttpGet("initiatives/{initiativeId}/edit")]
    public IActionResult InitiativeEdit(string initiativeId)
    {
        ViewData["InitiativeEditId"] = initiativeId;
        return View("InitiativeCreate", BuildVm("Edit Initiative", "initiatives", "Update a delivery initiative while preserving objective-linked planning context."));
    }

    [HttpGet("initiatives/{initiativeId}")]
    public IActionResult InitiativeDetail(string initiativeId)
    {
        ViewData["InitiativeId"] = initiativeId;
        return View(
            "InitiativeDetail",
            BuildVm(
                "Initiative Detail",
                "initiatives",
                "Delivery-owned initiative detail with execution context and strategy alignment references.",
                initiativeId,
                "Initiatives"));
    }

    [HttpGet("projects")]
    public IActionResult Projects() => View("~/Views/EnterpriseStrategyBusinessPerformance/Projects.cshtml", BuildVm("Projects", "projects", "Delivery-owned project orchestration, CRUD, and traceability."));

    [HttpGet("projects/new")]
    public IActionResult ProjectCreate([FromQuery] string? parentInitiativeId = null)
    {
        if (!string.IsNullOrWhiteSpace(parentInitiativeId))
            ViewData["PrefillParentInitiativeId"] = parentInitiativeId.Trim();

        return View("ProjectCreate", BuildVm("Create Project", "projects", "Create a governed project linked to a parent initiative with anchored lineage, delivery controls, and budget governance."));
    }

    [HttpGet("projects/{projectId}")]
    public IActionResult ProjectDetail(string projectId)
    {
        ViewData["ProjectId"] = projectId;
        return View(
            "~/Views/EnterpriseStrategyBusinessPerformance/ProjectDetail.cshtml",
            BuildVm(
                "Project Detail",
                "projects",
                "Delivery-owned project detail with execution signals and strategy lineage references.",
                projectId,
                "Projects"));
    }

    [HttpGet("programs")]
    public IActionResult Programs() => View("Programs", BuildVm("Programs", "programs", "Program-level workspace shell under Delivery & Execution Management."));

    [HttpGet("delivery-map")]
    public IActionResult DeliveryMap() => View("DeliveryMap", BuildVm("Delivery Map / Dependencies", "delivery-map", "Cross-initiative and cross-project dependency visibility under the delivery subdomain."));

    private EnterpriseStrategyPageViewModel BuildVm(
        string title,
        string activeTab,
        string subtitle,
        string? leafBreadcrumb = null,
        string? sectionBreadcrumb = null)
    {
        var goals = _adapter.UseGoals();
        var objectives = _adapter.UseObjectives();
        var connections = _adapter.UseConnections();
        var initiativeLinks = _adapter.UseInitiativeLinks();
        var projectLinks = _adapter.UseProjectLinks();

        var breadcrumbs = new List<string>
        {
            "Management & Governance",
            "Delivery & Execution Management"
        };

        var section = string.IsNullOrWhiteSpace(sectionBreadcrumb) ? title : sectionBreadcrumb;

        if (!string.Equals(section, "Overview", StringComparison.OrdinalIgnoreCase))
        {
            breadcrumbs.Add(section);
        }

        if (!string.IsNullOrWhiteSpace(leafBreadcrumb))
        {
            breadcrumbs.Add(leafBreadcrumb);
        }

        return new EnterpriseStrategyPageViewModel
        {
            Title = title,
            Subtitle = subtitle,
            ActiveTab = activeTab,
            Breadcrumbs = breadcrumbs,
            Tabs = DeliveryExecutionManagementRegistry.Tabs,
            Goals = goals,
            Objectives = objectives,
            Connections = connections,
            InitiativeLinks = initiativeLinks,
            ProjectLinks = projectLinks,
            MetricCards = BuildMetricCards(activeTab, goals, objectives, connections, initiativeLinks, projectLinks),
            IsLoading = false,
            HasError = false,
            AccessDenied = false
        };
    }

    private static IReadOnlyList<StrategyMetricSummaryCard> BuildMetricCards(
        string activeTab,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<Objective> objectives,
        IReadOnlyList<StrategyConnection> connections,
        IReadOnlyList<InitiativeStrategyLinkView> initiativeLinks,
        IReadOnlyList<ProjectStrategyLinkView> projectLinks)
    {
        var linkedInitiatives = initiativeLinks.Count;
        var linkedProjects = projectLinks.Count;
        var initiativesUnderReview = initiativeLinks.Count(x => string.Equals(x.TraceabilityStatus, "Under Review", StringComparison.OrdinalIgnoreCase));
        var blockedProjects = projectLinks.Count(x => string.Equals(x.TraceabilityStatus, "Blocked", StringComparison.OrdinalIgnoreCase));
        var linkedGoals = goals.Count;
        var linkedObjectives = objectives.Count;
        var alignmentRows = connections.Count;

        return activeTab switch
        {
            "initiatives" => new[]
            {
                Card("Managed Initiatives", linkedInitiatives, $"{initiativesUnderReview} under review"),
                Card("Aligned Objectives", linkedObjectives, "reference layer in ES&BP"),
                Card("Aligned Goals", linkedGoals, "reference layer in ES&BP")
            },
            "projects" => new[]
            {
                Card("Managed Projects", linkedProjects, $"{blockedProjects} blocked"),
                Card("Parent Initiatives", linkedInitiatives, "delivery hierarchy"),
                Card("Alignment Rows", alignmentRows, "reference lineage")
            },
            "programs" => new[]
            {
                Card("Programs Shell", linkedInitiatives, "ready to absorb existing delivery logic"),
                Card("Initiative Coverage", linkedInitiatives, "delivery-owned"),
                Card("Project Coverage", linkedProjects, "delivery-owned")
            },
            "delivery-map" => new[]
            {
                Card("Dependencies Shell", blockedProjects + initiativesUnderReview, "open delivery risks"),
                Card("Traceability Rows", alignmentRows, "strategy reference coverage"),
                Card("Projects in Scope", linkedProjects, "delivery-owned")
            },
            _ => new[]
            {
                Card("Initiatives", linkedInitiatives, $"{initiativesUnderReview} under review"),
                Card("Projects", linkedProjects, $"{blockedProjects} blocked"),
                Card("Alignment References", alignmentRows, "ES&BP consumer layer")
            }
        };
    }

    private static StrategyMetricSummaryCard Card(string label, int value, string trend) =>
        new()
        {
            Label = label,
            Value = value.ToString(),
            Trend = trend
        };
}
