using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard;

public static class ReviewBoardMapper
{
    public static ReviewBoardCaseDto ToDto(TepReviewBoardCaseMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ReviewBoardCaseState,
            entity.ReviewDecisionState,
            entity.AssociationMembershipRegistryId,
            entity.ConsentVisibilityPolicyId,
            entity.VerifiedParticipantAccessId,
            entity.ReviewerEligibilityState,
            entity.SegregationOfDutiesState,
            entity.LegalSecurityDecisionState,
            entity.ExternalReviewBoardState,
            entity.AuditEvidenceState,
            entity.RetentionState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ReviewBoardVersion,
            entity.DeferredReason);

    public static ReviewBoardCaseListItemDto ToListItemDto(TepReviewBoardCaseMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ReviewBoardCaseState,
            entity.ReviewDecisionState,
            entity.ReviewerEligibilityState,
            entity.SegregationOfDutiesState,
            entity.LegalSecurityDecisionState,
            entity.LastEvaluatedAt,
            entity.ReviewBoardVersion);

    public static ReviewBoardDependencyStateDto ToDto(TepReviewBoardDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepReviewBoardDependencyState ToEntity(ReviewBoardDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
