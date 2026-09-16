using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels;

public sealed record TrustLevelPolicyDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepTrustLevelPolicyState TrustLevelPolicyState,
    TepTrustValidationState TrustValidationState,
    TepMultiSignatureRequirementState MultiSignatureRequirementState,
    TepMultiSignaturePolicyUnavailableBehavior MultiSignaturePolicyUnavailableBehavior,
    Guid? AssociationMembershipRegistryId,
    Guid? ConsentVisibilityPolicyId,
    Guid? VerifiedParticipantAccessId,
    Guid? ReviewBoardCaseId,
    TepShellDependencyStatus AssociationValidationState,
    TepShellDependencyStatus ConsentVisibilityValidationState,
    TepShellDependencyStatus VerifiedAccessValidationState,
    TepShellDependencyStatus ReviewBoardValidationState,
    TepSignatureSubstrateState SignatureSubstrateState,
    string SignatureSubstrateReference,
    TepTrustLegalSecurityState LegalSecurityTrustModelState,
    TepAuditEvidenceState AuditEvidenceState,
    TepRetentionState RetentionState,
    IReadOnlyList<TrustLevelDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int TrustPolicyVersion,
    string? DeferredReason);

public sealed record TrustLevelPolicyListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepTrustLevelPolicyState TrustLevelPolicyState,
    TepTrustValidationState TrustValidationState,
    TepMultiSignatureRequirementState MultiSignatureRequirementState,
    TepTrustActivationState ActivationState,
    DateTimeOffset? LastEvaluatedAt,
    int TrustPolicyVersion);

public sealed record TrustLevelDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record TrustLevelEvaluationDto(
    Guid Id,
    bool TrustElevationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record TrustLevelAuditMetadataDto(
    Guid Id,
    TepSignatureSubstrateState SignatureSubstrateState,
    string SignatureSubstrateReference,
    TepAuditEvidenceState AuditEvidenceState,
    TepRetentionState RetentionState,
    IReadOnlyList<TrustLevelDependencyStateDto> DependencyStates,
    string? DeferredReason);
