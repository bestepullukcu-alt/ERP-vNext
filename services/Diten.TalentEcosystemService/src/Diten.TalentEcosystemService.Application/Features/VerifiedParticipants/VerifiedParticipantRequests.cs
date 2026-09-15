using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;

public sealed record VerifiedParticipantAccessRequest(
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

public sealed record EvaluateVerifiedParticipantAccessRequest(bool VerificationRequested);

public sealed record VerifyParticipantAccessRequest(bool VerificationRequested);
