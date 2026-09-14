using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;

public static class ExitReferenceRecordMapper
{
    public static ExitReferenceRecordDto ToDto(TepExitReferenceRecordMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CandidateProfileReference,
            entity.OffboardingCaseReference,
            entity.VerifiedParticipantReference,
            entity.AssociationMembershipReference,
            entity.ConsentVisibilityPolicyReference,
            entity.ReviewBoardCaseReference,
            entity.TrustLevelPolicyReference,
            entity.ReferenceRecordState,
            entity.ReferenceSharingState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.EvidenceRetentionState,
            entity.ReviewDisputeBoundaryState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ReferenceRecordVersion,
            entity.DeferredReason);

    public static ExitReferenceRecordListItemDto ToListItemDto(TepExitReferenceRecordMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ReferenceRecordState,
            entity.ReferenceSharingState,
            entity.ConsentPreconditionState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.EvidenceRetentionState,
            entity.ReviewDisputeBoundaryState,
            entity.LastEvaluatedAt,
            entity.ReferenceRecordVersion);

    public static ExitReferenceDependencyStateDto ToDto(TepExitReferenceDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepExitReferenceDependencyState ToEntity(ExitReferenceDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
