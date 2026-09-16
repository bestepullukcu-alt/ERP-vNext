using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class EarlyWarningSignalsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EarlyWarningSignalsReadinessState EarlyWarningSignalsReadinessState { get; set; }
    public EarlyWarningSignalsReadinessState SignalCatalogBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState PatternDetectionBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState CrossCompanyCorrelationBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState AlertRoutingBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState SignalReviewBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public EarlyWarningSignalsReadinessState TalentDataSourceDependencyState { get; set; }
    public EarlyWarningSignalsReadinessState RiskIndicatorSourceDependencyState { get; set; }
    public EarlyWarningSignalsReadinessState DocumentDependencyState { get; set; }
    public EarlyWarningSignalsReadinessState NotificationDependencyState { get; set; }
    public EarlyWarningSignalsReadinessState ConsentPreconditionState { get; set; }
    public EarlyWarningSignalsReadinessState DataMinimizationState { get; set; }
    public EarlyWarningSignalsReadinessState RetentionPolicyState { get; set; }
    public EarlyWarningSignalsReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, EarlyWarningSignalsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long EarlyWarningSignalsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
