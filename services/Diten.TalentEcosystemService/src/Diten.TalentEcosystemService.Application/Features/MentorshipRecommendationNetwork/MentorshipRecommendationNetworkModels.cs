using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork;

public class MentorshipRecommendationNetworkReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public MentorshipRecommendationNetworkReadinessState MentorshipRecommendationNetworkReadinessState { get; init; } = MentorshipRecommendationNetworkReadinessState.Draft;
    public MentorshipRecommendationNetworkReadinessState NetworkCatalogBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState PairingIntakeBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState RecommendationScopeBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState VisibilityControlBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState NetworkReviewBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState AutomatedDecisionBoundaryState { get; init; } = MentorshipRecommendationNetworkReadinessState.Blocked;
    public MentorshipRecommendationNetworkReadinessState TalentDataSourceDependencyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState ConsentPolicyDependencyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState ReputationSourceDependencyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState NotificationDependencyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState ConsentPreconditionState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState DataMinimizationState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState RetentionPolicyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public MentorshipRecommendationNetworkReadinessState PublicationPolicyState { get; init; } = MentorshipRecommendationNetworkReadinessState.Deferred;
    public IReadOnlyDictionary<string, MentorshipRecommendationNetworkReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, MentorshipRecommendationNetworkReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long MentorshipRecommendationNetworkReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record MentorshipRecommendationNetworkReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    MentorshipRecommendationNetworkReadinessState MentorshipRecommendationNetworkReadinessState,
    MentorshipRecommendationNetworkReadinessState NetworkCatalogBoundaryState,
    MentorshipRecommendationNetworkReadinessState PairingIntakeBoundaryState,
    MentorshipRecommendationNetworkReadinessState RecommendationScopeBoundaryState,
    MentorshipRecommendationNetworkReadinessState VisibilityControlBoundaryState,
    MentorshipRecommendationNetworkReadinessState NetworkReviewBoundaryState,
    MentorshipRecommendationNetworkReadinessState AutomatedDecisionBoundaryState,
    MentorshipRecommendationNetworkReadinessState TalentDataSourceDependencyState,
    MentorshipRecommendationNetworkReadinessState ConsentPolicyDependencyState,
    MentorshipRecommendationNetworkReadinessState ReputationSourceDependencyState,
    MentorshipRecommendationNetworkReadinessState NotificationDependencyState,
    MentorshipRecommendationNetworkReadinessState ConsentPreconditionState,
    MentorshipRecommendationNetworkReadinessState DataMinimizationState,
    MentorshipRecommendationNetworkReadinessState RetentionPolicyState,
    MentorshipRecommendationNetworkReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, MentorshipRecommendationNetworkReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long MentorshipRecommendationNetworkReadinessVersion,
    string? DeferredReason);

public sealed record MentorshipRecommendationNetworkReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    MentorshipRecommendationNetworkReadinessState MentorshipRecommendationNetworkReadinessState,
    MentorshipRecommendationNetworkReadinessState NetworkCatalogBoundaryState,
    MentorshipRecommendationNetworkReadinessState TalentDataSourceDependencyState,
    MentorshipRecommendationNetworkReadinessState VisibilityControlBoundaryState,
    MentorshipRecommendationNetworkReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record MentorshipRecommendationNetworkAuditMetadataDto(
    Guid Id,
    string Code,
    MentorshipRecommendationNetworkReadinessState RetentionPolicyState,
    MentorshipRecommendationNetworkReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class MentorshipRecommendationNetworkMapper
{
    public static MentorshipRecommendationNetworkReadinessDto ToDto(MentorshipRecommendationNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MentorshipRecommendationNetworkReadinessState,
            entity.NetworkCatalogBoundaryState,
            entity.PairingIntakeBoundaryState,
            entity.RecommendationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.NetworkReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.ReputationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.MentorshipRecommendationNetworkReadinessVersion,
            entity.DeferredReason);

    public static MentorshipRecommendationNetworkReadinessListItemDto ToListItem(MentorshipRecommendationNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MentorshipRecommendationNetworkReadinessState,
            entity.NetworkCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static MentorshipRecommendationNetworkAuditMetadataDto ToAuditMetadata(MentorshipRecommendationNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
