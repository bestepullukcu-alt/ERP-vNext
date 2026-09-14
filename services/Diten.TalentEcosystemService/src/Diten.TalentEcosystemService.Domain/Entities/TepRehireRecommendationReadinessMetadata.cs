using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepRehireRecommendationReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepRehireRecommendationReadinessState RecommendationReadinessState { get; set; } = TepRehireRecommendationReadinessState.Deferred;
    public TepRehireRecommendationPolicyState RecommendationPolicyState { get; set; } = TepRehireRecommendationPolicyState.Deferred;
    public TepRehireRecommendationEvaluationState RecommendationEvaluationState { get; set; } = TepRehireRecommendationEvaluationState.NotEvaluated;
    public TepRecommendationEligibilityPreconditionState EligibilityPreconditionState { get; set; } = TepRecommendationEligibilityPreconditionState.Deferred;
    public TepConsentRequirementState ConsentPreconditionState { get; set; } = TepConsentRequirementState.Deferred;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Deferred;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepDataMinimizationState MinimizationState { get; set; } = TepDataMinimizationState.Deferred;
    public TepRecommendationExplainabilityState ExplainabilityState { get; set; } = TepRecommendationExplainabilityState.Deferred;
    public TepHumanReviewState HumanReviewState { get; set; } = TepHumanReviewState.Deferred;
    public TepContestabilityState ContestabilityState { get; set; } = TepContestabilityState.Deferred;
    public TepReviewDisputeBoundaryState CandidateResponseBoundaryState { get; set; } = TepReviewDisputeBoundaryState.Deferred;
    public TepAbuseControlState AbuseControlState { get; set; } = TepAbuseControlState.Deferred;
    public TepMisuseDetectionState MisuseDetectionState { get; set; } = TepMisuseDetectionState.Deferred;
    public TepThrottlingPolicyState ThrottlingState { get; set; } = TepThrottlingPolicyState.Deferred;
    public TepEscalationState EscalationState { get; set; } = TepEscalationState.Deferred;
    public TepEvidenceRetentionDecisionState EvidenceRetentionState { get; set; } = TepEvidenceRetentionDecisionState.Deferred;
    public TepAuditReadinessState AuditReadinessState { get; set; } = TepAuditReadinessState.Deferred;
    public TepLocalDeferredPolicyState LegalHoldState { get; set; } = TepLocalDeferredPolicyState.Deferred;
    public TepLocalDeferredPolicyState DeletionPolicyState { get; set; } = TepLocalDeferredPolicyState.Deferred;
    public Guid? ReferenceExchangeReference { get; set; }
    public Guid? ExitReferenceRecordReference { get; set; }
    public Guid? CandidateProfileReference { get; set; }
    public Guid? VerifiedParticipantReference { get; set; }
    public Guid? AssociationMembershipReference { get; set; }
    public Guid? ConsentVisibilityPolicyReference { get; set; }
    public Guid? ReviewBoardCaseReference { get; set; }
    public Guid? TrustLevelPolicyReference { get; set; }
    public List<TepRehireRecommendationDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int RecommendationNetworkVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepRehireRecommendationDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
