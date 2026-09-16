using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

internal static class ReferenceExchangeHandlerMapper
{
    public static TepReferenceExchangeMarketplaceReadinessMetadata ToEntity(Guid tenantId, ReferenceExchangeReadinessRequest request) =>
        new()
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            ExchangeReadinessState = request.ExchangeReadinessState,
            ExchangeAvailabilityState = request.ExchangeAvailabilityState,
            ParticipantEligibilityState = request.ParticipantEligibilityState,
            AssociationMembershipReference = request.AssociationMembershipReference,
            VerifiedParticipantReference = request.VerifiedParticipantReference,
            ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference,
            CandidateProfileReference = request.CandidateProfileReference,
            ExitReferenceRecordReference = request.ExitReferenceRecordReference,
            ReviewBoardCaseReference = request.ReviewBoardCaseReference,
            TrustLevelPolicyReference = request.TrustLevelPolicyReference,
            ConsentPreconditionState = request.ConsentPreconditionState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            MinimizationState = request.MinimizationState,
            LegalPrivacyBasisState = request.LegalPrivacyBasisState,
            EvidenceRetentionState = request.EvidenceRetentionState,
            AuditReadinessState = request.AuditReadinessState,
            AbuseControlState = request.AbuseControlState,
            ThrottlingPolicyState = request.ThrottlingPolicyState,
            ReviewDisputeBoundaryState = request.ReviewDisputeBoundaryState,
            NotificationDependencyState = request.NotificationDependencyState,
            DocumentDependencyState = request.DocumentDependencyState,
            DependencyStates = request.DependencyStates.Select(ReferenceExchangeMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            ReferenceExchangeVersion = request.ReferenceExchangeVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };

    public static void Apply(TepReferenceExchangeMarketplaceReadinessMetadata entity, ReferenceExchangeReadinessRequest request)
    {
        entity.Code = request.Code.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.ExchangeReadinessState = request.ExchangeReadinessState;
        entity.ExchangeAvailabilityState = request.ExchangeAvailabilityState;
        entity.ParticipantEligibilityState = request.ParticipantEligibilityState;
        entity.AssociationMembershipReference = request.AssociationMembershipReference;
        entity.VerifiedParticipantReference = request.VerifiedParticipantReference;
        entity.ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference;
        entity.CandidateProfileReference = request.CandidateProfileReference;
        entity.ExitReferenceRecordReference = request.ExitReferenceRecordReference;
        entity.ReviewBoardCaseReference = request.ReviewBoardCaseReference;
        entity.TrustLevelPolicyReference = request.TrustLevelPolicyReference;
        entity.ConsentPreconditionState = request.ConsentPreconditionState;
        entity.VisibilityApprovalState = request.VisibilityApprovalState;
        entity.DataScopeState = request.DataScopeState;
        entity.MinimizationState = request.MinimizationState;
        entity.LegalPrivacyBasisState = request.LegalPrivacyBasisState;
        entity.EvidenceRetentionState = request.EvidenceRetentionState;
        entity.AuditReadinessState = request.AuditReadinessState;
        entity.AbuseControlState = request.AbuseControlState;
        entity.ThrottlingPolicyState = request.ThrottlingPolicyState;
        entity.ReviewDisputeBoundaryState = request.ReviewDisputeBoundaryState;
        entity.NotificationDependencyState = request.NotificationDependencyState;
        entity.DocumentDependencyState = request.DocumentDependencyState;
        entity.DependencyStates = request.DependencyStates.Select(ReferenceExchangeMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.LastEvaluatedAt;
        entity.ReferenceExchangeVersion = request.ReferenceExchangeVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
