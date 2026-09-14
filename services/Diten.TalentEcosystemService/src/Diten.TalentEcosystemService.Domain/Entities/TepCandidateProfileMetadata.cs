using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepCandidateProfileMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CandidateReference { get; set; } = string.Empty;
    public string TalentProfileReference { get; set; } = string.Empty;
    public Guid? AssociationMembershipId { get; set; }
    public Guid? ConsentVisibilityPolicyId { get; set; }
    public Guid? VerifiedParticipantId { get; set; }
    public Guid? ReviewBoardCaseId { get; set; }
    public Guid? TrustLevelPolicyId { get; set; }
    public string HcmFoundationReference { get; set; } = string.Empty;
    public string SkillSummaryMetadata { get; set; } = string.Empty;
    public string CredentialSummaryMetadata { get; set; } = string.Empty;
    public string ExperienceSummaryMetadata { get; set; } = string.Empty;
    public TepCandidateIdentityState CandidateIdentityState { get; set; } = TepCandidateIdentityState.Draft;
    public TepTalentProfileState TalentProfileState { get; set; } = TepTalentProfileState.Draft;
    public TepProfileCompletenessState ProfileCompletenessState { get; set; } = TepProfileCompletenessState.Deferred;
    public TepCandidateVisibilityClassification VisibilityClassification { get; set; } = TepCandidateVisibilityClassification.Private;
    public TepCandidateConsentBasisState ConsentBasisState { get; set; } = TepCandidateConsentBasisState.Deferred;
    public TepPolicyEvaluationState PolicyEvaluationState { get; set; } = TepPolicyEvaluationState.Deferred;
    public TepPolicyEvaluationState ProfilePolicyEvaluationState { get; set; } = TepPolicyEvaluationState.Deferred;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Deferred;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepDataMinimizationState DataMinimizationState { get; set; } = TepDataMinimizationState.Deferred;
    public TepShellDependencyStatus AssociationValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus VerifiedAccessValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus ReviewBoardValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus TrustLevelValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus HcmValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public List<TepCandidateProfileDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int CandidateVersion { get; set; } = 1;
    public TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState { get; set; } = TepLocalAuditEvidenceRetentionState.Deferred;
    public string? DeferredReason { get; set; }
}

public sealed class TepCandidateProfileDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
