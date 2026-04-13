using Diten.WebUI.Config;
using Diten.WebUI.Models.ManagementGovernance;
using Diten.WebUI.Services.ManagementGovernance;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebUI.Controllers;

[Route("management-governance")]
public sealed class ManagementGovernanceController : Controller
{
    private readonly IManagementGovernanceFrontendAdapter _adapter;

    public ManagementGovernanceController(IManagementGovernanceFrontendAdapter adapter)
    {
        _adapter = adapter;
    }

    [HttpGet("")]
    public IActionResult Index() => View("Index", BuildBaseModel("Overview", "Executive orchestration shell across governance subdomains.", "overview", new[] { "Management & Governance" }));

    [HttpGet("executive-cockpit")]
    public IActionResult ExecutiveCockpit() => View("ExecutiveCockpit", BuildBaseModel("Executive Cockpit", "Consolidated strategic and governance roll-up.", "executive-cockpit", new[] { "Management & Governance", "Executive Cockpit" }));

    [HttpGet("work-queue")]
    public IActionResult WorkQueue() => View("WorkQueue", BuildBaseModel("Work Queue", "Unified governance inbox across subdomains and modules.", "work-queue", new[] { "Management & Governance", "Work Queue" }));

    [HttpGet("cadence")]
    public IActionResult Cadence() => View("Cadence", BuildBaseModel("Cadence", "Review and governance calendar shell.", "cadence", new[] { "Management & Governance", "Cadence" }));

    [HttpGet("search")]
    public IActionResult Search() => View("Search", BuildBaseModel("Search", "Federated search shell across governance workspaces.", "search", new[] { "Management & Governance", "Search" }));

    [HttpGet("subdomains")]
    public IActionResult Subdomains() => View("Subdomains", BuildBaseModel("Subdomains", "Subdomain aggregation and navigation hub.", "subdomains", new[] { "Management & Governance", "Subdomains" }));

    [HttpGet("reports")]
    public IActionResult Reports() => View("Reports", BuildBaseModel("Reports", "Cross-subdomain reporting shell and export entry points.", "reports", new[] { "Management & Governance", "Reports" }));

    [HttpGet("recent-activity")]
    public IActionResult RecentActivity() => View("RecentActivity", BuildBaseModel("Recent Activity", "Audit-oriented stream of governance actions and evidence.", "recent-activity", new[] { "Management & Governance", "Recent Activity" }));

    [HttpGet("favorites")]
    public IActionResult Favorites() => View("Favorites", BuildBaseModel("Favorites", "Pinned subdomains and module shortcuts.", "favorites", new[] { "Management & Governance", "Favorites" }));

    [HttpGet("subdomains/{slug}")]
    public IActionResult Subdomain(string slug)
    {
        if (string.Equals(slug, "enterprise-strategy-business-performance", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/management-governance/enterprise-strategy-business-performance");
        }

        if (string.Equals(slug, "delivery-execution-management", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/management-governance/delivery-execution");
        }

        var subdomain = _adapter.UseSubdomainSummary(slug);
        if (subdomain == null)
        {
            return NotFound();
        }

        var vm = BuildBaseModel(subdomain.Name, subdomain.Description, "subdomains", new[] { "Management & Governance", "Subdomains", subdomain.Name });
        vm = new ManagementGovernancePageViewModel
        {
            PageTitle = vm.PageTitle,
            PageSubtitle = vm.PageSubtitle,
            ActiveNav = vm.ActiveNav,
            Domain = vm.Domain,
            Subdomains = vm.Subdomains,
            CurrentSubdomain = subdomain,
            WorkQueue = vm.WorkQueue,
            GovernanceEvents = vm.GovernanceEvents,
            ReviewCycles = vm.ReviewCycles,
            RiskSignals = vm.RiskSignals,
            RecentActivity = vm.RecentActivity,
            Breadcrumbs = vm.Breadcrumbs,
            AppliedFilters = vm.AppliedFilters,
            Permissions = vm.Permissions
        };
        return View("Subdomain", vm);
    }

    [HttpGet("subdomains/demand-ideas-opportunity-management/demand-ideas")]
    public IActionResult DemandIdeasWorkspace() =>
        View("SubdomainWorkspace",
            BuildWorkspaceModel(
                workspaceTitle: "Demand & Ideas",
                workspaceDescription: "Workspace entry under Demand, Ideas & Opportunity Management.",
                workspaceRoute: "/DemandIdeas",
                subdomainSlug: ManagementGovernanceRegistry.DemandIdeasSubdomainSlug));

    [HttpGet("subdomains/demand-ideas-opportunity-management/decomposition-tree-builder")]
    public IActionResult DecompositionTreeBuilderWorkspace() =>
        View("SubdomainWorkspace",
            BuildWorkspaceModel(
                workspaceTitle: "Decomposition Tree Builder",
                workspaceDescription: "Workspace entry under Demand, Ideas & Opportunity Management.",
                workspaceRoute: "/DecompositionTreeBuilder/Index",
                subdomainSlug: ManagementGovernanceRegistry.DemandIdeasSubdomainSlug));

    [HttpGet("modules/{moduleId}")]
    public IActionResult Module(string moduleId)
    {
        if (!ManagementGovernanceRegistry.ModulesById.TryGetValue(moduleId, out var module))
        {
            return NotFound();
        }

        return View("ModulePlaceholder", BuildBaseModel(module.Name, module.Description, "subdomains", new[] { "Management & Governance", "Module", module.Name }));
    }

    private ManagementGovernancePageViewModel BuildBaseModel(
        string title,
        string subtitle,
        string activeNav,
        IReadOnlyList<string> breadcrumbs)
    {
        return new ManagementGovernancePageViewModel
        {
            PageTitle = title,
            PageSubtitle = subtitle,
            ActiveNav = activeNav,
            Domain = _adapter.UseManagementGovernanceSummary(),
            Subdomains = ManagementGovernanceRegistry.Subdomains,
            WorkQueue = _adapter.UseGovernanceWorkQueue(),
            GovernanceEvents = _adapter.UseGovernanceCadence(),
            ReviewCycles = _adapter.UseGovernanceReviewCycles(),
            RiskSignals = _adapter.UseRiskSignals(),
            RecentActivity = _adapter.UseRecentGovernanceActivity(),
            Breadcrumbs = breadcrumbs,
            Tabs = ManagementGovernanceRegistry.DomainTabs,
            Permissions = new ManagementGovernancePermissions
            {
                CanApprove = true,
                CanAssign = true,
                CanEscalate = true,
                CanViewAuditTrail = true,
                CanViewEvidence = true
            }
        };
    }

    private ManagementGovernancePageViewModel BuildWorkspaceModel(
        string workspaceTitle,
        string workspaceDescription,
        string workspaceRoute,
        string subdomainSlug)
    {
        var subdomain = _adapter.UseSubdomainSummary(subdomainSlug);
        var vm = BuildBaseModel(
            workspaceTitle,
            workspaceDescription,
            "subdomains",
            new[] { "Management & Governance", "Subdomains", subdomain?.Name ?? subdomainSlug, workspaceTitle });

        return new ManagementGovernancePageViewModel
        {
            PageTitle = vm.PageTitle,
            PageSubtitle = vm.PageSubtitle,
            ActiveNav = vm.ActiveNav,
            Domain = vm.Domain,
            Subdomains = vm.Subdomains,
            CurrentSubdomain = subdomain,
            WorkQueue = vm.WorkQueue,
            GovernanceEvents = vm.GovernanceEvents,
            ReviewCycles = vm.ReviewCycles,
            RiskSignals = vm.RiskSignals,
            RecentActivity = vm.RecentActivity,
            Breadcrumbs = vm.Breadcrumbs,
            Tabs = vm.Tabs,
            AppliedFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["WorkspaceRoute"] = workspaceRoute
            },
            Permissions = vm.Permissions
        };
    }
}
