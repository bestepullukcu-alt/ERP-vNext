namespace Diten.Application.Dtos.EnterpriseStrategy;

/// <summary>
/// Server-driven governed values for ES&amp;BP workbook / modal selectors (replaces static JS-only lists).
/// </summary>
public sealed class EnterpriseStrategyWorkbookLookupsDto
{
    public IReadOnlyList<string> Owners { get; init; } = Array.Empty<string>();
    public IReadOnlyList<OwnerReferenceDto> OwnerReferences { get; init; } = Array.Empty<OwnerReferenceDto>();
    public IReadOnlyList<string> Priorities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ComplexityRiskScale { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LifecycleStatus { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ApprovalStatus { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GoalObjectiveTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InitiativeTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StrategicThemes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ContributionTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DependencyTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DirectionOfPerformance { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReportingFrequencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ThresholdModels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReviewCadences { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BusinessUnits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Regions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ApprovalGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ApprovalRouteTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PlanningCycles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PlanningCycleTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PlanningLifecycleStatuses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StrategyPeriodLifecycleStatuses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StrategyPeriodScenarioTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RiskIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FiscalPeriods { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DependencyObjectTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DependencyCriticalities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EntityScopes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnitOfMeasure { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GoalMetricType { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ObjectiveMetricType { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InitiativeMetricType { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectMetricType { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GoalAggregation { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConnectionAggregation { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ObjectiveTargetAggregation { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WaveValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MaturityValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectOwnerValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectSponsorValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectStageValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectDeliveryValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReadinessValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ScopeModeValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CurrencyCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BudgetTypeValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BudgetBasisValues { get; init; } = Array.Empty<string>();
    public string ProjectNumberingScheme { get; init; } = string.Empty;
    public IReadOnlyList<string> Positions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CompanyReferenceDto> Companies { get; init; } = Array.Empty<CompanyReferenceDto>();
}

public sealed class OwnerReferenceDto
{
    public string OwnerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class EnterpriseStrategyRuntimeIdPreviewDto
{
    public string GoalId { get; init; } = string.Empty;
    public string ObjectiveId { get; init; } = string.Empty;
    public string InitiativeId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
}
