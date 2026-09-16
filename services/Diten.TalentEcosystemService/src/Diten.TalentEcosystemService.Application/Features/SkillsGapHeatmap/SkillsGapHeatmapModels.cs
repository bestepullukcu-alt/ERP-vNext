using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap;

public class SkillsGapHeatmapReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public SkillsGapHeatmapReadinessState SkillsGapHeatmapReadinessState { get; init; } = SkillsGapHeatmapReadinessState.Draft;
    public SkillsGapHeatmapReadinessState GapCatalogBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState HeatmapBindingIntakeBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState SeverityScopeBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState VisibilityControlBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState GapReviewBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState AutomatedDecisionBoundaryState { get; init; } = SkillsGapHeatmapReadinessState.Blocked;
    public SkillsGapHeatmapReadinessState SkillsTaxonomySourceDependencyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState WorkforceAnalyticsSourceDependencyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState TalentDemandForecastSourceDependencyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState NotificationDependencyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState ConsentPreconditionState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState DataMinimizationState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState RetentionPolicyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public SkillsGapHeatmapReadinessState PublicationPolicyState { get; init; } = SkillsGapHeatmapReadinessState.Deferred;
    public IReadOnlyDictionary<string, SkillsGapHeatmapReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, SkillsGapHeatmapReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long SkillsGapHeatmapReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record SkillsGapHeatmapReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    SkillsGapHeatmapReadinessState SkillsGapHeatmapReadinessState,
    SkillsGapHeatmapReadinessState GapCatalogBoundaryState,
    SkillsGapHeatmapReadinessState HeatmapBindingIntakeBoundaryState,
    SkillsGapHeatmapReadinessState SeverityScopeBoundaryState,
    SkillsGapHeatmapReadinessState VisibilityControlBoundaryState,
    SkillsGapHeatmapReadinessState GapReviewBoundaryState,
    SkillsGapHeatmapReadinessState AutomatedDecisionBoundaryState,
    SkillsGapHeatmapReadinessState SkillsTaxonomySourceDependencyState,
    SkillsGapHeatmapReadinessState WorkforceAnalyticsSourceDependencyState,
    SkillsGapHeatmapReadinessState TalentDemandForecastSourceDependencyState,
    SkillsGapHeatmapReadinessState NotificationDependencyState,
    SkillsGapHeatmapReadinessState ConsentPreconditionState,
    SkillsGapHeatmapReadinessState DataMinimizationState,
    SkillsGapHeatmapReadinessState RetentionPolicyState,
    SkillsGapHeatmapReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, SkillsGapHeatmapReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long SkillsGapHeatmapReadinessVersion,
    string? DeferredReason);

public sealed record SkillsGapHeatmapReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    SkillsGapHeatmapReadinessState SkillsGapHeatmapReadinessState,
    SkillsGapHeatmapReadinessState GapCatalogBoundaryState,
    SkillsGapHeatmapReadinessState SkillsTaxonomySourceDependencyState,
    SkillsGapHeatmapReadinessState VisibilityControlBoundaryState,
    SkillsGapHeatmapReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record SkillsGapHeatmapAuditMetadataDto(
    Guid Id,
    string Code,
    SkillsGapHeatmapReadinessState RetentionPolicyState,
    SkillsGapHeatmapReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class SkillsGapHeatmapMapper
{
    public static SkillsGapHeatmapReadinessDto ToDto(SkillsGapHeatmapReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SkillsGapHeatmapReadinessState,
            entity.GapCatalogBoundaryState,
            entity.HeatmapBindingIntakeBoundaryState,
            entity.SeverityScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.GapReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SkillsTaxonomySourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.TalentDemandForecastSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.SkillsGapHeatmapReadinessVersion,
            entity.DeferredReason);

    public static SkillsGapHeatmapReadinessListItemDto ToListItem(SkillsGapHeatmapReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SkillsGapHeatmapReadinessState,
            entity.GapCatalogBoundaryState,
            entity.SkillsTaxonomySourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static SkillsGapHeatmapAuditMetadataDto ToAuditMetadata(SkillsGapHeatmapReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
