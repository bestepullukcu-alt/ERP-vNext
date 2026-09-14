using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes;

public sealed record CandidateDisputeReadinessDto(
    Guid Id,
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

public sealed record CandidateDisputeReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepCandidateDisputeReadinessState DisputeReadinessState,
    TepCandidateResponseBoundaryState ResponseBoundaryState,
    TepDisputeIntakeState DisputeIntakeState,
    TepDisputeReviewState DisputeReviewState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    DateTimeOffset? LastEvaluatedAt,
    int DisputeReadinessVersion);

public sealed record CandidateDisputeDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record CandidateDisputeEvaluationDto(
    Guid Id,
    bool ReadinessAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record CandidateDisputeAuditMetadataDto(
    Guid Id,
    TepHumanReviewState HumanReviewState,
    TepContestabilityState ContestabilityState,
    TepCandidateResponseBoundaryState ResponseBoundaryState,
    TepDisputeIntakeState DisputeIntakeState,
    TepDisputeReviewState DisputeReviewState,
    TepResolutionLifecycleState ResolutionLifecycleState,
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
    string? DeferredReason);
