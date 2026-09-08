using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class MetricDefinitionsOwnershipReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MetricDefinitionsOwnershipReadinessState MetricDefinitionsOwnershipReadinessState { get; set; }
    public MetricDefinitionsOwnershipReadinessState DefinitionCatalogBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState OwnershipAssignmentIntakeBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState StewardshipScopeBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState ApprovalControlBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState DefinitionReviewBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState AutomatedDecisionBoundaryState { get; set; }
    public MetricDefinitionsOwnershipReadinessState MetricSemanticRegistrySourceDependencyState { get; set; }
    public MetricDefinitionsOwnershipReadinessState KpiCatalogSourceDependencyState { get; set; }
    public MetricDefinitionsOwnershipReadinessState DataContractRegistryDependencyState { get; set; }
    public MetricDefinitionsOwnershipReadinessState NotificationDependencyState { get; set; }
    public MetricDefinitionsOwnershipReadinessState StewardshipPreconditionState { get; set; }
    public MetricDefinitionsOwnershipReadinessState DataMinimizationState { get; set; }
    public MetricDefinitionsOwnershipReadinessState RetentionPolicyState { get; set; }
    public MetricDefinitionsOwnershipReadinessState ApprovalPolicyState { get; set; }
    public Dictionary<string, MetricDefinitionsOwnershipReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long MetricDefinitionsOwnershipReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
