using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class HrComplianceReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HrComplianceReadinessState HrComplianceReadinessState { get; set; }
    public HrComplianceReadinessState ObligationCatalogBoundaryState { get; set; }
    public HrComplianceReadinessState ControlMappingBoundaryState { get; set; }
    public HrComplianceReadinessState StatutoryReportDefinitionBoundaryState { get; set; }
    public HrComplianceReadinessState FilingScheduleBoundaryState { get; set; }
    public HrComplianceReadinessState AttestationClosureBoundaryState { get; set; }
    public HrComplianceReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HrComplianceReadinessState RegulatorySourceDependencyState { get; set; }
    public HrComplianceReadinessState HcmDataSourceDependencyState { get; set; }
    public HrComplianceReadinessState DocumentDependencyState { get; set; }
    public HrComplianceReadinessState NotificationDependencyState { get; set; }
    public HrComplianceReadinessState ConsentPreconditionState { get; set; }
    public HrComplianceReadinessState DataMinimizationState { get; set; }
    public HrComplianceReadinessState RetentionPolicyState { get; set; }
    public HrComplianceReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HrComplianceReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HrComplianceReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
