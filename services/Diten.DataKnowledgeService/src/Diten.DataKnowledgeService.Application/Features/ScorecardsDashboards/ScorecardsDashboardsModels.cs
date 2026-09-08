using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards;

public class ScorecardsDashboardsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ScorecardsDashboardsReadinessState ScorecardsDashboardsReadinessState { get; init; } = ScorecardsDashboardsReadinessState.Draft;
    public ScorecardsDashboardsReadinessState ScorecardCatalogBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState WidgetBindingIntakeBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState LayoutScopeBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState PublicationControlBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState DashboardReviewBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState AutomatedDecisionBoundaryState { get; init; } = ScorecardsDashboardsReadinessState.Blocked;
    public ScorecardsDashboardsReadinessState MetricSemanticRegistrySourceDependencyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState DataWarehouseSourceDependencyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState DataContractRegistryDependencyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState NotificationDependencyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState StewardshipPreconditionState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState DataMinimizationState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState RetentionPolicyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public ScorecardsDashboardsReadinessState PublicationPolicyState { get; init; } = ScorecardsDashboardsReadinessState.Deferred;
    public IReadOnlyDictionary<string, ScorecardsDashboardsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, ScorecardsDashboardsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long ScorecardsDashboardsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record ScorecardsDashboardsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    ScorecardsDashboardsReadinessState ScorecardsDashboardsReadinessState,
    ScorecardsDashboardsReadinessState ScorecardCatalogBoundaryState,
    ScorecardsDashboardsReadinessState WidgetBindingIntakeBoundaryState,
    ScorecardsDashboardsReadinessState LayoutScopeBoundaryState,
    ScorecardsDashboardsReadinessState PublicationControlBoundaryState,
    ScorecardsDashboardsReadinessState DashboardReviewBoundaryState,
    ScorecardsDashboardsReadinessState AutomatedDecisionBoundaryState,
    ScorecardsDashboardsReadinessState MetricSemanticRegistrySourceDependencyState,
    ScorecardsDashboardsReadinessState DataWarehouseSourceDependencyState,
    ScorecardsDashboardsReadinessState DataContractRegistryDependencyState,
    ScorecardsDashboardsReadinessState NotificationDependencyState,
    ScorecardsDashboardsReadinessState StewardshipPreconditionState,
    ScorecardsDashboardsReadinessState DataMinimizationState,
    ScorecardsDashboardsReadinessState RetentionPolicyState,
    ScorecardsDashboardsReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, ScorecardsDashboardsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long ScorecardsDashboardsReadinessVersion,
    string? DeferredReason);

public sealed record ScorecardsDashboardsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    ScorecardsDashboardsReadinessState ScorecardsDashboardsReadinessState,
    ScorecardsDashboardsReadinessState ScorecardCatalogBoundaryState,
    ScorecardsDashboardsReadinessState MetricSemanticRegistrySourceDependencyState,
    ScorecardsDashboardsReadinessState PublicationControlBoundaryState,
    ScorecardsDashboardsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record ScorecardsDashboardsAuditMetadataDto(
    Guid Id,
    string Code,
    ScorecardsDashboardsReadinessState RetentionPolicyState,
    ScorecardsDashboardsReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class ScorecardsDashboardsMapper
{
    public static ScorecardsDashboardsReadinessDto ToDto(ScorecardsDashboardsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ScorecardsDashboardsReadinessState,
            entity.ScorecardCatalogBoundaryState,
            entity.WidgetBindingIntakeBoundaryState,
            entity.LayoutScopeBoundaryState,
            entity.PublicationControlBoundaryState,
            entity.DashboardReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.DataWarehouseSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ScorecardsDashboardsReadinessVersion,
            entity.DeferredReason);

    public static ScorecardsDashboardsReadinessListItemDto ToListItem(ScorecardsDashboardsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ScorecardsDashboardsReadinessState,
            entity.ScorecardCatalogBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.PublicationControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static ScorecardsDashboardsAuditMetadataDto ToAuditMetadata(ScorecardsDashboardsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
