using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepAssociationMembershipRegistry : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepAssociationMembershipState AssociationMembershipState { get; set; } = TepAssociationMembershipState.Draft;
    public TepMemberCompanyState MemberCompanyState { get; set; } = TepMemberCompanyState.Draft;
    public string MemberCompanyReference { get; set; } = string.Empty;
    public string HcmFoundationReference { get; set; } = string.Empty;
    public Guid? ConsentVisibilityPolicyId { get; set; }
    public TepPolicyEvaluationState PolicyEvaluationState { get; set; } = TepPolicyEvaluationState.NotEvaluated;
    public TepVisibilityApprovalState VisibilityApprovalState { get; set; } = TepVisibilityApprovalState.Pending;
    public TepAssociationActivationState AssociationActivationState { get; set; } = TepAssociationActivationState.Draft;
    public List<TepAssociationDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int RegistryVersion { get; set; } = 1;
}

public sealed class TepAssociationDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
