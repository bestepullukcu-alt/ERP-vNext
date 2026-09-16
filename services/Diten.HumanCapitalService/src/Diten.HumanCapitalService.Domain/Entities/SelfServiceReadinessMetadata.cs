using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class SelfServiceReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SelfServiceReadinessState SelfServiceReadinessState { get; set; }
    public SelfServiceReadinessState RequestIntakeBoundaryState { get; set; }
    public SelfServiceReadinessState ApprovalRoutingBoundaryState { get; set; }
    public SelfServiceReadinessState InboxDeliveryBoundaryState { get; set; }
    public SelfServiceReadinessState ProfileSelfUpdateBoundaryState { get; set; }
    public SelfServiceReadinessState DelegationScopeBoundaryState { get; set; }
    public SelfServiceReadinessState AutomatedDecisionBoundaryState { get; set; }
    public SelfServiceReadinessState IdentityDirectoryDependencyState { get; set; }
    public SelfServiceReadinessState HcmCapabilityDependencyState { get; set; }
    public SelfServiceReadinessState DocumentDependencyState { get; set; }
    public SelfServiceReadinessState NotificationDependencyState { get; set; }
    public SelfServiceReadinessState ConsentPreconditionState { get; set; }
    public SelfServiceReadinessState DataMinimizationState { get; set; }
    public SelfServiceReadinessState RetentionPolicyState { get; set; }
    public SelfServiceReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, SelfServiceReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long SelfServiceReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
