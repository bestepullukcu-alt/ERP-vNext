using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes;

public sealed record CandidateDisputeReadinessRequest(
    string Code,
    string DisplayName,
    TepCandidateDisputeReadinessState DisputeReadinessState,
    Guid? CandidateProfileReference,
    Guid? ExitReferenceRecordReference,
    Guid? ReferenceExchangeReference,
    Guid? RehireRecommendationReference,
    TepCandidateResponseBoundaryState ResponseBoundaryState,
    TepDisputeIntakeState DisputeIntakeState,
    TepDisputeReviewState DisputeReviewState,
    TepResolutionLifecycleState ResolutionLifecycleState,
    TepContestabilityState ContestabilityState,
    TepHumanReviewState HumanReviewState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepAuditReadinessState AuditReadinessState,
    TepLocalDeferredPolicyState LegalHoldState,
    TepLocalDeferredPolicyState DeletionPolicyState,
    TepSelfServiceBoundaryState SelfServiceBoundaryState,
    TepExternalDependencyState NotificationDependencyState,
    TepExternalDependencyState DocumentDependencyState,
    TepCandidateDisputeBoundaryState AutomatedDecisionBoundaryState,
    TepCandidateDisputeBoundaryState MarketplaceBoundaryState,
    IReadOnlyList<CandidateDisputeDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int DisputeReadinessVersion,
    string? DeferredReason);

public sealed record EvaluateCandidateDisputeReadinessRequest(bool ReadinessRequested);
