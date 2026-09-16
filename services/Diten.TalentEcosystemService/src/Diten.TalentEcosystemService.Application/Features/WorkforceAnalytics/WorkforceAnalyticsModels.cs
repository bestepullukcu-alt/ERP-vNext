using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics;

public class WorkforceAnalyticsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public WorkforceAnalyticsReadinessState WorkforceAnalyticsReadinessState { get; init; } = WorkforceAnalyticsReadinessState.Draft;
    public WorkforceAnalyticsReadinessState AnalyticsCatalogBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState MetricBindingIntakeBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState AggregationScopeBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState VisibilityControlBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState AnalyticsReviewBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState AutomatedDecisionBoundaryState { get; init; } = WorkforceAnalyticsReadinessState.Blocked;
    public WorkforceAnalyticsReadinessState TalentDataSourceDependencyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState ConsentPolicyDependencyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState DataGovernancePolicyDependencyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState NotificationDependencyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState ConsentPreconditionState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState DataMinimizationState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState RetentionPolicyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public WorkforceAnalyticsReadinessState PublicationPolicyState { get; init; } = WorkforceAnalyticsReadinessState.Deferred;
    public IReadOnlyDictionary<string, WorkforceAnalyticsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, WorkforceAnalyticsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long WorkforceAnalyticsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record WorkforceAnalyticsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    WorkforceAnalyticsReadinessState WorkforceAnalyticsReadinessState,
    WorkforceAnalyticsReadinessState AnalyticsCatalogBoundaryState,
    WorkforceAnalyticsReadinessState MetricBindingIntakeBoundaryState,
    WorkforceAnalyticsReadinessState AggregationScopeBoundaryState,
    WorkforceAnalyticsReadinessState VisibilityControlBoundaryState,
    WorkforceAnalyticsReadinessState AnalyticsReviewBoundaryState,
    WorkforceAnalyticsReadinessState AutomatedDecisionBoundaryState,
    WorkforceAnalyticsReadinessState TalentDataSourceDependencyState,
    WorkforceAnalyticsReadinessState ConsentPolicyDependencyState,
    WorkforceAnalyticsReadinessState DataGovernancePolicyDependencyState,
    WorkforceAnalyticsReadinessState NotificationDependencyState,
    WorkforceAnalyticsReadinessState ConsentPreconditionState,
    WorkforceAnalyticsReadinessState DataMinimizationState,
    WorkforceAnalyticsReadinessState RetentionPolicyState,
    WorkforceAnalyticsReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, WorkforceAnalyticsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long WorkforceAnalyticsReadinessVersion,
    string? DeferredReason);

public sealed record WorkforceAnalyticsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    WorkforceAnalyticsReadinessState WorkforceAnalyticsReadinessState,
    WorkforceAnalyticsReadinessState AnalyticsCatalogBoundaryState,
    WorkforceAnalyticsReadinessState TalentDataSourceDependencyState,
    WorkforceAnalyticsReadinessState VisibilityControlBoundaryState,
    WorkforceAnalyticsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record WorkforceAnalyticsAuditMetadataDto(
    Guid Id,
    string Code,
    WorkforceAnalyticsReadinessState RetentionPolicyState,
    WorkforceAnalyticsReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class WorkforceAnalyticsMapper
{
    public static WorkforceAnalyticsReadinessDto ToDto(WorkforceAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.WorkforceAnalyticsReadinessState,
            entity.AnalyticsCatalogBoundaryState,
            entity.MetricBindingIntakeBoundaryState,
            entity.AggregationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.AnalyticsReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.WorkforceAnalyticsReadinessVersion,
            entity.DeferredReason);

    public static WorkforceAnalyticsReadinessListItemDto ToListItem(WorkforceAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.WorkforceAnalyticsReadinessState,
            entity.AnalyticsCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static WorkforceAnalyticsAuditMetadataDto ToAuditMetadata(WorkforceAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
