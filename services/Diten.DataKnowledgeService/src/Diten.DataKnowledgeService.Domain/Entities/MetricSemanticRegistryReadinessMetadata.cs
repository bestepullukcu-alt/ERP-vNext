using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class MetricSemanticRegistryReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MetricSemanticRegistryReadinessState MetricSemanticRegistryReadinessState { get; set; }
    public MetricSemanticRegistryReadinessState MetricIdentityCatalogBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState SemanticEntityIntakeBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState DimensionMeasureScopeBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState SemanticBindingControlBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState RegistryReviewBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState AutomatedDecisionBoundaryState { get; set; }
    public MetricSemanticRegistryReadinessState DataSourceRegistryDependencyState { get; set; }
    public MetricSemanticRegistryReadinessState DataGovernancePolicyDependencyState { get; set; }
    public MetricSemanticRegistryReadinessState SemanticContractSourceDependencyState { get; set; }
    public MetricSemanticRegistryReadinessState NotificationDependencyState { get; set; }
    public MetricSemanticRegistryReadinessState StewardshipPreconditionState { get; set; }
    public MetricSemanticRegistryReadinessState DataMinimizationState { get; set; }
    public MetricSemanticRegistryReadinessState RetentionPolicyState { get; set; }
    public MetricSemanticRegistryReadinessState VersioningPolicyState { get; set; }
    public Dictionary<string, MetricSemanticRegistryReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long MetricSemanticRegistryReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
