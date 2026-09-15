using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork;

public class TalentDevelopmentNetworkReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public TalentDevelopmentNetworkReadinessState TalentDevelopmentNetworkReadinessState { get; init; } = TalentDevelopmentNetworkReadinessState.Draft;
    public TalentDevelopmentNetworkReadinessState PathwayCatalogBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState MentorshipLinkIntakeBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState ProgressionScopeBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState VisibilityControlBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState NetworkReviewBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState AutomatedDecisionBoundaryState { get; init; } = TalentDevelopmentNetworkReadinessState.Blocked;
    public TalentDevelopmentNetworkReadinessState TalentDataSourceDependencyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState ConsentPolicyDependencyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState SkillPassportSourceDependencyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState NotificationDependencyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState ConsentPreconditionState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState DataMinimizationState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState RetentionPolicyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public TalentDevelopmentNetworkReadinessState ProgressionPolicyState { get; init; } = TalentDevelopmentNetworkReadinessState.Deferred;
    public IReadOnlyDictionary<string, TalentDevelopmentNetworkReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, TalentDevelopmentNetworkReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long TalentDevelopmentNetworkReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record TalentDevelopmentNetworkReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentDevelopmentNetworkReadinessState TalentDevelopmentNetworkReadinessState,
    TalentDevelopmentNetworkReadinessState PathwayCatalogBoundaryState,
    TalentDevelopmentNetworkReadinessState MentorshipLinkIntakeBoundaryState,
    TalentDevelopmentNetworkReadinessState ProgressionScopeBoundaryState,
    TalentDevelopmentNetworkReadinessState VisibilityControlBoundaryState,
    TalentDevelopmentNetworkReadinessState NetworkReviewBoundaryState,
    TalentDevelopmentNetworkReadinessState AutomatedDecisionBoundaryState,
    TalentDevelopmentNetworkReadinessState TalentDataSourceDependencyState,
    TalentDevelopmentNetworkReadinessState ConsentPolicyDependencyState,
    TalentDevelopmentNetworkReadinessState SkillPassportSourceDependencyState,
    TalentDevelopmentNetworkReadinessState NotificationDependencyState,
    TalentDevelopmentNetworkReadinessState ConsentPreconditionState,
    TalentDevelopmentNetworkReadinessState DataMinimizationState,
    TalentDevelopmentNetworkReadinessState RetentionPolicyState,
    TalentDevelopmentNetworkReadinessState ProgressionPolicyState,
    IReadOnlyDictionary<string, TalentDevelopmentNetworkReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long TalentDevelopmentNetworkReadinessVersion,
    string? DeferredReason);

public sealed record TalentDevelopmentNetworkReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentDevelopmentNetworkReadinessState TalentDevelopmentNetworkReadinessState,
    TalentDevelopmentNetworkReadinessState PathwayCatalogBoundaryState,
    TalentDevelopmentNetworkReadinessState TalentDataSourceDependencyState,
    TalentDevelopmentNetworkReadinessState VisibilityControlBoundaryState,
    TalentDevelopmentNetworkReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record TalentDevelopmentNetworkAuditMetadataDto(
    Guid Id,
    string Code,
    TalentDevelopmentNetworkReadinessState RetentionPolicyState,
    TalentDevelopmentNetworkReadinessState ProgressionPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class TalentDevelopmentNetworkMapper
{
    public static TalentDevelopmentNetworkReadinessDto ToDto(TalentDevelopmentNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentDevelopmentNetworkReadinessState,
            entity.PathwayCatalogBoundaryState,
            entity.MentorshipLinkIntakeBoundaryState,
            entity.ProgressionScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.NetworkReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.SkillPassportSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.ProgressionPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.TalentDevelopmentNetworkReadinessVersion,
            entity.DeferredReason);

    public static TalentDevelopmentNetworkReadinessListItemDto ToListItem(TalentDevelopmentNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentDevelopmentNetworkReadinessState,
            entity.PathwayCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static TalentDevelopmentNetworkAuditMetadataDto ToAuditMetadata(TalentDevelopmentNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.ProgressionPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
