using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class WorkforceAnalyticsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WorkforceAnalyticsReadinessState WorkforceAnalyticsReadinessState { get; set; }
    public WorkforceAnalyticsReadinessState AnalyticsCatalogBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState MetricBindingIntakeBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState AggregationScopeBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState VisibilityControlBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState AnalyticsReviewBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public WorkforceAnalyticsReadinessState TalentDataSourceDependencyState { get; set; }
    public WorkforceAnalyticsReadinessState ConsentPolicyDependencyState { get; set; }
    public WorkforceAnalyticsReadinessState DataGovernancePolicyDependencyState { get; set; }
    public WorkforceAnalyticsReadinessState NotificationDependencyState { get; set; }
    public WorkforceAnalyticsReadinessState ConsentPreconditionState { get; set; }
    public WorkforceAnalyticsReadinessState DataMinimizationState { get; set; }
    public WorkforceAnalyticsReadinessState RetentionPolicyState { get; set; }
    public WorkforceAnalyticsReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, WorkforceAnalyticsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long WorkforceAnalyticsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
