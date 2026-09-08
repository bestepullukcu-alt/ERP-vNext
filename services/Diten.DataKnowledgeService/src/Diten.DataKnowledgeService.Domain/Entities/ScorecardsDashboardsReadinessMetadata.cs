using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class ScorecardsDashboardsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ScorecardsDashboardsReadinessState ScorecardsDashboardsReadinessState { get; set; }
    public ScorecardsDashboardsReadinessState ScorecardCatalogBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState WidgetBindingIntakeBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState LayoutScopeBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState PublicationControlBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState DashboardReviewBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public ScorecardsDashboardsReadinessState MetricSemanticRegistrySourceDependencyState { get; set; }
    public ScorecardsDashboardsReadinessState DataWarehouseSourceDependencyState { get; set; }
    public ScorecardsDashboardsReadinessState DataContractRegistryDependencyState { get; set; }
    public ScorecardsDashboardsReadinessState NotificationDependencyState { get; set; }
    public ScorecardsDashboardsReadinessState StewardshipPreconditionState { get; set; }
    public ScorecardsDashboardsReadinessState DataMinimizationState { get; set; }
    public ScorecardsDashboardsReadinessState RetentionPolicyState { get; set; }
    public ScorecardsDashboardsReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, ScorecardsDashboardsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long ScorecardsDashboardsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
