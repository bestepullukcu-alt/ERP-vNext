using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships;

public static class AssociationMembershipMapper
{
    public static AssociationMembershipRegistryDto ToDto(TepAssociationMembershipRegistry entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.AssociationMembershipState,
            entity.MemberCompanyState,
            entity.MemberCompanyReference,
            entity.HcmFoundationReference,
            entity.ConsentVisibilityPolicyId,
            entity.PolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.AssociationActivationState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.RegistryVersion);

    public static AssociationMembershipRegistryListItemDto ToListItemDto(TepAssociationMembershipRegistry entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.AssociationMembershipState,
            entity.MemberCompanyState,
            entity.PolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.AssociationActivationState,
            entity.LastEvaluatedAt,
            entity.RegistryVersion);

    public static TepAssociationDependencyStateDto ToDto(TepAssociationDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepAssociationDependencyState ToEntity(TepAssociationDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
