using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;

public static class ConsentVisibilityPolicyMapper
{
    public static ConsentVisibilityPolicyDto ToDto(TepConsentVisibilityPolicy entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PolicyState,
            entity.ConsentRequirementState,
            entity.VisibilityScope,
            entity.DataScopeState,
            entity.AccessPolicyState,
            entity.AssociationConsumptionState,
            entity.PolicyUnavailableBehavior,
            entity.SourceContractVersion,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.LocalAuditEvidenceRetentionState,
            entity.LastEvaluatedAt,
            entity.PolicyVersion);

    public static ConsentVisibilityPolicyListItemDto ToListItemDto(TepConsentVisibilityPolicy entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PolicyState,
            entity.ConsentRequirementState,
            entity.VisibilityScope,
            entity.DataScopeState,
            entity.AssociationConsumptionState,
            entity.LastEvaluatedAt,
            entity.PolicyVersion);

    public static TepPolicyDependencyStateDto ToDto(TepPolicyDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepPolicyDependencyState ToEntity(TepPolicyDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
