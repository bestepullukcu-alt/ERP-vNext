using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class WorkforcePlanningReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WorkforcePlanningReadinessState WorkforcePlanningReadinessState { get; set; }
    public WorkforcePlanningReadinessState HeadcountPlanBoundaryState { get; set; }
    public WorkforcePlanningReadinessState DemandForecastBoundaryState { get; set; }
    public WorkforcePlanningReadinessState SupplyForecastBoundaryState { get; set; }
    public WorkforcePlanningReadinessState GapAnalysisBoundaryState { get; set; }
    public WorkforcePlanningReadinessState ScenarioModelingBoundaryState { get; set; }
    public WorkforcePlanningReadinessState AutomatedDecisionBoundaryState { get; set; }
    public WorkforcePlanningReadinessState OrganizationStructureDependencyState { get; set; }
    public WorkforcePlanningReadinessState PositionFrameworkDependencyState { get; set; }
    public WorkforcePlanningReadinessState DocumentDependencyState { get; set; }
    public WorkforcePlanningReadinessState NotificationDependencyState { get; set; }
    public WorkforcePlanningReadinessState ConsentPreconditionState { get; set; }
    public WorkforcePlanningReadinessState DataMinimizationState { get; set; }
    public WorkforcePlanningReadinessState RetentionPolicyState { get; set; }
    public WorkforcePlanningReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, WorkforcePlanningReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long WorkforcePlanningReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
