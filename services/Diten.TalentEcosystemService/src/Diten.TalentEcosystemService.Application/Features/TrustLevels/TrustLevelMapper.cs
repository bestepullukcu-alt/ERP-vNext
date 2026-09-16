using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels;

public static class TrustLevelMapper
{
    public static TrustLevelPolicyDto ToDto(TepTrustLevelPolicyMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TrustLevelPolicyState,
            entity.TrustValidationState,
            entity.MultiSignatureRequirementState,
            entity.MultiSignaturePolicyUnavailableBehavior,
            entity.AssociationMembershipRegistryId,
            entity.ConsentVisibilityPolicyId,
            entity.VerifiedParticipantAccessId,
            entity.ReviewBoardCaseId,
            entity.AssociationValidationState,
            entity.ConsentVisibilityValidationState,
            entity.VerifiedAccessValidationState,
            entity.ReviewBoardValidationState,
            entity.SignatureSubstrateState,
            entity.SignatureSubstrateReference,
            entity.LegalSecurityTrustModelState,
            entity.AuditEvidenceState,
            entity.RetentionState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.TrustPolicyVersion,
            entity.DeferredReason);

    public static TrustLevelPolicyListItemDto ToListItemDto(TepTrustLevelPolicyMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TrustLevelPolicyState,
            entity.TrustValidationState,
            entity.MultiSignatureRequirementState,
            ToActivationState(entity),
            entity.LastEvaluatedAt,
            entity.TrustPolicyVersion);

    public static TrustLevelDependencyStateDto ToDto(TepTrustLevelDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepTrustLevelDependencyState ToEntity(TrustLevelDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };

    private static TepTrustActivationState ToActivationState(TepTrustLevelPolicyMetadata entity)
    {
        if (entity.IsDeleted)
        {
            return TepTrustActivationState.Archived;
        }

        if (entity.TrustLevelPolicyState is TepTrustLevelPolicyState.Active
            && entity.TrustValidationState is TepTrustValidationState.Approved
            && entity.MultiSignatureRequirementState is TepMultiSignatureRequirementState.NotRequired or TepMultiSignatureRequirementState.Approved)
        {
            return TepTrustActivationState.Active;
        }

        return entity.TrustValidationState is TepTrustValidationState.Deferred
            ? TepTrustActivationState.Deferred
            : TepTrustActivationState.Draft;
    }
}
