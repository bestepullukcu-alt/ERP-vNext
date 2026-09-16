using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepVerifiedParticipantAccess : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? AssociationMembershipId { get; set; }
    public string MemberCompanyReference { get; set; } = string.Empty;
    public string HrParticipantReference { get; set; } = string.Empty;
    public string HcmFoundationReference { get; set; } = string.Empty;
    public Guid? ConsentVisibilityPolicyId { get; set; }
    public TepVerificationState VerificationState { get; set; } = TepVerificationState.Draft;
    public TepAccessState AccessState { get; set; } = TepAccessState.Draft;
    public TepPolicyEvaluationState PolicyEvaluationState { get; set; } = TepPolicyEvaluationState.NotEvaluated;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Pending;
    public TepShellDependencyStatus HcmValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus AssociationValidationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepVerifiedCompanyAccessState VerifiedCompanyAccessState { get; set; } = TepVerifiedCompanyAccessState.LocalMetadata;
    public List<TepVerifiedParticipantDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int VerificationVersion { get; set; } = 1;
    public TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState { get; set; } = TepLocalAuditEvidenceRetentionState.Deferred;
    public string? DeferredReason { get; set; }
}

public sealed class TepVerifiedParticipantDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
