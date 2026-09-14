using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange;

public sealed record ReferenceExchangeReadinessDto(
    Guid Id,
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

public sealed record ReferenceExchangeReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepReferenceExchangeReadinessState ExchangeReadinessState,
    TepReferenceExchangeAvailabilityState ExchangeAvailabilityState,
    TepParticipantEligibilityState ParticipantEligibilityState,
    TepConsentRequirementState ConsentPreconditionState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState MinimizationState,
    DateTimeOffset? LastEvaluatedAt,
    int ReferenceExchangeVersion);

public sealed record ReferenceExchangeDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record ReferenceExchangeEvaluationDto(
    Guid Id,
    bool ReadinessAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record ReferenceExchangeAuditMetadataDto(
    Guid Id,
    TepEvidenceRetentionDecisionState EvidenceRetentionState,
    TepAuditReadinessState AuditReadinessState,
    TepAbuseControlState AbuseControlState,
    TepThrottlingPolicyState ThrottlingPolicyState,
    TepReviewDisputeBoundaryState ReviewDisputeBoundaryState,
    TepExternalDependencyBoundaryState NotificationDependencyState,
    TepExternalDependencyBoundaryState DocumentDependencyState,
    IReadOnlyList<ReferenceExchangeDependencyStateDto> DependencyStates,
    string? DeferredReason);
