using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class EtlEltPipelinesReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EtlEltPipelinesReadinessState EtlEltPipelinesReadinessState { get; set; }
    public EtlEltPipelinesReadinessState PipelineDefinitionCatalogBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState ExtractIntakeBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState TransformScopeBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState LoadControlBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState OrchestrationReviewBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState AutomatedDecisionBoundaryState { get; set; }
    public EtlEltPipelinesReadinessState LakehouseSourceDependencyState { get; set; }
    public EtlEltPipelinesReadinessState JobOrchestrationDependencyState { get; set; }
    public EtlEltPipelinesReadinessState DataContractRegistryDependencyState { get; set; }
    public EtlEltPipelinesReadinessState NotificationDependencyState { get; set; }
    public EtlEltPipelinesReadinessState StewardshipPreconditionState { get; set; }
    public EtlEltPipelinesReadinessState DataMinimizationState { get; set; }
    public EtlEltPipelinesReadinessState RetentionPolicyState { get; set; }
    public EtlEltPipelinesReadinessState MonitoringPolicyState { get; set; }
    public Dictionary<string, EtlEltPipelinesReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long EtlEltPipelinesReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
