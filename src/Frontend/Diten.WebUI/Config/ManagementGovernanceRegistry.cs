using Diten.WebUI.Models.ManagementGovernance;

namespace Diten.WebUI.Config;

public static class ManagementGovernanceRegistry
{
    public const string DomainRootRoute = "/management-governance";
    public const string DemandIdeasSubdomainSlug = "demand-ideas-opportunity-management";
    public const string DemandIdeasNestedRoute = "/management-governance/subdomains/demand-ideas-opportunity-management/demand-ideas";
    public const string DecompositionNestedRoute = "/management-governance/subdomains/demand-ideas-opportunity-management/decomposition-tree-builder";

    public static IReadOnlyList<DomainShellTab> DomainTabs => new List<DomainShellTab>
    {
        new() { Key = "overview", Label = "Overview", Route = "/management-governance" },
        new() { Key = "executive-cockpit", Label = "Executive Cockpit", Route = "/management-governance/executive-cockpit" },
        new() { Key = "work-queue", Label = "Work Queue", Route = "/management-governance/work-queue" },
        new() { Key = "cadence", Label = "Cadence", Route = "/management-governance/cadence" },
        new() { Key = "search", Label = "Search", Route = "/management-governance/search" },
        new() { Key = "subdomains", Label = "Subdomains", Route = "/management-governance/subdomains" },
        new() { Key = "reports", Label = "Reports", Route = "/management-governance/reports" },
        new() { Key = "recent-activity", Label = "Recent Activity", Route = "/management-governance/recent-activity" }
    };

    private static readonly HashSet<string> SidebarSubdomainSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "enterprise-strategy-business-performance",
        "demand-ideas-opportunity-management",
        "portfolio-investment-value-management",
        "delivery-execution-management",
        "resource-capacity-management",
        "business-process-management",
        "change-transformation-management",
        "governance-risk-control",
        "decisioning-reviews-management-cadence"
    };

    public static IReadOnlyList<SubdomainSummary> SidebarSubdomains =>
        Subdomains.Where(x => SidebarSubdomainSlugs.Contains(x.Slug)).ToList();

    public static IReadOnlyList<SubdomainSummary> Subdomains => new List<SubdomainSummary>
    {
        BuildSubdomain(
            id: "sd-strategy-performance",
            name: "Enterprise Strategy & Business Performance",
            slug: "enterprise-strategy-business-performance",
            description: "Strategic goals, scorecards, cascades, and review governance.",
            owner: "Strategy Office",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "strategy", "performance", "scorecard" },
            dependencies: new[] { "portfolio-investment-value-management" },
            routeOverride: "/management-governance/enterprise-strategy-business-performance",
            modules: new[]
            {
                Module("mod-strategic-goals", "Strategic Goals Management", "strategic-goals-management", "Define and track enterprise strategic goals.", "Active", "Strategy Office", "Medium", "/management-governance/modules/mod-strategic-goals", false),
                Module("mod-objectives-management", "Objectives Management", "objectives-management", "Manage objective hierarchies and progress rollups.", "Active", "Strategy Office", "Low", "/management-governance/modules/mod-objectives-management", false),
                Module("mod-kpi-scorecard", "KPI & Scorecard Management", "kpi-scorecard-management", "Scorecard publication and KPI variance visibility.", "Active", "Performance Office", "Medium", "/management-governance/enterprise-strategy-business-performance/kpis", true),
                Module("mod-goal-cascade", "Goal Cascade & Consolidation", "goal-cascade-consolidation", "Cross-unit cascade and consolidation controls.", "Monitor", "Strategy Office", "High", "/management-governance/enterprise-strategy-business-performance/cascade/builder", true),
                Module("mod-strategic-review", "Strategic Review Management", "strategic-review-management", "Review packs and strategic meeting cadence.", "Active", "Executive PMO", "Medium", "/management-governance/enterprise-strategy-business-performance/reviews/calendar", true)
            },
            sidebarChildren: new[]
            {
                new SidebarNavChild { Label = "Overview", Route = "/management-governance/enterprise-strategy-business-performance" },
                new SidebarNavChild { Label = "Planning & Periods", Route = "/management-governance/enterprise-strategy-business-performance/planning" },
                new SidebarNavChild { Label = "Goals", Route = "/management-governance/enterprise-strategy-business-performance/goals" },
                new SidebarNavChild { Label = "Objectives", Route = "/management-governance/enterprise-strategy-business-performance/objectives" },
                new SidebarNavChild { Label = "Strategy Alignment Register", Route = "/management-governance/enterprise-strategy-business-performance/connections" },
                new SidebarNavChild { Label = "KPI & Scorecard", Route = "/management-governance/enterprise-strategy-business-performance/kpis" },
                new SidebarNavChild { Label = "Cascade & Consolidation", Route = "/management-governance/enterprise-strategy-business-performance/cascade" },
                new SidebarNavChild { Label = "Strategic Reviews", Route = "/management-governance/enterprise-strategy-business-performance/reviews" }
            }),

        BuildSubdomain(
            id: "sd-demand-ideas-opportunity",
            name: "Demand, Ideas & Opportunity Management",
            slug: "demand-ideas-opportunity-management",
            description: "Intake, triage, and early business case orchestration.",
            owner: "Intake Office",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "demand", "ideas", "triage" },
            dependencies: new[] { "portfolio-investment-value-management", "delivery-execution-management" },
            modules: new[]
            {
                Module("mod-demand-ideas", "Demand & Ideas", "demand-ideas", "Demand intake and idea capture workspace.", "Active", "Intake Office", "Medium", DemandIdeasNestedRoute, true),
                Module("mod-decomposition-tree-builder", "Decomposition Tree Builder", "decomposition-tree-builder", "Decomposition planning workspace.", "Active", "Intake Office", "Medium", DecompositionNestedRoute, true),
                Module("mod-demand-triage", "Demand Qualification & Triage", "demand-qualification-triage", "Qualification workflow and triage decisions.", "Monitor", "Intake Office", "High", "/DemandIdeas/Index", true),
                Module("mod-early-business-case", "Early Business Case Management", "early-business-case-management", "Early case completeness and evidence controls.", "Active", "Portfolio Office", "Medium", "/DemandIdeas/Capture", true)
            },
            sidebarChildren: new[]
            {
                new SidebarNavChild { Label = "Demand & Ideas", Route = DemandIdeasNestedRoute },
                new SidebarNavChild { Label = "Decomposition Tree Builder", Route = DecompositionNestedRoute }
            }),

        BuildSubdomain(
            id: "sd-portfolio-investment-value",
            name: "Portfolio, Investment & Value Management",
            slug: "portfolio-investment-value-management",
            description: "Portfolio shaping, investment control, and value realization.",
            owner: "Portfolio Office",
            riskLevel: "High",
            status: "Monitor",
            tags: new[] { "portfolio", "investment", "value" },
            dependencies: new[] { "enterprise-strategy-business-performance", "delivery-execution-management" },
            modules: new[]
            {
                Module("mod-portfolio-management", "Portfolio Management", "portfolio-management", "Portfolio prioritization and balancing.", "Monitor", "Portfolio Office", "High", "/management-governance/modules/mod-portfolio-management", false),
                Module("mod-investment-governance", "Investment Governance", "investment-governance", "Investment forum decisions and controls.", "Monitor", "Finance Governance", "High", "/management-governance/modules/mod-investment-governance", false),
                Module("mod-benefits-value", "Benefits & Value Tracking", "benefits-value-tracking", "Realization milestones and value deltas.", "Active", "Value Office", "Medium", "/management-governance/modules/mod-benefits-value", false),
                Module("mod-capital-allocation", "Capital Allocation Management", "capital-allocation-management", "Capital envelope planning and approvals.", "Active", "Finance Governance", "Medium", "/management-governance/modules/mod-capital-allocation", false)
            }),

        BuildSubdomain(
            id: "sd-delivery-execution",
            name: "Delivery & Execution Management",
            slug: "delivery-execution-management",
            description: "Delivery progress, milestones, and dependency visibility.",
            owner: "Delivery PMO",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "delivery", "execution", "dependency" },
            dependencies: new[] { "resource-capacity-management", "decisioning-reviews-management-cadence" },
            routeOverride: "/management-governance/delivery-execution",
            modules: new[]
            {
                Module("mod-initiative-management", "Initiative Management", "initiative-management", "Initiative-level orchestration and status.", "Active", "Delivery PMO", "Medium", "/management-governance/delivery-execution/initiatives", true),
                Module("mod-program-project-management", "Program & Project Management", "program-project-management", "Program/project governance and health rollups.", "Active", "Delivery PMO", "Medium", "/management-governance/delivery-execution/projects", true),
                Module("mod-milestone-dependency", "Milestone & Dependency Management", "milestone-dependency-management", "Critical path and dependency blockers.", "Monitor", "Delivery PMO", "High", "/management-governance/delivery-execution/delivery-map", true),
                Module("mod-delivery-status", "Delivery Status Reporting", "delivery-status-reporting", "Cross-portfolio status signal publication.", "Active", "Delivery PMO", "Low", "/TaskReports/TaskReport", true)
            },
            sidebarChildren: new[]
            {
                new SidebarNavChild { Label = "Overview", Route = "/management-governance/delivery-execution" },
                new SidebarNavChild { Label = "Initiatives", Route = "/management-governance/delivery-execution/initiatives" },
                new SidebarNavChild { Label = "Projects", Route = "/management-governance/delivery-execution/projects" },
                new SidebarNavChild { Label = "Programs", Route = "/management-governance/delivery-execution/programs" },
                new SidebarNavChild { Label = "Delivery Map / Dependencies", Route = "/management-governance/delivery-execution/delivery-map" }
            }),

        BuildSubdomain(
            id: "sd-resource-capacity",
            name: "Resource & Capacity Management",
            slug: "resource-capacity-management",
            description: "Capacity visibility and allocation governance.",
            owner: "Workforce Planning",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "capacity", "allocation", "utilization" },
            dependencies: new[] { "delivery-execution-management" },
            modules: new[]
            {
                Module("mod-resource-planning", "Resource Planning", "resource-planning", "Resource demand and staffing plans.", "Active", "Workforce Planning", "Medium", "/management-governance/modules/mod-resource-planning", false),
                Module("mod-capacity-planning", "Capacity Planning", "capacity-planning", "Capacity envelopes and forecasting.", "Monitor", "Workforce Planning", "High", "/management-governance/modules/mod-capacity-planning", false),
                Module("mod-utilization-allocation", "Utilization & Allocation Tracking", "utilization-allocation-tracking", "Utilization signals and rebalancing prompts.", "Active", "Workforce Planning", "Medium", "/management-governance/modules/mod-utilization-allocation", false),
                Module("mod-skills-availability", "Skills & Availability Management", "skills-availability-management", "Skills inventory and availability matching.", "Active", "Talent Office", "Low", "/management-governance/modules/mod-skills-availability", false)
            }),

        BuildSubdomain(
            id: "sd-business-process",
            name: "Business Process Management",
            slug: "business-process-management",
            description: "Process architecture, model health, and improvement pipeline.",
            owner: "Process Excellence",
            riskLevel: "Low",
            status: "Active",
            tags: new[] { "process", "modeling", "improvement" },
            dependencies: new[] { "change-transformation-management" },
            modules: new[]
            {
                Module("mod-process-architecture", "Process Architecture Management", "process-architecture-management", "Process landscape and ownership governance.", "Active", "Process Excellence", "Low", "/management-governance/modules/mod-process-architecture", false),
                Module("mod-process-design", "Process Design & Modeling", "process-design-modeling", "Design/model quality and lifecycle controls.", "Active", "Process Excellence", "Medium", "/management-governance/modules/mod-process-design", false),
                Module("mod-process-performance", "Process Performance Management", "process-performance-management", "Process KPI and throughput visibility.", "Monitor", "Process Excellence", "Medium", "/management-governance/modules/mod-process-performance", false),
                Module("mod-process-improvement", "Process Improvement Management", "process-improvement-management", "Improvement backlog and execution health.", "Active", "Process Excellence", "Low", "/management-governance/modules/mod-process-improvement", false)
            }),

        BuildSubdomain(
            id: "sd-change-transformation",
            name: "Change & Transformation Management",
            slug: "change-transformation-management",
            description: "Transformation progress, readiness, and adoption signals.",
            owner: "Transformation Office",
            riskLevel: "High",
            status: "Monitor",
            tags: new[] { "transformation", "change", "adoption" },
            dependencies: new[] { "delivery-execution-management", "enterprise-strategy-business-performance" },
            modules: new[]
            {
                Module("mod-transformation-roadmap", "Transformation Roadmap Management", "transformation-roadmap-management", "Roadmap synchronization and dependencies.", "Monitor", "Transformation Office", "High", "/management-governance/modules/mod-transformation-roadmap", false),
                Module("mod-change-impact-readiness", "Change Impact & Readiness Management", "change-impact-readiness-management", "Readiness evidence and impact views.", "Active", "Transformation Office", "Medium", "/management-governance/modules/mod-change-impact-readiness", false),
                Module("mod-stakeholder-adoption", "Stakeholder & Adoption Management", "stakeholder-adoption-management", "Adoption pulse and stakeholder actions.", "Active", "Transformation Office", "Medium", "/management-governance/modules/mod-stakeholder-adoption", false),
                Module("mod-transformation-governance", "Transformation Governance & Reporting", "transformation-governance-reporting", "Governance reporting for transformation outcomes.", "Monitor", "Transformation Office", "High", "/management-governance/modules/mod-transformation-governance", false)
            }),

        BuildSubdomain(
            id: "sd-governance-risk-control",
            name: "Governance, Risk & Control",
            slug: "governance-risk-control",
            description: "Risk, controls, compliance, and assurance oversight.",
            owner: "Risk & Compliance",
            riskLevel: "High",
            status: "Active",
            tags: new[] { "risk", "control", "compliance" },
            dependencies: new[] { "policy-authority-organizational-governance" },
            modules: new[]
            {
                Module("mod-risk-management", "Risk Management", "risk-management", "Risk register signals and actions.", "Active", "Risk Office", "High", "/InventoryGovernance/Index", true),
                Module("mod-internal-control", "Internal Control Management", "internal-control-management", "Control catalog and remediation visibility.", "Monitor", "Control Office", "High", "/management-governance/modules/mod-internal-control", false),
                Module("mod-compliance-oversight", "Compliance Oversight", "compliance-oversight", "Compliance obligations and breach alerts.", "Active", "Compliance Office", "High", "/management-governance/modules/mod-compliance-oversight", false),
                Module("mod-assurance-resilience", "Assurance & Resilience Oversight", "assurance-resilience-oversight", "Assurance plan execution and resilience posture.", "Monitor", "Assurance Office", "Medium", "/management-governance/modules/mod-assurance-resilience", false)
            }),

        BuildSubdomain(
            id: "sd-decisioning-reviews-cadence",
            name: "Decisioning, Reviews & Management Cadence",
            slug: "decisioning-reviews-management-cadence",
            description: "Review cycles, forums, stage gates, and action closure.",
            owner: "Governance Secretariat",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "cadence", "decisions", "reviews" },
            dependencies: new[] { "enterprise-strategy-business-performance", "portfolio-investment-value-management" },
            modules: new[]
            {
                Module("mod-review-cycle", "Review Cycle Management", "review-cycle-management", "Cadenced review cycles and schedules.", "Active", "Governance Secretariat", "Medium", "/management-governance/cadence", true),
                Module("mod-stage-gate", "Stage Gate Management", "stage-gate-management", "Stage gate control points and outcomes.", "Active", "Governance Secretariat", "Medium", "/management-governance/cadence", true),
                Module("mod-governance-forum", "Governance Forum Management", "governance-forum-management", "Forum agendas, packs, and records.", "Monitor", "Governance Secretariat", "Medium", "/management-governance/cadence", true),
                Module("mod-decision-log-action", "Decision Log & Action Tracking", "decision-log-action-tracking", "Decision records and action aging.", "Active", "Governance Secretariat", "Medium", "/management-governance/work-queue", true)
            }),

        BuildSubdomain(
            id: "sd-policy-authority-org-governance",
            name: "Policy, Authority & Organizational Governance",
            slug: "policy-authority-organizational-governance",
            description: "Policy model, delegated authority, and governance ownership.",
            owner: "Corporate Governance",
            riskLevel: "Medium",
            status: "Active",
            tags: new[] { "policy", "authority", "ownership" },
            dependencies: new[] { "governance-risk-control" },
            modules: new[]
            {
                Module("mod-policy-management", "Policy Management", "policy-management", "Policy lifecycle and attestation coverage.", "Active", "Corporate Governance", "Medium", "/management-governance/modules/mod-policy-management", false),
                Module("mod-delegated-authority", "Delegated Authority Management", "delegated-authority-management", "Delegation matrix and decision rights.", "Monitor", "Corporate Governance", "High", "/management-governance/modules/mod-delegated-authority", false),
                Module("mod-governance-charter", "Governance Charter Management", "governance-charter-management", "Forum charter controls and versioning.", "Active", "Corporate Governance", "Low", "/management-governance/modules/mod-governance-charter", false),
                Module("mod-ownership-responsibility", "Ownership & Responsibility Model Management", "ownership-responsibility-model-management", "RACI and accountability maps.", "Active", "Corporate Governance", "Medium", "/management-governance/modules/mod-ownership-responsibility", false)
            })
    };

    public static IReadOnlyDictionary<string, ModuleSummary> ModulesById =>
        Subdomains.SelectMany(x => x.Modules).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public static SubdomainSummary? FindSubdomainBySlug(string slug) =>
        Subdomains.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<SidebarNavChild> GetSidebarChildrenForSubdomain(string slug) =>
        FindSubdomainBySlug(slug)?.SidebarChildren ?? Array.Empty<SidebarNavChild>();

    private static SubdomainSummary BuildSubdomain(
        string id,
        string name,
        string slug,
        string description,
        string owner,
        string riskLevel,
        string status,
        IEnumerable<string> tags,
        IEnumerable<string> dependencies,
        IEnumerable<ModuleSummary> modules,
        string? routeOverride = null,
        IEnumerable<SidebarNavChild>? sidebarChildren = null)
    {
        var moduleList = modules.ToList();
        return new SubdomainSummary
        {
            Id = id,
            Name = name,
            Slug = slug,
            Description = description,
            Owner = owner,
            RiskLevel = riskLevel,
            Status = status,
            Route = routeOverride ?? $"/management-governance/subdomains/{slug}",
            Implemented = moduleList.Any(m => m.Implemented),
            PendingItemsCount = moduleList.Sum(m => m.PendingItemsCount),
            Tags = tags.ToList(),
            Dependencies = dependencies.ToList(),
            Modules = moduleList,
            SidebarChildren = sidebarChildren == null ? Array.Empty<SidebarNavChild>() : sidebarChildren.ToList(),
            Kpis = new List<KPIWidgetModel>
            {
                new() { Id = $"{id}-kpi-pending", Label = "Pending Items", Value = moduleList.Sum(m => m.PendingItemsCount).ToString(), Trend = "Across modules", TrendDirection = "Neutral" },
                new() { Id = $"{id}-kpi-risk", Label = "High Risk Modules", Value = moduleList.Count(m => string.Equals(m.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)).ToString(), Trend = "Needs review", TrendDirection = "Up" },
                new() { Id = $"{id}-kpi-implemented", Label = "Implemented Modules", Value = moduleList.Count(m => m.Implemented).ToString(), Trend = $"{moduleList.Count} total", TrendDirection = "Neutral" }
            }
        };
    }

    private static ModuleSummary Module(
        string id,
        string name,
        string slug,
        string description,
        string status,
        string owner,
        string riskLevel,
        string route,
        bool implemented)
    {
        return new ModuleSummary
        {
            Id = id,
            Name = name,
            Slug = slug,
            Description = description,
            Status = status,
            Owner = owner,
            RiskLevel = riskLevel,
            Route = route,
            Implemented = implemented,
            PendingItemsCount = Math.Abs(id.GetHashCode()) % 28 + 3,
            LinkedBlueprintModuleIds = new[] { $"bp-{slug}" },
            Tags = new[] { slug.Split('-').First(), "governance" },
            Dependencies = Array.Empty<string>(),
            KpiSummary = new[]
            {
                new KPIWidgetModel { Id = $"{id}-health", Label = "Health", Value = status, Trend = "Latest sync", TrendDirection = "Neutral" },
                new KPIWidgetModel { Id = $"{id}-pending", Label = "Pending", Value = (Math.Abs(id.GetHashCode()) % 28 + 3).ToString(), Trend = "Queue", TrendDirection = "Up" }
            }
        };
    }
}
