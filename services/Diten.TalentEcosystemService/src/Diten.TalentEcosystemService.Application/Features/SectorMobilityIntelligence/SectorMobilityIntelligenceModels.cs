using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence;

public class SectorMobilityIntelligenceReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public SectorMobilityIntelligenceReadinessState SectorMobilityIntelligenceReadinessState { get; init; } = SectorMobilityIntelligenceReadinessState.Draft;
    public SectorMobilityIntelligenceReadinessState MobilityCatalogBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState FlowBindingIntakeBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState CorridorScopeBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState VisibilityControlBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState MobilityReviewBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState AutomatedDecisionBoundaryState { get; init; } = SectorMobilityIntelligenceReadinessState.Blocked;
    public SectorMobilityIntelligenceReadinessState SectorTrendSourceDependencyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState WorkforceAnalyticsSourceDependencyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState SkillsTaxonomySourceDependencyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState NotificationDependencyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState ConsentPreconditionState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState DataMinimizationState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState RetentionPolicyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public SectorMobilityIntelligenceReadinessState PublicationPolicyState { get; init; } = SectorMobilityIntelligenceReadinessState.Deferred;
    public IReadOnlyDictionary<string, SectorMobilityIntelligenceReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, SectorMobilityIntelligenceReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long SectorMobilityIntelligenceReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record SectorMobilityIntelligenceReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    SectorMobilityIntelligenceReadinessState SectorMobilityIntelligenceReadinessState,
    SectorMobilityIntelligenceReadinessState MobilityCatalogBoundaryState,
    SectorMobilityIntelligenceReadinessState FlowBindingIntakeBoundaryState,
    SectorMobilityIntelligenceReadinessState CorridorScopeBoundaryState,
    SectorMobilityIntelligenceReadinessState VisibilityControlBoundaryState,
    SectorMobilityIntelligenceReadinessState MobilityReviewBoundaryState,
    SectorMobilityIntelligenceReadinessState AutomatedDecisionBoundaryState,
    SectorMobilityIntelligenceReadinessState SectorTrendSourceDependencyState,
    SectorMobilityIntelligenceReadinessState WorkforceAnalyticsSourceDependencyState,
    SectorMobilityIntelligenceReadinessState SkillsTaxonomySourceDependencyState,
    SectorMobilityIntelligenceReadinessState NotificationDependencyState,
    SectorMobilityIntelligenceReadinessState ConsentPreconditionState,
    SectorMobilityIntelligenceReadinessState DataMinimizationState,
    SectorMobilityIntelligenceReadinessState RetentionPolicyState,
    SectorMobilityIntelligenceReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, SectorMobilityIntelligenceReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long SectorMobilityIntelligenceReadinessVersion,
    string? DeferredReason);

public sealed record SectorMobilityIntelligenceReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    SectorMobilityIntelligenceReadinessState SectorMobilityIntelligenceReadinessState,
    SectorMobilityIntelligenceReadinessState MobilityCatalogBoundaryState,
    SectorMobilityIntelligenceReadinessState SectorTrendSourceDependencyState,
    SectorMobilityIntelligenceReadinessState VisibilityControlBoundaryState,
    SectorMobilityIntelligenceReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record SectorMobilityIntelligenceAuditMetadataDto(
    Guid Id,
    string Code,
    SectorMobilityIntelligenceReadinessState RetentionPolicyState,
    SectorMobilityIntelligenceReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class SectorMobilityIntelligenceMapper
{
    public static SectorMobilityIntelligenceReadinessDto ToDto(SectorMobilityIntelligenceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SectorMobilityIntelligenceReadinessState,
            entity.MobilityCatalogBoundaryState,
            entity.FlowBindingIntakeBoundaryState,
            entity.CorridorScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.MobilityReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SectorTrendSourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.SkillsTaxonomySourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.SectorMobilityIntelligenceReadinessVersion,
            entity.DeferredReason);

    public static SectorMobilityIntelligenceReadinessListItemDto ToListItem(SectorMobilityIntelligenceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SectorMobilityIntelligenceReadinessState,
            entity.MobilityCatalogBoundaryState,
            entity.SectorTrendSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static SectorMobilityIntelligenceAuditMetadataDto ToAuditMetadata(SectorMobilityIntelligenceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
