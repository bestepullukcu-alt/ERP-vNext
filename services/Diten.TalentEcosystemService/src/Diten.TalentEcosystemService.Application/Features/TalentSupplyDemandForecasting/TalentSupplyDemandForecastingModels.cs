using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting;

public class TalentSupplyDemandForecastingReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public TalentSupplyDemandForecastingReadinessState TalentSupplyDemandForecastingReadinessState { get; init; } = TalentSupplyDemandForecastingReadinessState.Draft;
    public TalentSupplyDemandForecastingReadinessState ForecastCatalogBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState ModelBindingIntakeBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState HorizonScopeBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState VisibilityControlBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState ForecastReviewBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState AutomatedDecisionBoundaryState { get; init; } = TalentSupplyDemandForecastingReadinessState.Blocked;
    public TalentSupplyDemandForecastingReadinessState TalentDataSourceDependencyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState WorkforceAnalyticsSourceDependencyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState SectorTrendSourceDependencyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState NotificationDependencyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState ConsentPreconditionState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState DataMinimizationState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState RetentionPolicyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public TalentSupplyDemandForecastingReadinessState PublicationPolicyState { get; init; } = TalentSupplyDemandForecastingReadinessState.Deferred;
    public IReadOnlyDictionary<string, TalentSupplyDemandForecastingReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, TalentSupplyDemandForecastingReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long TalentSupplyDemandForecastingReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record TalentSupplyDemandForecastingReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentSupplyDemandForecastingReadinessState TalentSupplyDemandForecastingReadinessState,
    TalentSupplyDemandForecastingReadinessState ForecastCatalogBoundaryState,
    TalentSupplyDemandForecastingReadinessState ModelBindingIntakeBoundaryState,
    TalentSupplyDemandForecastingReadinessState HorizonScopeBoundaryState,
    TalentSupplyDemandForecastingReadinessState VisibilityControlBoundaryState,
    TalentSupplyDemandForecastingReadinessState ForecastReviewBoundaryState,
    TalentSupplyDemandForecastingReadinessState AutomatedDecisionBoundaryState,
    TalentSupplyDemandForecastingReadinessState TalentDataSourceDependencyState,
    TalentSupplyDemandForecastingReadinessState WorkforceAnalyticsSourceDependencyState,
    TalentSupplyDemandForecastingReadinessState SectorTrendSourceDependencyState,
    TalentSupplyDemandForecastingReadinessState NotificationDependencyState,
    TalentSupplyDemandForecastingReadinessState ConsentPreconditionState,
    TalentSupplyDemandForecastingReadinessState DataMinimizationState,
    TalentSupplyDemandForecastingReadinessState RetentionPolicyState,
    TalentSupplyDemandForecastingReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, TalentSupplyDemandForecastingReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long TalentSupplyDemandForecastingReadinessVersion,
    string? DeferredReason);

public sealed record TalentSupplyDemandForecastingReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentSupplyDemandForecastingReadinessState TalentSupplyDemandForecastingReadinessState,
    TalentSupplyDemandForecastingReadinessState ForecastCatalogBoundaryState,
    TalentSupplyDemandForecastingReadinessState TalentDataSourceDependencyState,
    TalentSupplyDemandForecastingReadinessState VisibilityControlBoundaryState,
    TalentSupplyDemandForecastingReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record TalentSupplyDemandForecastingAuditMetadataDto(
    Guid Id,
    string Code,
    TalentSupplyDemandForecastingReadinessState RetentionPolicyState,
    TalentSupplyDemandForecastingReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class TalentSupplyDemandForecastingMapper
{
    public static TalentSupplyDemandForecastingReadinessDto ToDto(TalentSupplyDemandForecastingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentSupplyDemandForecastingReadinessState,
            entity.ForecastCatalogBoundaryState,
            entity.ModelBindingIntakeBoundaryState,
            entity.HorizonScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.ForecastReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.SectorTrendSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.TalentSupplyDemandForecastingReadinessVersion,
            entity.DeferredReason);

    public static TalentSupplyDemandForecastingReadinessListItemDto ToListItem(TalentSupplyDemandForecastingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentSupplyDemandForecastingReadinessState,
            entity.ForecastCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static TalentSupplyDemandForecastingAuditMetadataDto ToAuditMetadata(TalentSupplyDemandForecastingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
