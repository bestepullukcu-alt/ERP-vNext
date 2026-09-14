using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange;

public static class ReferenceExchangeMapper
{
    public static ReferenceExchangeReadinessDto ToDto(TepReferenceExchangeMarketplaceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ExchangeReadinessState,
            entity.ExchangeAvailabilityState,
            entity.ParticipantEligibilityState,
            entity.AssociationMembershipReference,
            entity.VerifiedParticipantReference,
            entity.ConsentVisibilityPolicyReference,
            entity.CandidateProfileReference,
            entity.ExitReferenceRecordReference,
            entity.ReviewBoardCaseReference,
            entity.TrustLevelPolicyReference,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.MinimizationState,
            entity.LegalPrivacyBasisState,
            entity.EvidenceRetentionState,
            entity.AuditReadinessState,
            entity.AbuseControlState,
            entity.ThrottlingPolicyState,
            entity.ReviewDisputeBoundaryState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ReferenceExchangeVersion,
            entity.DeferredReason);

    public static ReferenceExchangeReadinessListItemDto ToListItemDto(TepReferenceExchangeMarketplaceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ExchangeReadinessState,
            entity.ExchangeAvailabilityState,
            entity.ParticipantEligibilityState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.MinimizationState,
            entity.LastEvaluatedAt,
            entity.ReferenceExchangeVersion);

    public static ReferenceExchangeDependencyStateDto ToDto(TepReferenceExchangeDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepReferenceExchangeDependencyState ToEntity(ReferenceExchangeDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
