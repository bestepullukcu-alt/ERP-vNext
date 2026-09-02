using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Handlers;

internal static class RehireRecommendationHandlerMapper
{
    public static TepRehireRecommendationReadinessMetadata ToEntity(Guid tenantId, RehireRecommendationReadinessRequest request) =>
        new()
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            RecommendationReadinessState = request.RecommendationReadinessState,
            RecommendationPolicyState = request.RecommendationPolicyState,
            RecommendationEvaluationState = request.RecommendationEvaluationState,
            EligibilityPreconditionState = request.EligibilityPreconditionState,
            ConsentPreconditionState = request.ConsentPreconditionState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            MinimizationState = request.MinimizationState,
            ExplainabilityState = request.ExplainabilityState,
            HumanReviewState = request.HumanReviewState,
            ContestabilityState = request.ContestabilityState,
            CandidateResponseBoundaryState = request.CandidateResponseBoundaryState,
            AbuseControlState = request.AbuseControlState,
            MisuseDetectionState = request.MisuseDetectionState,
            ThrottlingState = request.ThrottlingState,
            EscalationState = request.EscalationState,
            EvidenceRetentionState = request.EvidenceRetentionState,
            AuditReadinessState = request.AuditReadinessState,
            LegalHoldState = request.LegalHoldState,
            DeletionPolicyState = request.DeletionPolicyState,
            ReferenceExchangeReference = request.ReferenceExchangeReference,
            ExitReferenceRecordReference = request.ExitReferenceRecordReference,
            CandidateProfileReference = request.CandidateProfileReference,
            VerifiedParticipantReference = request.VerifiedParticipantReference,
            AssociationMembershipReference = request.AssociationMembershipReference,
            ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference,
            ReviewBoardCaseReference = request.ReviewBoardCaseReference,
            TrustLevelPolicyReference = request.TrustLevelPolicyReference,
            DependencyStates = request.DependencyStates.Select(RehireRecommendationMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            RecommendationNetworkVersion = request.RecommendationNetworkVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };

    public static void Apply(TepRehireRecommendationReadinessMetadata entity, RehireRecommendationReadinessRequest request)
    {
        entity.Code = request.Code.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.RecommendationReadinessState = request.RecommendationReadinessState;
        entity.RecommendationPolicyState = request.RecommendationPolicyState;
        entity.RecommendationEvaluationState = request.RecommendationEvaluationState;
        entity.EligibilityPreconditionState = request.EligibilityPreconditionState;
        entity.ConsentPreconditionState = request.ConsentPreconditionState;
        entity.VisibilityApprovalState = request.VisibilityApprovalState;
        entity.DataScopeState = request.DataScopeState;
        entity.MinimizationState = request.MinimizationState;
        entity.ExplainabilityState = request.ExplainabilityState;
        entity.HumanReviewState = request.HumanReviewState;
        entity.ContestabilityState = request.ContestabilityState;
        entity.CandidateResponseBoundaryState = request.CandidateResponseBoundaryState;
        entity.AbuseControlState = request.AbuseControlState;
        entity.MisuseDetectionState = request.MisuseDetectionState;
        entity.ThrottlingState = request.ThrottlingState;
        entity.EscalationState = request.EscalationState;
        entity.EvidenceRetentionState = request.EvidenceRetentionState;
        entity.AuditReadinessState = request.AuditReadinessState;
        entity.LegalHoldState = request.LegalHoldState;
        entity.DeletionPolicyState = request.DeletionPolicyState;
        entity.ReferenceExchangeReference = request.ReferenceExchangeReference;
        entity.ExitReferenceRecordReference = request.ExitReferenceRecordReference;
        entity.CandidateProfileReference = request.CandidateProfileReference;
        entity.VerifiedParticipantReference = request.VerifiedParticipantReference;
        entity.AssociationMembershipReference = request.AssociationMembershipReference;
        entity.ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference;
        entity.ReviewBoardCaseReference = request.ReviewBoardCaseReference;
        entity.TrustLevelPolicyReference = request.TrustLevelPolicyReference;
        entity.DependencyStates = request.DependencyStates.Select(RehireRecommendationMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.LastEvaluatedAt;
        entity.RecommendationNetworkVersion = request.RecommendationNetworkVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
