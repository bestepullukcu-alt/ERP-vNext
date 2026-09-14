using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool;

public class IndustrySuccessionPoolReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IndustrySuccessionPoolReadinessState IndustrySuccessionPoolReadinessState { get; init; } = IndustrySuccessionPoolReadinessState.Draft;
    public IndustrySuccessionPoolReadinessState PoolCatalogBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState CandidateInclusionIntakeBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState ReadinessTierScopeBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState VisibilityControlBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState SuccessionReviewBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState AutomatedDecisionBoundaryState { get; init; } = IndustrySuccessionPoolReadinessState.Blocked;
    public IndustrySuccessionPoolReadinessState TalentDataSourceDependencyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState ConsentPolicyDependencyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState TalentPoolSourceDependencyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState NotificationDependencyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState ConsentPreconditionState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState DataMinimizationState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState RetentionPolicyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IndustrySuccessionPoolReadinessState PublicationPolicyState { get; init; } = IndustrySuccessionPoolReadinessState.Deferred;
    public IReadOnlyDictionary<string, IndustrySuccessionPoolReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, IndustrySuccessionPoolReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long IndustrySuccessionPoolReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record IndustrySuccessionPoolReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustrySuccessionPoolReadinessState IndustrySuccessionPoolReadinessState,
    IndustrySuccessionPoolReadinessState PoolCatalogBoundaryState,
    IndustrySuccessionPoolReadinessState CandidateInclusionIntakeBoundaryState,
    IndustrySuccessionPoolReadinessState ReadinessTierScopeBoundaryState,
    IndustrySuccessionPoolReadinessState VisibilityControlBoundaryState,
    IndustrySuccessionPoolReadinessState SuccessionReviewBoundaryState,
    IndustrySuccessionPoolReadinessState AutomatedDecisionBoundaryState,
    IndustrySuccessionPoolReadinessState TalentDataSourceDependencyState,
    IndustrySuccessionPoolReadinessState ConsentPolicyDependencyState,
    IndustrySuccessionPoolReadinessState TalentPoolSourceDependencyState,
    IndustrySuccessionPoolReadinessState NotificationDependencyState,
    IndustrySuccessionPoolReadinessState ConsentPreconditionState,
    IndustrySuccessionPoolReadinessState DataMinimizationState,
    IndustrySuccessionPoolReadinessState RetentionPolicyState,
    IndustrySuccessionPoolReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, IndustrySuccessionPoolReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long IndustrySuccessionPoolReadinessVersion,
    string? DeferredReason);

public sealed record IndustrySuccessionPoolReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustrySuccessionPoolReadinessState IndustrySuccessionPoolReadinessState,
    IndustrySuccessionPoolReadinessState PoolCatalogBoundaryState,
    IndustrySuccessionPoolReadinessState TalentDataSourceDependencyState,
    IndustrySuccessionPoolReadinessState VisibilityControlBoundaryState,
    IndustrySuccessionPoolReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record IndustrySuccessionPoolAuditMetadataDto(
    Guid Id,
    string Code,
    IndustrySuccessionPoolReadinessState RetentionPolicyState,
    IndustrySuccessionPoolReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class IndustrySuccessionPoolMapper
{
    public static IndustrySuccessionPoolReadinessDto ToDto(IndustrySuccessionPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustrySuccessionPoolReadinessState,
            entity.PoolCatalogBoundaryState,
            entity.CandidateInclusionIntakeBoundaryState,
            entity.ReadinessTierScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.SuccessionReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.TalentPoolSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.IndustrySuccessionPoolReadinessVersion,
            entity.DeferredReason);

    public static IndustrySuccessionPoolReadinessListItemDto ToListItem(IndustrySuccessionPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustrySuccessionPoolReadinessState,
            entity.PoolCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static IndustrySuccessionPoolAuditMetadataDto ToAuditMetadata(IndustrySuccessionPoolReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
