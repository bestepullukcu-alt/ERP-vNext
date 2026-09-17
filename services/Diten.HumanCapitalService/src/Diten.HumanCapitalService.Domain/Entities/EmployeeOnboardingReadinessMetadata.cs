using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class EmployeeOnboardingReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EmployeeOnboardingReadinessState OnboardingReadinessState { get; set; }
    public EmployeeOnboardingReadinessState LifecycleBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState ChecklistBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState ManagerActionBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState EmployeeActionBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState CandidateTransitionBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState IdentityProvisioningBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState AccessProvisioningBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState DeviceEquipmentProvisioningBoundaryState { get; set; }
    public EmployeeOnboardingReadinessState DocumentDependencyState { get; set; }
    public EmployeeOnboardingReadinessState NotificationDependencyState { get; set; }
    public EmployeeOnboardingReadinessState ConsentPreconditionState { get; set; }
    public EmployeeOnboardingReadinessState DataMinimizationState { get; set; }
    public EmployeeOnboardingReadinessState RetentionPolicyState { get; set; }
    public EmployeeOnboardingReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, EmployeeOnboardingReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long OnboardingReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
