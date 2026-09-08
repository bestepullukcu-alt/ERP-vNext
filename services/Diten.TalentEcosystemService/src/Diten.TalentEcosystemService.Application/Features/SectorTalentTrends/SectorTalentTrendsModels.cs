using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends;

public class SectorTalentTrendsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public SectorTalentTrendsReadinessState SectorTalentTrendsReadinessState { get; init; } = SectorTalentTrendsReadinessState.Draft;
    public SectorTalentTrendsReadinessState TrendCatalogBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState SignalBindingIntakeBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState AggregationScopeBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState VisibilityControlBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState TrendReviewBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState AutomatedDecisionBoundaryState { get; init; } = SectorTalentTrendsReadinessState.Blocked;
    public SectorTalentTrendsReadinessState TalentDataSourceDependencyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState WorkforceAnalyticsSourceDependencyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState DataGovernancePolicyDependencyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState NotificationDependencyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState ConsentPreconditionState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState DataMinimizationState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState RetentionPolicyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public SectorTalentTrendsReadinessState PublicationPolicyState { get; init; } = SectorTalentTrendsReadinessState.Deferred;
    public IReadOnlyDictionary<string, SectorTalentTrendsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, SectorTalentTrendsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long SectorTalentTrendsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record SectorTalentTrendsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    SectorTalentTrendsReadinessState SectorTalentTrendsReadinessState,
    SectorTalentTrendsReadinessState TrendCatalogBoundaryState,
    SectorTalentTrendsReadinessState SignalBindingIntakeBoundaryState,
    SectorTalentTrendsReadinessState AggregationScopeBoundaryState,
    SectorTalentTrendsReadinessState VisibilityControlBoundaryState,
    SectorTalentTrendsReadinessState TrendReviewBoundaryState,
    SectorTalentTrendsReadinessState AutomatedDecisionBoundaryState,
    SectorTalentTrendsReadinessState TalentDataSourceDependencyState,
    SectorTalentTrendsReadinessState WorkforceAnalyticsSourceDependencyState,
    SectorTalentTrendsReadinessState DataGovernancePolicyDependencyState,
    SectorTalentTrendsReadinessState NotificationDependencyState,
    SectorTalentTrendsReadinessState ConsentPreconditionState,
    SectorTalentTrendsReadinessState DataMinimizationState,
    SectorTalentTrendsReadinessState RetentionPolicyState,
    SectorTalentTrendsReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, SectorTalentTrendsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long SectorTalentTrendsReadinessVersion,
    string? DeferredReason);

public sealed record SectorTalentTrendsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    SectorTalentTrendsReadinessState SectorTalentTrendsReadinessState,
    SectorTalentTrendsReadinessState TrendCatalogBoundaryState,
    SectorTalentTrendsReadinessState TalentDataSourceDependencyState,
    SectorTalentTrendsReadinessState VisibilityControlBoundaryState,
    SectorTalentTrendsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record SectorTalentTrendsAuditMetadataDto(
    Guid Id,
    string Code,
    SectorTalentTrendsReadinessState RetentionPolicyState,
    SectorTalentTrendsReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class SectorTalentTrendsMapper
{
    public static SectorTalentTrendsReadinessDto ToDto(SectorTalentTrendsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SectorTalentTrendsReadinessState,
            entity.TrendCatalogBoundaryState,
            entity.SignalBindingIntakeBoundaryState,
            entity.AggregationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.TrendReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.SectorTalentTrendsReadinessVersion,
            entity.DeferredReason);

    public static SectorTalentTrendsReadinessListItemDto ToListItem(SectorTalentTrendsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SectorTalentTrendsReadinessState,
            entity.TrendCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static SectorTalentTrendsAuditMetadataDto ToAuditMetadata(SectorTalentTrendsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
