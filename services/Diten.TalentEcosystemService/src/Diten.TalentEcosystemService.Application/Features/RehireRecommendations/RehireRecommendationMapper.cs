using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations;

public static class RehireRecommendationMapper
{
    public static RehireRecommendationReadinessDto ToDto(TepRehireRecommendationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.RecommendationReadinessState,
            entity.RecommendationPolicyState,
            entity.RecommendationEvaluationState,
            entity.EligibilityPreconditionState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.MinimizationState,
            entity.ExplainabilityState,
            entity.HumanReviewState,
            entity.ContestabilityState,
            entity.CandidateResponseBoundaryState,
            entity.AbuseControlState,
            entity.MisuseDetectionState,
            entity.ThrottlingState,
            entity.EscalationState,
            entity.EvidenceRetentionState,
            entity.AuditReadinessState,
            entity.LegalHoldState,
            entity.DeletionPolicyState,
            entity.ReferenceExchangeReference,
            entity.ExitReferenceRecordReference,
            entity.CandidateProfileReference,
            entity.VerifiedParticipantReference,
            entity.AssociationMembershipReference,
            entity.ConsentVisibilityPolicyReference,
            entity.ReviewBoardCaseReference,
            entity.TrustLevelPolicyReference,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.RecommendationNetworkVersion,
            entity.DeferredReason);

    public static RehireRecommendationReadinessListItemDto ToListItemDto(TepRehireRecommendationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.RecommendationReadinessState,
            entity.RecommendationPolicyState,
            entity.RecommendationEvaluationState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.MinimizationState,
            entity.LastEvaluatedAt,
            entity.RecommendationNetworkVersion);

    public static RehireRecommendationDependencyStateDto ToDto(TepRehireRecommendationDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepRehireRecommendationDependencyState ToEntity(RehireRecommendationDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
