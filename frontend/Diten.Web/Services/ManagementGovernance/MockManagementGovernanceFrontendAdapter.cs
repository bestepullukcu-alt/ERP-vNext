using Diten.Web.Config;
using Diten.Web.Models.ManagementGovernance;

namespace Diten.Web.Services.ManagementGovernance;

public sealed class MockManagementGovernanceFrontendAdapter : IManagementGovernanceFrontendAdapter
{
    public DomainSummary UseManagementGovernanceSummary()
    {
        var subdomains = ManagementGovernanceRegistry.Subdomains;
        var queue = UseGovernanceWorkQueue();
        var cycles = UseGovernanceReviewCycles();
        var risks = UseRiskSignals();
        var activities = UseRecentGovernanceActivity();

        return new DomainSummary
        {
            Description = "Cross-module orchestration shell for decisions, governance cadence, risk posture, and drill-through navigation.",
            Kpis = new[]
            {
                new KPIWidgetModel { Id = "kpi-open-approvals", Label = "Open Approvals", Value = queue.Count(x => x.Type == "Approval").ToString(), Trend = "Needs decision", TrendDirection = "Up" },
                new KPIWidgetModel { Id = "kpi-overdue-actions", Label = "Overdue Actions", Value = queue.Count(x => x.DueAtUtc < DateTime.UtcNow && x.Status != "Closed").ToString(), Trend = "Aging", TrendDirection = "Up" },
                new KPIWidgetModel { Id = "kpi-upcoming-reviews", Label = "Upcoming Reviews", Value = cycles.Count(x => x.DueAtUtc <= DateTime.UtcNow.AddDays(14)).ToString(), Trend = "Next 14 days", TrendDirection = "Neutral" },
                new KPIWidgetModel { Id = "kpi-blocked-dependencies", Label = "Blocked Dependencies", Value = risks.Count(x => x.Severity == "High").ToString(), Trend = "Cross-subdomain", TrendDirection = "Up" }
            },
            RiskSignals = risks.Take(6).ToList(),
            QueueHighlights = queue.Take(8).ToList(),
            UpcomingCadence = cycles.OrderBy(x => x.DueAtUtc).Take(6).ToList(),
            RecentActivity = activities.Take(8).ToList(),
            SavedViews = new[]
            {
                new SavedView { Id = "sv-critical-risk", Name = "Critical Risk Focus", Description = "High risk and overdue actions", Route = "/management-governance?view=critical", Filters = new Dictionary<string, string> { ["risk"] = "High", ["status"] = "Open" } },
                new SavedView { Id = "sv-weekly-cadence", Name = "Weekly Cadence", Description = "This week's governance forums", Route = "/management-governance/cadence?view=weekly", Filters = new Dictionary<string, string> { ["window"] = "7d" } }
            },
            FavoriteModuleIds = new[] { "mod-demand-intake", "mod-decision-log-action", "mod-risk-management" }
        };
    }

    public SubdomainSummary? UseSubdomainSummary(string slug) =>
        ManagementGovernanceRegistry.Subdomains.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<WorkQueueItem> UseGovernanceWorkQueue()
    {
        var modules = ManagementGovernanceRegistry.ModulesById;
        var items = new List<WorkQueueItem>();
        var now = DateTime.UtcNow;
        var queueTypes = new[] { "Approval", "Review", "Escalation", "Evidence", "Remediation", "DecisionClosure" };
        var priorities = new[] { "Low", "Medium", "High", "Critical" };

        foreach (var subdomain in ManagementGovernanceRegistry.Subdomains)
        {
            foreach (var module in subdomain.Modules.Take(2))
            {
                for (var i = 0; i < 2; i++)
                {
                    var idx = items.Count + 1;
                    items.Add(new WorkQueueItem
                    {
                        Id = $"wq-{idx:000}",
                        Title = $"{queueTypes[idx % queueTypes.Length]} for {module.Name}",
                        Type = queueTypes[idx % queueTypes.Length],
                        SubdomainSlug = subdomain.Slug,
                        SourceModuleId = module.Id,
                        SourceModuleRoute = module.Route,
                        Priority = priorities[idx % priorities.Length],
                        RiskLevel = idx % 3 == 0 ? "High" : idx % 2 == 0 ? "Medium" : "Low",
                        Status = idx % 5 == 0 ? "In Progress" : "Open",
                        Assignee = idx % 4 == 0 ? "Unassigned" : $"Owner {idx % 7 + 1}",
                        DueAtUtc = now.AddDays((idx % 9) - 4),
                        IsEscalated = idx % 7 == 0,
                        RequiresEvidence = idx % 2 == 0,
                        HasAuditTrail = true
                    });
                }
            }
        }

        return items.OrderBy(x => x.DueAtUtc).ToList();
    }

    public IReadOnlyList<GovernanceEvent> UseGovernanceCadence()
    {
        var now = DateTime.UtcNow.Date;
        return new List<GovernanceEvent>
        {
            Event("ev-001", "Executive strategy checkpoint", "StrategicReview", now.AddDays(2).AddHours(9), now.AddDays(2).AddHours(10), "Executive Forum", "Scheduled", "/management-governance/executive-cockpit"),
            Event("ev-002", "Stage gate forum", "StageGate", now.AddDays(4).AddHours(11), now.AddDays(4).AddHours(12), "Portfolio Council", "Scheduled", "/management-governance/cadence"),
            Event("ev-003", "Transformation readiness review", "ReadinessReview", now.AddDays(6).AddHours(14), now.AddDays(6).AddHours(15), "Transformation Board", "Scheduled", "/management-governance/subdomains/change-transformation-management"),
            Event("ev-004", "Risk and control committee", "RiskReview", now.AddDays(8).AddHours(10), now.AddDays(8).AddHours(11), "Risk Committee", "Scheduled", "/management-governance/subdomains/governance-risk-control")
        };
    }

    public IReadOnlyList<ReviewCycleItem> UseGovernanceReviewCycles()
    {
        var now = DateTime.UtcNow;
        return new[]
        {
            new ReviewCycleItem { Id = "rc-001", Name = "Weekly governance sync", CycleType = "Weekly", SubdomainSlug = "decisioning-reviews-management-cadence", DueAtUtc = now.AddDays(3), Status = "Planned", AgendaItems = 12 },
            new ReviewCycleItem { Id = "rc-002", Name = "Monthly portfolio review", CycleType = "Monthly", SubdomainSlug = "portfolio-investment-value-management", DueAtUtc = now.AddDays(9), Status = "Planned", AgendaItems = 16 },
            new ReviewCycleItem { Id = "rc-003", Name = "Quarterly strategy review", CycleType = "Quarterly", SubdomainSlug = "enterprise-strategy-business-performance", DueAtUtc = now.AddDays(21), Status = "Planned", AgendaItems = 8 },
            new ReviewCycleItem { Id = "rc-004", Name = "Risk oversight review", CycleType = "Monthly", SubdomainSlug = "governance-risk-control", DueAtUtc = now.AddDays(11), Status = "Planned", AgendaItems = 10 }
        };
    }

    public IReadOnlyList<RecentActivityItem> UseRecentGovernanceActivity()
    {
        var now = DateTime.UtcNow;
        return new[]
        {
            Activity("ra-001", "Stage gate approved", "PMO Reviewer", "Decision", now.AddHours(-2), "/management-governance/work-queue"),
            Activity("ra-002", "Evidence uploaded for risk item", "Control Analyst", "Evidence", now.AddHours(-5), "/management-governance/subdomains/governance-risk-control"),
            Activity("ra-003", "Strategic KPI variance flagged", "Strategy Analyst", "Alert", now.AddHours(-8), "/management-governance/executive-cockpit"),
            Activity("ra-004", "Action reassigned", "Governance Secretary", "Assignment", now.AddHours(-12), "/management-governance/work-queue"),
            Activity("ra-005", "New review pack published", "Portfolio Ops", "Cadence", now.AddHours(-18), "/management-governance/cadence"),
            Activity("ra-006", "Dependency blocker raised", "Delivery Lead", "Dependency", now.AddHours(-23), "/management-governance/delivery-execution/delivery-map")
        };
    }

    public IReadOnlyList<RiskSignal> UseRiskSignals()
    {
        return new[]
        {
            Risk("rs-001", "Blocked dependency impacts stage gate", "High", "delivery-execution-management", "mod-milestone-dependency", "Cross-program dependency remains unresolved before gate.", "/management-governance/delivery-execution/delivery-map"),
            Risk("rs-002", "Evidence gap in compliance package", "High", "governance-risk-control", "mod-compliance-oversight", "Critical evidence set missing for upcoming committee review.", "/management-governance/subdomains/governance-risk-control"),
            Risk("rs-003", "Transformation adoption lag", "Medium", "change-transformation-management", "mod-stakeholder-adoption", "Adoption metrics below plan in two business units.", "/management-governance/executive-cockpit"),
            Risk("rs-004", "Portfolio variance above threshold", "Medium", "portfolio-investment-value-management", "mod-portfolio-management", "Investment variance exceeds tolerance for current cycle.", "/management-governance/executive-cockpit"),
            Risk("rs-005", "Decision backlog aging", "Medium", "decisioning-reviews-management-cadence", "mod-decision-log-action", "Decision closure SLA exceeded for multiple actions.", "/management-governance/work-queue")
        };
    }

    private static GovernanceEvent Event(string id, string title, string type, DateTime start, DateTime end, string forum, string status, string route) =>
        new()
        {
            Id = id,
            Title = title,
            EventType = type,
            StartUtc = start,
            EndUtc = end,
            Forum = forum,
            Status = status,
            Owner = "Governance Secretariat",
            Route = route
        };

    private static RecentActivityItem Activity(string id, string title, string actor, string actionType, DateTime atUtc, string route) =>
        new()
        {
            Id = id,
            Title = title,
            Actor = actor,
            ActionType = actionType,
            AtUtc = atUtc,
            Route = route,
            HasAuditMarker = true,
            HasEvidenceLink = actionType is "Evidence" or "Decision"
        };

    private static RiskSignal Risk(string id, string title, string severity, string subdomainSlug, string moduleId, string description, string route) =>
        new()
        {
            Id = id,
            Title = title,
            Severity = severity,
            SubdomainSlug = subdomainSlug,
            ModuleId = moduleId,
            Description = description,
            Route = route
        };
}
