using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles;

public static class CandidateProfileMapper
{
    public static CandidateProfileDto ToDto(TepCandidateProfileMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CandidateReference,
            entity.TalentProfileReference,
            entity.AssociationMembershipId,
            entity.ConsentVisibilityPolicyId,
            entity.VerifiedParticipantId,
            entity.ReviewBoardCaseId,
            entity.TrustLevelPolicyId,
            entity.HcmFoundationReference,
            entity.SkillSummaryMetadata,
            entity.CredentialSummaryMetadata,
            entity.ExperienceSummaryMetadata,
            entity.CandidateIdentityState,
            entity.TalentProfileState,
            entity.ProfileCompletenessState,
            entity.VisibilityClassification,
            entity.ConsentBasisState,
            entity.PolicyEvaluationState,
            entity.ProfilePolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.DataMinimizationState,
            entity.AssociationValidationState,
            entity.VerifiedAccessValidationState,
            entity.ReviewBoardValidationState,
            entity.TrustLevelValidationState,
            entity.HcmValidationState,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.CandidateVersion,
            entity.LocalAuditEvidenceRetentionState,
            entity.DeferredReason);

    public static CandidateProfileListItemDto ToListItemDto(TepCandidateProfileMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CandidateIdentityState,
            entity.TalentProfileState,
            entity.ProfileCompletenessState,
            entity.VisibilityClassification,
            entity.ConsentBasisState,
            entity.PolicyEvaluationState,
            entity.ProfilePolicyEvaluationState,
            entity.VisibilityApprovalState,
            entity.DataScopeState,
            entity.DataMinimizationState,
            entity.LastEvaluatedAt,
            entity.CandidateVersion);

    public static CandidateProfileDependencyStateDto ToDto(TepCandidateProfileDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepCandidateProfileDependencyState ToEntity(CandidateProfileDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
