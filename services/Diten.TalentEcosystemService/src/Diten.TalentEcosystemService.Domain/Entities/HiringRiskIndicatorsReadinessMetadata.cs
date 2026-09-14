using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class HiringRiskIndicatorsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HiringRiskIndicatorsReadinessState HiringRiskIndicatorsReadinessState { get; set; }
    public HiringRiskIndicatorsReadinessState RiskIndicatorCatalogBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState RiskSignalIntakeBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState RiskAssessmentBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState MitigationTrackingBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState IndicatorReviewBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HiringRiskIndicatorsReadinessState TalentDataSourceDependencyState { get; set; }
    public HiringRiskIndicatorsReadinessState ConsentPolicyDependencyState { get; set; }
    public HiringRiskIndicatorsReadinessState DocumentDependencyState { get; set; }
    public HiringRiskIndicatorsReadinessState NotificationDependencyState { get; set; }
    public HiringRiskIndicatorsReadinessState ConsentPreconditionState { get; set; }
    public HiringRiskIndicatorsReadinessState DataMinimizationState { get; set; }
    public HiringRiskIndicatorsReadinessState RetentionPolicyState { get; set; }
    public HiringRiskIndicatorsReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HiringRiskIndicatorsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HiringRiskIndicatorsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
