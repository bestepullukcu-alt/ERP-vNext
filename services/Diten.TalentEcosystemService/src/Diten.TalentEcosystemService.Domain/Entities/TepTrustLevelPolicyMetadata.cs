using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepTrustLevelPolicyMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepTrustLevelPolicyState TrustLevelPolicyState { get; set; } = TepTrustLevelPolicyState.Draft;
    public TepTrustValidationState TrustValidationState { get; set; } = TepTrustValidationState.NotEvaluated;
    public TepMultiSignatureRequirementState MultiSignatureRequirementState { get; set; } = TepMultiSignatureRequirementState.NotRequired;
    public TepMultiSignaturePolicyUnavailableBehavior MultiSignaturePolicyUnavailableBehavior { get; set; } = TepMultiSignaturePolicyUnavailableBehavior.FailClosed;
    public Guid? AssociationMembershipRegistryId { get; set; }
    public Guid? ConsentVisibilityPolicyId { get; set; }
    public Guid? VerifiedParticipantAccessId { get; set; }
    public Guid? ReviewBoardCaseId { get; set; }
    public TepShellDependencyStatus AssociationValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus ConsentVisibilityValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus VerifiedAccessValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus ReviewBoardValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepSignatureSubstrateState SignatureSubstrateState { get; set; } = TepSignatureSubstrateState.Deferred;
    public string SignatureSubstrateReference { get; set; } = string.Empty;
    public TepTrustLegalSecurityState LegalSecurityTrustModelState { get; set; } = TepTrustLegalSecurityState.Deferred;
    public TepAuditEvidenceState AuditEvidenceState { get; set; } = TepAuditEvidenceState.Deferred;
    public TepRetentionState RetentionState { get; set; } = TepRetentionState.Deferred;
    public List<TepTrustLevelDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int TrustPolicyVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepTrustLevelDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
