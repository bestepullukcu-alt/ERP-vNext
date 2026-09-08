using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool;

public class IndustryTalentPoolReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IndustryTalentPoolReadinessState IndustryTalentPoolReadinessState { get; init; } = IndustryTalentPoolReadinessState.Draft;
    public IndustryTalentPoolReadinessState PoolMembershipCatalogBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState CandidateInclusionIntakeBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState EligibilityScopeBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState VisibilityControlBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState PoolCurationReviewBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState AutomatedDecisionBoundaryState { get; init; } = IndustryTalentPoolReadinessState.Blocked;
    public IndustryTalentPoolReadinessState TalentDataSourceDependencyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState ConsentPolicyDependencyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState ReputationSourceDependencyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState NotificationDependencyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState ConsentPreconditionState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState DataMinimizationState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState RetentionPolicyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IndustryTalentPoolReadinessState EligibilityPolicyState { get; init; } = IndustryTalentPoolReadinessState.Deferred;
    public IReadOnlyDictionary<string, IndustryTalentPoolReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, IndustryTalentPoolReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long IndustryTalentPoolReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record IndustryTalentPoolReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustryTalentPoolReadinessState IndustryTalentPoolReadinessState,
    IndustryTalentPoolReadinessState PoolMembershipCatalogBoundaryState,
    IndustryTalentPoolReadinessState CandidateInclusionIntakeBoundaryState,
    IndustryTalentPoolReadinessState EligibilityScopeBoundaryState,
    IndustryTalentPoolReadinessState VisibilityControlBoundaryState,
    IndustryTalentPoolReadinessState PoolCurationReviewBoundaryState,
    IndustryTalentPoolReadinessState AutomatedDecisionBoundaryState,
    IndustryTalentPoolReadinessState TalentDataSourceDependencyState,
    IndustryTalentPoolReadinessState ConsentPolicyDependencyState,
    IndustryTalentPoolReadinessState ReputationSourceDependencyState,
    IndustryTalentPoolReadinessState NotificationDependencyState,
    IndustryTalentPoolReadinessState ConsentPreconditionState,
    IndustryTalentPoolReadinessState DataMinimizationState,
    IndustryTalentPoolReadinessState RetentionPolicyState,
    IndustryTalentPoolReadinessState EligibilityPolicyState,
    IReadOnlyDictionary<string, IndustryTalentPoolReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long IndustryTalentPoolReadinessVersion,
    string? DeferredReason);

public sealed record IndustryTalentPoolReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustryTalentPoolReadinessState IndustryTalentPoolReadinessState,
    IndustryTalentPoolReadinessState PoolMembershipCatalogBoundaryState,
    IndustryTalentPoolReadinessState TalentDataSourceDependencyState,
    IndustryTalentPoolReadinessState VisibilityControlBoundaryState,
    IndustryTalentPoolReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record IndustryTalentPoolAuditMetadataDto(
    Guid Id,
    string Code,
    IndustryTalentPoolReadinessState RetentionPolicyState,
    IndustryTalentPoolReadinessState EligibilityPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class IndustryTalentPoolMapper
{
    public static IndustryTalentPoolReadinessDto ToDto(IndustryTalentPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustryTalentPoolReadinessState,
            entity.PoolMembershipCatalogBoundaryState,
            entity.CandidateInclusionIntakeBoundaryState,
            entity.EligibilityScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.PoolCurationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.ReputationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EligibilityPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.IndustryTalentPoolReadinessVersion,
            entity.DeferredReason);

    public static IndustryTalentPoolReadinessListItemDto ToListItem(IndustryTalentPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustryTalentPoolReadinessState,
            entity.PoolMembershipCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static IndustryTalentPoolAuditMetadataDto ToAuditMetadata(IndustryTalentPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EligibilityPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
