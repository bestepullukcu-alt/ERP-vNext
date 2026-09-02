using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;

public sealed record ExitReferenceRecordDto(
    Guid Id,
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

public sealed record ExitReferenceRecordListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepExitReferenceRecordState ReferenceRecordState,
    TepReferenceSharingState ReferenceSharingState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepReviewDisputeBoundaryState ReviewDisputeBoundaryState,
    DateTimeOffset? LastEvaluatedAt,
    int ReferenceRecordVersion);

public sealed record ExitReferenceDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record ExitReferenceEvaluationDto(
    Guid Id,
    bool ActivationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record ExitReferenceAuditMetadataDto(
    Guid Id,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepReviewDisputeBoundaryState ReviewDisputeBoundaryState,
    IReadOnlyList<ExitReferenceDependencyStateDto> DependencyStates,
    string? DeferredReason);
