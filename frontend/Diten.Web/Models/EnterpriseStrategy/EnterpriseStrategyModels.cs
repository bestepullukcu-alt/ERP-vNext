namespace Diten.Web.Models.EnterpriseStrategy;

public sealed class StrategyTab
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}

public sealed class GoalMetric
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal TargetValue { get; init; }
}

public sealed class Goal
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Statement { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Status { get; init; } = "Draft";
    public string Priority { get; init; } = "Medium";
    public string EntityScope { get; init; } = string.Empty;
    public string ScopeMode { get; init; } = "Enterprise";
    public string? PrimaryCompanyId { get; init; }
    public IReadOnlyList<string> ApplicableCompanyIds { get; init; } = Array.Empty<string>();
    public DateTime? PlanningHorizonStart { get; init; }
    public DateTime? PlanningHorizonEnd { get; init; }
    public int Version { get; init; }
    public string? DecisionReference { get; init; }
    public string? EvidenceReference { get; init; }
    public IReadOnlyList<GoalMetric> Metrics { get; init; } = Array.Empty<GoalMetric>();
}

public sealed class ObjectiveMetric
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal TargetValue { get; init; }
}

public sealed class Objective
{
    public string Id { get; init; } = string.Empty;
    public string ParentGoalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Statement { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Status { get; init; } = "Draft";
    public string Type { get; init; } = string.Empty;
    public string Priority { get; init; } = "Medium";
    public string ContributionType { get; init; } = "Supports";
    public decimal ContributionWeight { get; init; }
    public string EntityScope { get; init; } = string.Empty;
    public bool InheritCompanyScope { get; init; } = true;
    public string? PrimaryCompanyId { get; init; }
    public IReadOnlyList<string> ApplicableCompanyIds { get; init; } = Array.Empty<string>();
    public int Version { get; init; }
    public string? DecisionReference { get; init; }
    public string? EvidenceReference { get; init; }
    public IReadOnlyList<ObjectiveMetric> Metrics { get; init; } = Array.Empty<ObjectiveMetric>();
}

public sealed class StrategyConnection
{
    public string Id { get; init; } = string.Empty;
    public string FromType { get; init; } = string.Empty;
    public string FromId { get; init; } = string.Empty;
    public string ToType { get; init; } = string.Empty;
    public string ToId { get; init; } = string.Empty;
    public string RelationshipType { get; init; } = "Supports";
    public string ContributionType { get; init; } = "Supports";
    public decimal ContributionWeight { get; init; }
    public string Status { get; init; } = "Draft";
    public int Version { get; init; }
    public string MetricBindingsJson { get; init; } = "[]";
    public string CompanyScopeMode { get; init; } = "Derived";
    public string? CompanyId { get; init; }
    public string DecisionReferencesJson { get; init; } = "[]";
    public string EvidenceReferencesJson { get; init; } = "[]";
}

public sealed class InitiativeStrategyLinkView
{
    public string LinkId { get; init; } = string.Empty;
    public string InitiativeId { get; init; } = string.Empty;
    public string InitiativeName { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = "PPM";
    public string SourceRecordId { get; init; } = string.Empty;
    public string LinkStatus { get; init; } = "Linked";
    public string TraceabilityStatus { get; init; } = "Under Review";
}

public sealed class ProjectStrategyLinkView
{
    public string LinkId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = "PPM";
    public string SourceRecordId { get; init; } = string.Empty;
    public string LinkStatus { get; init; } = "Linked";
    public string TraceabilityStatus { get; init; } = "Under Review";
}

public sealed class StrategyMetricSummaryCard
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Trend { get; init; } = string.Empty;
}

public sealed class EnterpriseStrategyPageViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string ActiveTab { get; init; } = "overview";
    public IReadOnlyList<string> Breadcrumbs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<StrategyTab> Tabs { get; init; } = Array.Empty<StrategyTab>();
    public IReadOnlyList<Goal> Goals { get; init; } = Array.Empty<Goal>();
    public IReadOnlyList<Objective> Objectives { get; init; } = Array.Empty<Objective>();
    public IReadOnlyList<StrategyConnection> Connections { get; init; } = Array.Empty<StrategyConnection>();
    public IReadOnlyList<InitiativeStrategyLinkView> InitiativeLinks { get; init; } = Array.Empty<InitiativeStrategyLinkView>();
    public IReadOnlyList<ProjectStrategyLinkView> ProjectLinks { get; init; } = Array.Empty<ProjectStrategyLinkView>();
    public IReadOnlyList<StrategyMetricSummaryCard> MetricCards { get; init; } = Array.Empty<StrategyMetricSummaryCard>();
    public bool IsLoading { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
    public bool AccessDenied { get; init; }
}
