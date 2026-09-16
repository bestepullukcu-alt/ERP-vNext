using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations;

public sealed record RehireRecommendationReadinessRequest(
    string Code,
    string DisplayName,
    TepRehireRecommendationReadinessState RecommendationReadinessState,
    TepRehireRecommendationPolicyState RecommendationPolicyState,
    TepRehireRecommendationEvaluationState RecommendationEvaluationState,
    TepRecommendationEligibilityPreconditionState EligibilityPreconditionState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState MinimizationState,
    TepRecommendationExplainabilityState ExplainabilityState,
    TepHumanReviewState HumanReviewState,
    TepContestabilityState ContestabilityState,
    TepReviewDisputeBoundaryState CandidateResponseBoundaryState,
    TepAbuseControlState AbuseControlState,
    TepMisuseDetectionState MisuseDetectionState,
    TepThrottlingPolicyState ThrottlingState,
    TepEscalationState EscalationState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepAuditReadinessState AuditReadinessState,
    TepLocalDeferredPolicyState LegalHoldState,
    TepLocalDeferredPolicyState DeletionPolicyState,
    Guid? ReferenceExchangeReference,
    Guid? ExitReferenceRecordReference,
    Guid? CandidateProfileReference,
    Guid? VerifiedParticipantReference,
    Guid? AssociationMembershipReference,
    Guid? ConsentVisibilityPolicyReference,
    Guid? ReviewBoardCaseReference,
    Guid? TrustLevelPolicyReference,
    IReadOnlyList<RehireRecommendationDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int RecommendationNetworkVersion,
    string? DeferredReason);

public sealed record EvaluateRehireRecommendationReadinessRequest(bool ReadinessRequested);
