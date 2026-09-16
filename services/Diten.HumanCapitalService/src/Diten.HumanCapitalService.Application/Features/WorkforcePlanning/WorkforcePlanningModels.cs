using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning;

public class WorkforcePlanningReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public WorkforcePlanningReadinessState WorkforcePlanningReadinessState { get; init; } = WorkforcePlanningReadinessState.Draft;
    public WorkforcePlanningReadinessState HeadcountPlanBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState DemandForecastBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState SupplyForecastBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState GapAnalysisBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState ScenarioModelingBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState AutomatedDecisionBoundaryState { get; init; } = WorkforcePlanningReadinessState.Blocked;
    public WorkforcePlanningReadinessState OrganizationStructureDependencyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState PositionFrameworkDependencyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState DocumentDependencyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState NotificationDependencyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState ConsentPreconditionState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState DataMinimizationState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState RetentionPolicyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public WorkforcePlanningReadinessState EvidencePolicyState { get; init; } = WorkforcePlanningReadinessState.Deferred;
    public IReadOnlyDictionary<string, WorkforcePlanningReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, WorkforcePlanningReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long WorkforcePlanningReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record WorkforcePlanningReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    WorkforcePlanningReadinessState WorkforcePlanningReadinessState,
    WorkforcePlanningReadinessState HeadcountPlanBoundaryState,
    WorkforcePlanningReadinessState DemandForecastBoundaryState,
    WorkforcePlanningReadinessState SupplyForecastBoundaryState,
    WorkforcePlanningReadinessState GapAnalysisBoundaryState,
    WorkforcePlanningReadinessState ScenarioModelingBoundaryState,
    WorkforcePlanningReadinessState AutomatedDecisionBoundaryState,
    WorkforcePlanningReadinessState OrganizationStructureDependencyState,
    WorkforcePlanningReadinessState PositionFrameworkDependencyState,
    WorkforcePlanningReadinessState DocumentDependencyState,
    WorkforcePlanningReadinessState NotificationDependencyState,
    WorkforcePlanningReadinessState ConsentPreconditionState,
    WorkforcePlanningReadinessState DataMinimizationState,
    WorkforcePlanningReadinessState RetentionPolicyState,
    WorkforcePlanningReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, WorkforcePlanningReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long WorkforcePlanningReadinessVersion,
    string? DeferredReason);

public sealed record WorkforcePlanningReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    WorkforcePlanningReadinessState WorkforcePlanningReadinessState,
    WorkforcePlanningReadinessState HeadcountPlanBoundaryState,
    WorkforcePlanningReadinessState OrganizationStructureDependencyState,
    WorkforcePlanningReadinessState GapAnalysisBoundaryState,
    WorkforcePlanningReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record WorkforcePlanningAuditMetadataDto(
    Guid Id,
    string Code,
    WorkforcePlanningReadinessState RetentionPolicyState,
    WorkforcePlanningReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class WorkforcePlanningMapper
{
    public static WorkforcePlanningReadinessDto ToDto(WorkforcePlanningReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.WorkforcePlanningReadinessState,
            entity.HeadcountPlanBoundaryState,
            entity.DemandForecastBoundaryState,
            entity.SupplyForecastBoundaryState,
            entity.GapAnalysisBoundaryState,
            entity.ScenarioModelingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.OrganizationStructureDependencyState,
            entity.PositionFrameworkDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.WorkforcePlanningReadinessVersion,
            entity.DeferredReason);

    public static WorkforcePlanningReadinessListItemDto ToListItem(WorkforcePlanningReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.WorkforcePlanningReadinessState,
            entity.HeadcountPlanBoundaryState,
            entity.OrganizationStructureDependencyState,
            entity.GapAnalysisBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static WorkforcePlanningAuditMetadataDto ToAuditMetadata(WorkforcePlanningReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
