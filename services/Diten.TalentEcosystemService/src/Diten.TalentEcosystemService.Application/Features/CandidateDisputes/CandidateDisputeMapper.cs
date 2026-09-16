using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes;

public static class CandidateDisputeMapper
{
    public static CandidateDisputeReadinessDto ToDto(TepCandidateDisputeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DisputeReadinessState,
            entity.CandidateProfileReference,
            entity.ExitReferenceRecordReference,
            entity.ReferenceExchangeReference,
            entity.RehireRecommendationReference,
            entity.ResponseBoundaryState,
            entity.DisputeIntakeState,
            entity.DisputeReviewState,
            entity.ResolutionLifecycleState,
            entity.ContestabilityState,
            entity.HumanReviewState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.EvidenceRetentionState,
            entity.AuditReadinessState,
            entity.LegalHoldState,
            entity.DeletionPolicyState,
            entity.SelfServiceBoundaryState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.AutomatedDecisionBoundaryState,
            entity.MarketplaceBoundaryState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.DisputeReadinessVersion,
            entity.DeferredReason);

    public static CandidateDisputeReadinessListItemDto ToListItemDto(TepCandidateDisputeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DisputeReadinessState,
            entity.ResponseBoundaryState,
            entity.DisputeIntakeState,
            entity.DisputeReviewState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.LastEvaluatedAt,
            entity.DisputeReadinessVersion);

    public static CandidateDisputeDependencyStateDto ToDto(TepCandidateDisputeDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepCandidateDisputeDependencyState ToEntity(CandidateDisputeDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
