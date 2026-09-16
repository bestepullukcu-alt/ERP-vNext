using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class HeadcountBudgetReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HeadcountBudgetReadinessState HeadcountBudgetReadinessState { get; set; }
    public HeadcountBudgetReadinessState HeadcountRequisitionBoundaryState { get; set; }
    public HeadcountBudgetReadinessState PositionBudgetBoundaryState { get; set; }
    public HeadcountBudgetReadinessState BudgetAllocationBoundaryState { get; set; }
    public HeadcountBudgetReadinessState BudgetApprovalBoundaryState { get; set; }
    public HeadcountBudgetReadinessState BudgetReconciliationBoundaryState { get; set; }
    public HeadcountBudgetReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HeadcountBudgetReadinessState OrganizationStructureDependencyState { get; set; }
    public HeadcountBudgetReadinessState PositionFrameworkDependencyState { get; set; }
    public HeadcountBudgetReadinessState DocumentDependencyState { get; set; }
    public HeadcountBudgetReadinessState NotificationDependencyState { get; set; }
    public HeadcountBudgetReadinessState ConsentPreconditionState { get; set; }
    public HeadcountBudgetReadinessState DataMinimizationState { get; set; }
    public HeadcountBudgetReadinessState RetentionPolicyState { get; set; }
    public HeadcountBudgetReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HeadcountBudgetReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HeadcountBudgetReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
