using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepConsentVisibilityPolicy : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepPolicyState PolicyState { get; set; } = TepPolicyState.Draft;
    public TepConsentRequirementState ConsentRequirementState { get; set; } = TepConsentRequirementState.Deferred;
    public TepVisibilityScope VisibilityScope { get; set; } = TepVisibilityScope.InternalOnly;
    public TepDataScopeState DataScopeState { get; set; } = TepDataScopeState.Deferred;
    public TepAccessPolicyState AccessPolicyState { get; set; } = TepAccessPolicyState.Draft;
    public TepAssociationConsumptionState AssociationConsumptionState { get; set; } = TepAssociationConsumptionState.Draft;
    public TepPolicyUnavailableBehavior PolicyUnavailableBehavior { get; set; } = TepPolicyUnavailableBehavior.FailClosed;
    public string SourceContractVersion { get; set; } = string.Empty;
    public List<TepPolicyDependencyState> DependencyStates { get; set; } = [];
    public TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState { get; set; } = TepLocalAuditEvidenceRetentionState.Deferred;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int PolicyVersion { get; set; } = 1;
}

public sealed class TepPolicyDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
