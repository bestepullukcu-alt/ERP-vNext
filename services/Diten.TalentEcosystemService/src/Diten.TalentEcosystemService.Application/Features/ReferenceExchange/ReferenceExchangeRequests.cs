using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange;

public sealed record ReferenceExchangeReadinessRequest(
    string Code,
    string DisplayName,
    TepReferenceExchangeReadinessState ExchangeReadinessState,
    TepReferenceExchangeAvailabilityState ExchangeAvailabilityState,
    TepParticipantEligibilityState ParticipantEligibilityState,
    Guid? AssociationMembershipReference,
    Guid? VerifiedParticipantReference,
    Guid? ConsentVisibilityPolicyReference,
    Guid? CandidateProfileReference,
    Guid? ExitReferenceRecordReference,
    Guid? ReviewBoardCaseReference,
    Guid? TrustLevelPolicyReference,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState MinimizationState,
    TepLegalPrivacyBasisState LegalPrivacyBasisState,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepAuditReadinessState AuditReadinessState,
    TepAbuseControlState AbuseControlState,
    TepThrottlingPolicyState ThrottlingPolicyState,
    TepReviewDisputeBoundaryState ReviewDisputeBoundaryState,
    TepExternalDependencyBoundaryState NotificationDependencyState,
    TepExternalDependencyBoundaryState DocumentDependencyState,
    IReadOnlyList<ReferenceExchangeDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int ReferenceExchangeVersion,
    string? DeferredReason);

public sealed record EvaluateReferenceExchangeReadinessRequest(bool ReadinessRequested);
