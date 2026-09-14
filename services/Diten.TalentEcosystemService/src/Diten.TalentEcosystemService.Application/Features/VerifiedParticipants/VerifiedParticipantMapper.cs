using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;

public static class VerifiedParticipantMapper
{
    public static VerifiedParticipantAccessDto ToDto(TepVerifiedParticipantAccess entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.AssociationMembershipId,
            entity.MemberCompanyReference,
            entity.HrParticipantReference,
            entity.HcmFoundationReference,
            entity.ConsentVisibilityPolicyId,
            entity.VerificationState,
            entity.AccessState,
            entity.PolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.HcmValidationState,
            entity.AssociationValidationState,
            entity.VerifiedCompanyAccessState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.VerificationVersion,
            entity.LocalAuditEvidenceRetentionState,
            entity.DeferredReason);

    public static VerifiedParticipantAccessListItemDto ToListItemDto(TepVerifiedParticipantAccess entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.VerificationState,
            entity.AccessState,
            entity.PolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.HcmValidationState,
            entity.AssociationValidationState,
            entity.VerifiedCompanyAccessState,
            entity.LastEvaluatedAt,
            entity.VerificationVersion);

    public static TepVerifiedParticipantDependencyStateDto ToDto(TepVerifiedParticipantDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepVerifiedParticipantDependencyState ToEntity(TepVerifiedParticipantDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
