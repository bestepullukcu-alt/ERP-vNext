using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels;

public sealed record TrustLevelPolicyRequest(
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

public sealed record EvaluateTrustLevelPolicyRequest(bool TrustElevationRequested);
