using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class HrCaseManagementReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HrCaseManagementReadinessState HrCaseManagementReadinessState { get; set; }
    public HrCaseManagementReadinessState CaseIntakeBoundaryState { get; set; }
    public HrCaseManagementReadinessState CaseTriageBoundaryState { get; set; }
    public HrCaseManagementReadinessState InvestigationTrackingBoundaryState { get; set; }
    public HrCaseManagementReadinessState DisciplinaryActionBoundaryState { get; set; }
    public HrCaseManagementReadinessState ResolutionClosureBoundaryState { get; set; }
    public HrCaseManagementReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HrCaseManagementReadinessState EmployeeRecordDependencyState { get; set; }
    public HrCaseManagementReadinessState SensitiveAccessPolicyDependencyState { get; set; }
    public HrCaseManagementReadinessState DocumentDependencyState { get; set; }
    public HrCaseManagementReadinessState NotificationDependencyState { get; set; }
    public HrCaseManagementReadinessState ConsentPreconditionState { get; set; }
    public HrCaseManagementReadinessState DataMinimizationState { get; set; }
    public HrCaseManagementReadinessState RetentionPolicyState { get; set; }
    public HrCaseManagementReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HrCaseManagementReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HrCaseManagementReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
