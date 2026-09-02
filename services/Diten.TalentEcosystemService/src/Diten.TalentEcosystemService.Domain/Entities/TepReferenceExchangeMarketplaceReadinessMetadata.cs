using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepReferenceExchangeMarketplaceReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepReferenceExchangeReadinessState ExchangeReadinessState { get; set; } = TepReferenceExchangeReadinessState.Deferred;
    public TepReferenceExchangeAvailabilityState ExchangeAvailabilityState { get; set; } = TepReferenceExchangeAvailabilityState.Deferred;
    public TepParticipantEligibilityState ParticipantEligibilityState { get; set; } = TepParticipantEligibilityState.Deferred;
    public Guid? AssociationMembershipReference { get; set; }
    public Guid? VerifiedParticipantReference { get; set; }
    public Guid? ConsentVisibilityPolicyReference { get; set; }
    public Guid? CandidateProfileReference { get; set; }
    public Guid? ExitReferenceRecordReference { get; set; }
    public Guid? ReviewBoardCaseReference { get; set; }
    public Guid? TrustLevelPolicyReference { get; set; }
    public TepConsentRequirementState ConsentPreconditionState { get; set; } = TepConsentRequirementState.Deferred;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Deferred;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepDataMinimizationState MinimizationState { get; set; } = TepDataMinimizationState.Deferred;
    public TepLegalPrivacyBasisState LegalPrivacyBasisState { get; set; } = TepLegalPrivacyBasisState.Deferred;
    public TepEvidenceRetentionDecisionState EvidenceRetentionState { get; set; } = TepEvidenceRetentionDecisionState.Deferred;
    public TepAuditReadinessState AuditReadinessState { get; set; } = TepAuditReadinessState.Deferred;
    public TepAbuseControlState AbuseControlState { get; set; } = TepAbuseControlState.Deferred;
    public TepThrottlingPolicyState ThrottlingPolicyState { get; set; } = TepThrottlingPolicyState.Deferred;
    public TepReviewDisputeBoundaryState ReviewDisputeBoundaryState { get; set; } = TepReviewDisputeBoundaryState.Deferred;
    public TepExternalDependencyBoundaryState NotificationDependencyState { get; set; } = TepExternalDependencyBoundaryState.Deferred;
    public TepExternalDependencyBoundaryState DocumentDependencyState { get; set; } = TepExternalDependencyBoundaryState.Deferred;
    public List<TepReferenceExchangeDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int ReferenceExchangeVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepReferenceExchangeDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
