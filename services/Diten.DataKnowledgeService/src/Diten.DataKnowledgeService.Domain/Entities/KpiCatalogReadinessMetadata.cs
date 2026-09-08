using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class KpiCatalogReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public KpiCatalogReadinessState KpiCatalogReadinessState { get; set; }
    public KpiCatalogReadinessState KpiIdentityCatalogBoundaryState { get; set; }
    public KpiCatalogReadinessState DefinitionBindingIntakeBoundaryState { get; set; }
    public KpiCatalogReadinessState OwnershipScopeBoundaryState { get; set; }
    public KpiCatalogReadinessState PublicationControlBoundaryState { get; set; }
    public KpiCatalogReadinessState CatalogReviewBoundaryState { get; set; }
    public KpiCatalogReadinessState AutomatedDecisionBoundaryState { get; set; }
    public KpiCatalogReadinessState MetricSemanticRegistrySourceDependencyState { get; set; }
    public KpiCatalogReadinessState DataDictionaryDependencyState { get; set; }
    public KpiCatalogReadinessState DataContractRegistryDependencyState { get; set; }
    public KpiCatalogReadinessState NotificationDependencyState { get; set; }
    public KpiCatalogReadinessState StewardshipPreconditionState { get; set; }
    public KpiCatalogReadinessState DataMinimizationState { get; set; }
    public KpiCatalogReadinessState RetentionPolicyState { get; set; }
    public KpiCatalogReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, KpiCatalogReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long KpiCatalogReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
