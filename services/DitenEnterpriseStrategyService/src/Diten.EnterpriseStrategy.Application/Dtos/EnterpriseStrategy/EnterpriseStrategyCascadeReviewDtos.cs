namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class CascadeObjectiveRowDto
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string ObjectiveName { get; set; } = string.Empty;
    public decimal ContributionWeight { get; set; }
    public decimal AllocatedTarget { get; set; }
    public string CoverageStatus { get; set; } = "Complete";
    public string? CompanyId { get; set; }
    public string Warning { get; set; } = string.Empty;
}

public sealed class CascadeBuilderSnapshotDto
{
    public string GoalId { get; set; } = string.Empty;
    public string GoalName { get; set; } = string.Empty;
    public string GoalMetric { get; set; } = string.Empty;
    public decimal ParentTarget { get; set; }
    public string? CompanyId { get; set; }
    public IReadOnlyList<CascadeObjectiveRowDto> Objectives { get; set; } = Array.Empty<CascadeObjectiveRowDto>();
}

public sealed class TargetAllocationRowDto
{
    public string LevelType { get; set; } = "Objective";
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public decimal ManualTarget { get; set; }
    public decimal GeneratedTarget { get; set; }
    public decimal FinalTarget { get; set; }
}

public sealed class TargetAllocationSnapshotDto
{
    public string ParentGoalId { get; set; } = string.Empty;
    public string ParentGoalName { get; set; } = string.Empty;
    public string GoalMetric { get; set; } = string.Empty;
    public decimal ParentTarget { get; set; }
    public IReadOnlyList<TargetAllocationRowDto> Allocations { get; set; } = Array.Empty<TargetAllocationRowDto>();
}

public sealed class ConsolidationRowDto
{
    public string GoalId { get; set; } = string.Empty;
    public string GoalName { get; set; } = string.Empty;
    public string ObjectiveId { get; set; } = string.Empty;
    public string ObjectiveName { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public decimal ContributionTotal { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal Variance { get; set; }
}

public sealed class VarianceAnalysisRowDto
{
    public string GoalId { get; set; } = string.Empty;
    public string ObjectiveId { get; set; } = string.Empty;
    public string KpiId { get; set; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public string TimePeriod { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal VariancePercent { get; set; }
    public string Trend { get; set; } = "Stable";
    public string Status { get; set; } = "On Track";
    public string AlignmentRowId { get; set; } = string.Empty;
}

public sealed class StrategicReviewEventDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string ReviewType { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string ObjectiveId { get; set; } = string.Empty;
    public string ScorecardScope { get; set; } = string.Empty;
    public string Facilitator { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
}

public sealed class ReviewPackDto
{
    public string ReviewId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> GoalIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ObjectiveIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> KpiIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CascadeHighlights { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> VarianceHighlights { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DecisionsRequired { get; set; } = Array.Empty<string>();
}

public sealed class ReviewDecisionActionDto
{
    public string Id { get; set; } = string.Empty;
    public string ReviewId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RelatedGoalId { get; set; } = string.Empty;
    public string RelatedObjectiveId { get; set; } = string.Empty;
    public string RelatedKpiId { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public string Evidence { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
}

public sealed class ReviewHistoryRowDto
{
    public string ReviewId { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string ReviewType { get; set; } = string.Empty;
    public int DecisionsCount { get; set; }
    public int OpenActions { get; set; }
    public int ClosedActions { get; set; }
    public string ScorecardSnapshotRef { get; set; } = string.Empty;
    public string CascadeSnapshotRef { get; set; } = string.Empty;
}

