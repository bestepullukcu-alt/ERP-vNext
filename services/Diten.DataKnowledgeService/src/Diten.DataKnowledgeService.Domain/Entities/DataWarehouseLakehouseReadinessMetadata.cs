using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class DataWarehouseLakehouseReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DataWarehouseLakehouseReadinessState DataWarehouseLakehouseReadinessState { get; set; }
    public DataWarehouseLakehouseReadinessState StorageLayerCatalogBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState IngestionIntakeBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState PartitioningScopeBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState LineageControlBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState WarehouseReviewBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState AutomatedDecisionBoundaryState { get; set; }
    public DataWarehouseLakehouseReadinessState VaultDependencyState { get; set; }
    public DataWarehouseLakehouseReadinessState LoggingMonitoringDependencyState { get; set; }
    public DataWarehouseLakehouseReadinessState DataContractRegistryDependencyState { get; set; }
    public DataWarehouseLakehouseReadinessState NotificationDependencyState { get; set; }
    public DataWarehouseLakehouseReadinessState StewardshipPreconditionState { get; set; }
    public DataWarehouseLakehouseReadinessState DataMinimizationState { get; set; }
    public DataWarehouseLakehouseReadinessState RetentionPolicyState { get; set; }
    public DataWarehouseLakehouseReadinessState StorageTierPolicyState { get; set; }
    public Dictionary<string, DataWarehouseLakehouseReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long DataWarehouseLakehouseReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
