using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;

public sealed record ExitReferenceRecordRequest(
    string Code,
    string DisplayName,
    Guid? CandidateProfileReference,
    string OffboardingCaseReference,
    Guid? VerifiedParticipantReference,
    Guid? AssociationMembershipReference,
    Guid? ConsentVisibilityPolicyReference,
    Guid? ReviewBoardCaseReference,
    Guid? TrustLevelPolicyReference,
    TepExitReferenceRecordState ReferenceRecordState,
    TepReferenceSharingState ReferenceSharingState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepReviewDisputeBoundaryState ReviewDisputeBoundaryState,
    IReadOnlyList<ExitReferenceDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int ReferenceRecordVersion,
    string? DeferredReason);

public sealed record EvaluateExitReferenceRecordRequest(bool ActivationRequested);
