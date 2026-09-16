using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles;

public sealed record CandidateProfileDto(
    Guid Id,
    string Code,
    string DisplayName,
    string CandidateReference,
    string TalentProfileReference,
    Guid? AssociationMembershipId,
    Guid? ConsentVisibilityPolicyId,
    Guid? VerifiedParticipantId,
    Guid? ReviewBoardCaseId,
    Guid? TrustLevelPolicyId,
    string HcmFoundationReference,
    string SkillSummaryMetadata,
    string CredentialSummaryMetadata,
    string ExperienceSummaryMetadata,
    TepCandidateIdentityState CandidateIdentityState,
    TepTalentProfileState TalentProfileState,
    TepProfileCompletenessState ProfileCompletenessState,
    TepCandidateVisibilityClassification VisibilityClassification,
    TepCandidateConsentBasisState ConsentBasisState,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepPolicyEvaluationState ProfilePolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState DataMinimizationState,
    TepShellDependencyStatus AssociationValidationState,
    TepShellDependencyStatus VerifiedAccessValidationState,
    TepShellDependencyStatus ReviewBoardValidationState,
    TepShellDependencyStatus TrustLevelValidationState,
    TepShellDependencyStatus HcmValidationState,
    IReadOnlyList<CandidateProfileDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int CandidateVersion,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    string? DeferredReason);

public sealed record CandidateProfileListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepCandidateIdentityState CandidateIdentityState,
    TepTalentProfileState TalentProfileState,
    TepProfileCompletenessState ProfileCompletenessState,
    TepCandidateVisibilityClassification VisibilityClassification,
    TepCandidateConsentBasisState ConsentBasisState,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepPolicyEvaluationState ProfilePolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepDataScopeState DataScopeState,
    TepDataMinimizationState DataMinimizationState,
    DateTimeOffset? LastEvaluatedAt,
    int CandidateVersion);

public sealed record CandidateProfileDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record CandidateProfileEvaluationDto(
    Guid Id,
    bool ActivationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record CandidateProfileAuditMetadataDto(
    Guid Id,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    IReadOnlyList<CandidateProfileDependencyStateDto> DependencyStates,
    string? DeferredReason);
