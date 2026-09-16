using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepExitReferenceRecordMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? CandidateProfileReference { get; set; }
    public string OffboardingCaseReference { get; set; } = string.Empty;
    public Guid? VerifiedParticipantReference { get; set; }
    public Guid? AssociationMembershipReference { get; set; }
    public Guid? ConsentVisibilityPolicyReference { get; set; }
    public Guid? ReviewBoardCaseReference { get; set; }
    public Guid? TrustLevelPolicyReference { get; set; }
    public TepExitReferenceRecordState ReferenceRecordState { get; set; } = TepExitReferenceRecordState.Deferred;
    public TepReferenceSharingState ReferenceSharingState { get; set; } = TepReferenceSharingState.Deferred;
    public TepConsentRequirementState ConsentPreconditionState { get; set; } = TepConsentRequirementState.Deferred;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Deferred;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepEvidenceRetentionDecisionState EvidenceRetentionState { get; set; } = TepEvidenceRetentionDecisionState.Deferred;
    public TepReviewDisputeBoundaryState ReviewDisputeBoundaryState { get; set; } = TepReviewDisputeBoundaryState.Deferred;
    public List<TepExitReferenceDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int ReferenceRecordVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepExitReferenceDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
