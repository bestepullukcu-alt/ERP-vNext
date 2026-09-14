using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepCandidateDisputeReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepCandidateDisputeReadinessState DisputeReadinessState { get; set; } = TepCandidateDisputeReadinessState.Deferred;
    public Guid? CandidateProfileReference { get; set; }
    public Guid? ExitReferenceRecordReference { get; set; }
    public Guid? ReferenceExchangeReference { get; set; }
    public Guid? RehireRecommendationReference { get; set; }
    public TepCandidateResponseBoundaryState ResponseBoundaryState { get; set; } = TepCandidateResponseBoundaryState.Deferred;
    public TepDisputeIntakeState DisputeIntakeState { get; set; } = TepDisputeIntakeState.Deferred;
    public TepDisputeReviewState DisputeReviewState { get; set; } = TepDisputeReviewState.Deferred;
    public TepResolutionLifecycleState ResolutionLifecycleState { get; set; } = TepResolutionLifecycleState.Deferred;
    public TepContestabilityState ContestabilityState { get; set; } = TepContestabilityState.Deferred;
    public TepHumanReviewState HumanReviewState { get; set; } = TepHumanReviewState.Deferred;
    public TepConsentRequirementState ConsentPreconditionState { get; set; } = TepConsentRequirementState.Deferred;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Deferred;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepEvidenceRetentionDecisionState EvidenceRetentionState { get; set; } = TepEvidenceRetentionDecisionState.Deferred;
    public TepAuditReadinessState AuditReadinessState { get; set; } = TepAuditReadinessState.Deferred;
    public TepLocalDeferredPolicyState LegalHoldState { get; set; } = TepLocalDeferredPolicyState.Deferred;
    public TepLocalDeferredPolicyState DeletionPolicyState { get; set; } = TepLocalDeferredPolicyState.Deferred;
    public TepSelfServiceBoundaryState SelfServiceBoundaryState { get; set; } = TepSelfServiceBoundaryState.Deferred;
    public TepExternalDependencyState NotificationDependencyState { get; set; } = TepExternalDependencyState.Deferred;
    public TepExternalDependencyState DocumentDependencyState { get; set; } = TepExternalDependencyState.Deferred;
    public TepCandidateDisputeBoundaryState AutomatedDecisionBoundaryState { get; set; } = TepCandidateDisputeBoundaryState.Deferred;
    public TepCandidateDisputeBoundaryState MarketplaceBoundaryState { get; set; } = TepCandidateDisputeBoundaryState.Deferred;
    public List<TepCandidateDisputeDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int DisputeReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepCandidateDisputeDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
