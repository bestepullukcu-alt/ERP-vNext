using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport;

public class CandidateCareerPassportReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CandidateCareerPassportReadinessState CandidateCareerPassportReadinessState { get; init; } = CandidateCareerPassportReadinessState.Draft;
    public CandidateCareerPassportReadinessState CareerMilestoneCatalogBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState ExperienceIntakeBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState OwnershipScopeBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState VisibilityControlBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState PassportReviewBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState AutomatedDecisionBoundaryState { get; init; } = CandidateCareerPassportReadinessState.Blocked;
    public CandidateCareerPassportReadinessState TalentDataSourceDependencyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState ConsentPolicyDependencyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState SkillPassportSourceDependencyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState NotificationDependencyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState ConsentPreconditionState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState DataMinimizationState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState RetentionPolicyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public CandidateCareerPassportReadinessState PortabilityPolicyState { get; init; } = CandidateCareerPassportReadinessState.Deferred;
    public IReadOnlyDictionary<string, CandidateCareerPassportReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, CandidateCareerPassportReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long CandidateCareerPassportReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record CandidateCareerPassportReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    CandidateCareerPassportReadinessState CandidateCareerPassportReadinessState,
    CandidateCareerPassportReadinessState CareerMilestoneCatalogBoundaryState,
    CandidateCareerPassportReadinessState ExperienceIntakeBoundaryState,
    CandidateCareerPassportReadinessState OwnershipScopeBoundaryState,
    CandidateCareerPassportReadinessState VisibilityControlBoundaryState,
    CandidateCareerPassportReadinessState PassportReviewBoundaryState,
    CandidateCareerPassportReadinessState AutomatedDecisionBoundaryState,
    CandidateCareerPassportReadinessState TalentDataSourceDependencyState,
    CandidateCareerPassportReadinessState ConsentPolicyDependencyState,
    CandidateCareerPassportReadinessState SkillPassportSourceDependencyState,
    CandidateCareerPassportReadinessState NotificationDependencyState,
    CandidateCareerPassportReadinessState ConsentPreconditionState,
    CandidateCareerPassportReadinessState DataMinimizationState,
    CandidateCareerPassportReadinessState RetentionPolicyState,
    CandidateCareerPassportReadinessState PortabilityPolicyState,
    IReadOnlyDictionary<string, CandidateCareerPassportReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long CandidateCareerPassportReadinessVersion,
    string? DeferredReason);

public sealed record CandidateCareerPassportReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    CandidateCareerPassportReadinessState CandidateCareerPassportReadinessState,
    CandidateCareerPassportReadinessState CareerMilestoneCatalogBoundaryState,
    CandidateCareerPassportReadinessState TalentDataSourceDependencyState,
    CandidateCareerPassportReadinessState VisibilityControlBoundaryState,
    CandidateCareerPassportReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record CandidateCareerPassportAuditMetadataDto(
    Guid Id,
    string Code,
    CandidateCareerPassportReadinessState RetentionPolicyState,
    CandidateCareerPassportReadinessState PortabilityPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class CandidateCareerPassportMapper
{
    public static CandidateCareerPassportReadinessDto ToDto(CandidateCareerPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CandidateCareerPassportReadinessState,
            entity.CareerMilestoneCatalogBoundaryState,
            entity.ExperienceIntakeBoundaryState,
            entity.OwnershipScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.PassportReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.SkillPassportSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PortabilityPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.CandidateCareerPassportReadinessVersion,
            entity.DeferredReason);

    public static CandidateCareerPassportReadinessListItemDto ToListItem(CandidateCareerPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CandidateCareerPassportReadinessState,
            entity.CareerMilestoneCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static CandidateCareerPassportAuditMetadataDto ToAuditMetadata(CandidateCareerPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PortabilityPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
