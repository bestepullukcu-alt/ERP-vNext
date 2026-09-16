using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class EmploymentChangeReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EmploymentChangeReadinessState EmploymentChangeReadinessState { get; set; }
    public EmploymentChangeReadinessState ChangeLifecycleBoundaryState { get; set; }
    public EmploymentChangeReadinessState TransferBoundaryState { get; set; }
    public EmploymentChangeReadinessState PromotionBoundaryState { get; set; }
    public EmploymentChangeReadinessState ApprovalBoundaryState { get; set; }
    public EmploymentChangeReadinessState PositionAssignmentBoundaryState { get; set; }
    public EmploymentChangeReadinessState EmployeeActionBoundaryState { get; set; }
    public EmploymentChangeReadinessState ManagerActionBoundaryState { get; set; }
    public EmploymentChangeReadinessState CompensationDataBoundaryState { get; set; }
    public EmploymentChangeReadinessState BenefitsDataBoundaryState { get; set; }
    public EmploymentChangeReadinessState PayrollDataBoundaryState { get; set; }
    public EmploymentChangeReadinessState DocumentDependencyState { get; set; }
    public EmploymentChangeReadinessState NotificationDependencyState { get; set; }
    public EmploymentChangeReadinessState ConsentPreconditionState { get; set; }
    public EmploymentChangeReadinessState DataMinimizationState { get; set; }
    public EmploymentChangeReadinessState RetentionPolicyState { get; set; }
    public EmploymentChangeReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, EmploymentChangeReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long EmploymentChangeReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
