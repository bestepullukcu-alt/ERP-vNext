using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport;

public class IndustrySkillPassportReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IndustrySkillPassportReadinessState IndustrySkillPassportReadinessState { get; init; } = IndustrySkillPassportReadinessState.Draft;
    public IndustrySkillPassportReadinessState SkillClaimCatalogBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState AttestationIntakeBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState VerificationScopeBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState VisibilityControlBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState PassportReviewBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState AutomatedDecisionBoundaryState { get; init; } = IndustrySkillPassportReadinessState.Blocked;
    public IndustrySkillPassportReadinessState TalentDataSourceDependencyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState ConsentPolicyDependencyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState CertificationSourceDependencyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState NotificationDependencyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState ConsentPreconditionState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState DataMinimizationState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState RetentionPolicyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IndustrySkillPassportReadinessState VerificationPolicyState { get; init; } = IndustrySkillPassportReadinessState.Deferred;
    public IReadOnlyDictionary<string, IndustrySkillPassportReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, IndustrySkillPassportReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long IndustrySkillPassportReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record IndustrySkillPassportReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustrySkillPassportReadinessState IndustrySkillPassportReadinessState,
    IndustrySkillPassportReadinessState SkillClaimCatalogBoundaryState,
    IndustrySkillPassportReadinessState AttestationIntakeBoundaryState,
    IndustrySkillPassportReadinessState VerificationScopeBoundaryState,
    IndustrySkillPassportReadinessState VisibilityControlBoundaryState,
    IndustrySkillPassportReadinessState PassportReviewBoundaryState,
    IndustrySkillPassportReadinessState AutomatedDecisionBoundaryState,
    IndustrySkillPassportReadinessState TalentDataSourceDependencyState,
    IndustrySkillPassportReadinessState ConsentPolicyDependencyState,
    IndustrySkillPassportReadinessState CertificationSourceDependencyState,
    IndustrySkillPassportReadinessState NotificationDependencyState,
    IndustrySkillPassportReadinessState ConsentPreconditionState,
    IndustrySkillPassportReadinessState DataMinimizationState,
    IndustrySkillPassportReadinessState RetentionPolicyState,
    IndustrySkillPassportReadinessState VerificationPolicyState,
    IReadOnlyDictionary<string, IndustrySkillPassportReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long IndustrySkillPassportReadinessVersion,
    string? DeferredReason);

public sealed record IndustrySkillPassportReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustrySkillPassportReadinessState IndustrySkillPassportReadinessState,
    IndustrySkillPassportReadinessState SkillClaimCatalogBoundaryState,
    IndustrySkillPassportReadinessState TalentDataSourceDependencyState,
    IndustrySkillPassportReadinessState VisibilityControlBoundaryState,
    IndustrySkillPassportReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record IndustrySkillPassportAuditMetadataDto(
    Guid Id,
    string Code,
    IndustrySkillPassportReadinessState RetentionPolicyState,
    IndustrySkillPassportReadinessState VerificationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class IndustrySkillPassportMapper
{
    public static IndustrySkillPassportReadinessDto ToDto(IndustrySkillPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustrySkillPassportReadinessState,
            entity.SkillClaimCatalogBoundaryState,
            entity.AttestationIntakeBoundaryState,
            entity.VerificationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.PassportReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.CertificationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.VerificationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.IndustrySkillPassportReadinessVersion,
            entity.DeferredReason);

    public static IndustrySkillPassportReadinessListItemDto ToListItem(IndustrySkillPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustrySkillPassportReadinessState,
            entity.SkillClaimCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static IndustrySkillPassportAuditMetadataDto ToAuditMetadata(IndustrySkillPassportReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.VerificationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
