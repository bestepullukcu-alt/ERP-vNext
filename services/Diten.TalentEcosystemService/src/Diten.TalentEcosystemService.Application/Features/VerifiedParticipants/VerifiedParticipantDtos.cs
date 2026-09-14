using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;

public sealed record VerifiedParticipantAccessDto(
    Guid Id,
    string Code,
    string DisplayName,
    Guid? AssociationMembershipId,
    string MemberCompanyReference,
    string HrParticipantReference,
    string HcmFoundationReference,
    Guid? ConsentVisibilityPolicyId,
    TepVerificationState VerificationState,
    TepAccessState AccessState,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepShellDependencyStatus HcmValidationState,
    TepShellDependencyStatus AssociationValidationState,
    TepVerifiedCompanyAccessState VerifiedCompanyAccessState,
    IReadOnlyList<TepVerifiedParticipantDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int VerificationVersion,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    string? DeferredReason);

public sealed record VerifiedParticipantAccessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepVerificationState VerificationState,
    TepAccessState AccessState,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepShellDependencyStatus HcmValidationState,
    TepShellDependencyStatus AssociationValidationState,
    TepVerifiedCompanyAccessState VerifiedCompanyAccessState,
    DateTimeOffset? LastEvaluatedAt,
    int VerificationVersion);

public sealed record TepVerifiedParticipantDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record VerifiedParticipantEvaluationDto(
    Guid Id,
    bool VerificationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record VerifiedParticipantAuditMetadataDto(
    Guid Id,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    IReadOnlyList<TepVerifiedParticipantDependencyStateDto> DependencyStates,
    string? DeferredReason);
