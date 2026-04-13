namespace Diten.WebUI.Models.ManagementGovernance;

public sealed class DomainSummary
{
    public string DomainId { get; init; } = "management-governance";
    public string DomainName { get; init; } = "Management & Governance";
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<KPIWidgetModel> Kpis { get; init; } = Array.Empty<KPIWidgetModel>();
    public IReadOnlyList<RiskSignal> RiskSignals { get; init; } = Array.Empty<RiskSignal>();
    public IReadOnlyList<WorkQueueItem> QueueHighlights { get; init; } = Array.Empty<WorkQueueItem>();
    public IReadOnlyList<ReviewCycleItem> UpcomingCadence { get; init; } = Array.Empty<ReviewCycleItem>();
    public IReadOnlyList<RecentActivityItem> RecentActivity { get; init; } = Array.Empty<RecentActivityItem>();
    public IReadOnlyList<SavedView> SavedViews { get; init; } = Array.Empty<SavedView>();
    public IReadOnlyList<string> FavoriteModuleIds { get; init; } = Array.Empty<string>();
}

public sealed class SubdomainSummary
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "Active";
    public string Owner { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = "Medium";
    public int PendingItemsCount { get; init; }
    public string Route { get; init; } = string.Empty;
    public bool Implemented { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<KPIWidgetModel> Kpis { get; init; } = Array.Empty<KPIWidgetModel>();
    public IReadOnlyList<ModuleSummary> Modules { get; init; } = Array.Empty<ModuleSummary>();
    public IReadOnlyList<SidebarNavChild> SidebarChildren { get; init; } = Array.Empty<SidebarNavChild>();
}

public sealed class ModuleSummary
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "Planned";
    public string Owner { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = "Medium";
    public string Route { get; init; } = string.Empty;
    public int PendingItemsCount { get; init; }
    public bool Implemented { get; init; }
    public IReadOnlyList<string> LinkedBlueprintModuleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<KPIWidgetModel> KpiSummary { get; init; } = Array.Empty<KPIWidgetModel>();
}

public sealed class WorkQueueItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string SubdomainSlug { get; init; } = string.Empty;
    public string SourceModuleId { get; init; } = string.Empty;
    public string SourceModuleRoute { get; init; } = string.Empty;
    public string Priority { get; init; } = "Medium";
    public string RiskLevel { get; init; } = "Medium";
    public string Status { get; init; } = "Open";
    public string Assignee { get; init; } = "Unassigned";
    public DateTime DueAtUtc { get; init; }
    public bool IsEscalated { get; init; }
    public bool RequiresEvidence { get; init; }
    public bool HasAuditTrail { get; init; }
}

public sealed class GovernanceEvent
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string Forum { get; init; } = string.Empty;
    public string Status { get; init; } = "Scheduled";
    public string Owner { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

public sealed class ReviewCycleItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CycleType { get; init; } = string.Empty;
    public string SubdomainSlug { get; init; } = string.Empty;
    public DateTime DueAtUtc { get; init; }
    public string Status { get; init; } = "Planned";
    public int AgendaItems { get; init; }
}

public sealed class RiskSignal
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = "Medium";
    public string SubdomainSlug { get; init; } = string.Empty;
    public string ModuleId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

public sealed class KPIWidgetModel
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Trend { get; init; } = string.Empty;
    public string TrendDirection { get; init; } = "Neutral";
}

public sealed class SavedView
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Filters { get; init; } = new Dictionary<string, string>();
}

public sealed class RecentActivityItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public DateTime AtUtc { get; init; }
    public string Route { get; init; } = string.Empty;
    public bool HasAuditMarker { get; init; }
    public bool HasEvidenceLink { get; init; }
}

public sealed class ManagementGovernancePermissions
{
    public bool CanApprove { get; init; }
    public bool CanAssign { get; init; }
    public bool CanEscalate { get; init; }
    public bool CanViewEvidence { get; init; } = true;
    public bool CanViewAuditTrail { get; init; } = true;
}

public sealed class DomainShellTab
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

public sealed class SidebarNavChild
{
    public string Label { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

public sealed class ManagementGovernancePageViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string PageSubtitle { get; init; } = string.Empty;
    public string ActiveNav { get; init; } = "overview";
    public DomainSummary Domain { get; init; } = new();
    public IReadOnlyList<SubdomainSummary> Subdomains { get; init; } = Array.Empty<SubdomainSummary>();
    public SubdomainSummary? CurrentSubdomain { get; init; }
    public IReadOnlyList<WorkQueueItem> WorkQueue { get; init; } = Array.Empty<WorkQueueItem>();
    public IReadOnlyList<GovernanceEvent> GovernanceEvents { get; init; } = Array.Empty<GovernanceEvent>();
    public IReadOnlyList<ReviewCycleItem> ReviewCycles { get; init; } = Array.Empty<ReviewCycleItem>();
    public IReadOnlyList<RiskSignal> RiskSignals { get; init; } = Array.Empty<RiskSignal>();
    public IReadOnlyList<RecentActivityItem> RecentActivity { get; init; } = Array.Empty<RecentActivityItem>();
    public IReadOnlyList<string> Breadcrumbs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DomainShellTab> Tabs { get; init; } = Array.Empty<DomainShellTab>();
    public IReadOnlyDictionary<string, string> AppliedFilters { get; init; } = new Dictionary<string, string>();
    public ManagementGovernancePermissions Permissions { get; init; } = new();
}
