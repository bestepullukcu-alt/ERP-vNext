using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class HrKpiAnalyticsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HrKpiAnalyticsReadinessState HrKpiAnalyticsReadinessState { get; set; }
    public HrKpiAnalyticsReadinessState KpiCatalogBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState MetricDefinitionBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState DashboardBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState AnalyticsQueryBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState DataExportBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HrKpiAnalyticsReadinessState AnalyticsPlatformDependencyState { get; set; }
    public HrKpiAnalyticsReadinessState DataSourceDependencyState { get; set; }
    public HrKpiAnalyticsReadinessState DocumentDependencyState { get; set; }
    public HrKpiAnalyticsReadinessState NotificationDependencyState { get; set; }
    public HrKpiAnalyticsReadinessState ConsentPreconditionState { get; set; }
    public HrKpiAnalyticsReadinessState DataMinimizationState { get; set; }
    public HrKpiAnalyticsReadinessState RetentionPolicyState { get; set; }
    public HrKpiAnalyticsReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HrKpiAnalyticsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HrKpiAnalyticsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
