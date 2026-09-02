using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations;

public sealed record RehireRecommendationReadinessDto(
    Guid Id,
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

public sealed record RehireRecommendationReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepRehireRecommendationReadinessState RecommendationReadinessState,
    TepRehireRecommendationPolicyState RecommendationPolicyState,
    TepRehireRecommendationEvaluationState RecommendationEvaluationState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState MinimizationState,
    DateTimeOffset? LastEvaluatedAt,
    int RecommendationNetworkVersion);

public sealed record RehireRecommendationDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record RehireRecommendationEvaluationDto(
    Guid Id,
    bool ReadinessAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record RehireRecommendationAuditMetadataDto(
    Guid Id,
    TepRecommendationExplainabilityState ExplainabilityState,
    TepHumanReviewState HumanReviewState,
    TepContestabilityState ContestabilityState,
    TepAbuseControlState AbuseControlState,
    TepMisuseDetectionState MisuseDetectionState,
    TepThrottlingPolicyState ThrottlingState,
    TepEscalationState EscalationState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepAuditReadinessState AuditReadinessState,
    TepLocalDeferredPolicyState LegalHoldState,
    TepLocalDeferredPolicyState DeletionPolicyState,
    TepReviewDisputeBoundaryState CandidateResponseBoundaryState,
    IReadOnlyList<RehireRecommendationDependencyStateDto> DependencyStates,
    string? DeferredReason);
