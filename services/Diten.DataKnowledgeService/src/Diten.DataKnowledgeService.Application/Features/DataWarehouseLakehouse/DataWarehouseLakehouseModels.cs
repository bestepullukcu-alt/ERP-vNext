using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse;

public class DataWarehouseLakehouseReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DataWarehouseLakehouseReadinessState DataWarehouseLakehouseReadinessState { get; init; } = DataWarehouseLakehouseReadinessState.Draft;
    public DataWarehouseLakehouseReadinessState StorageLayerCatalogBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState IngestionIntakeBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState PartitioningScopeBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState LineageControlBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState WarehouseReviewBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState AutomatedDecisionBoundaryState { get; init; } = DataWarehouseLakehouseReadinessState.Blocked;
    public DataWarehouseLakehouseReadinessState VaultDependencyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState LoggingMonitoringDependencyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState DataContractRegistryDependencyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState NotificationDependencyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState StewardshipPreconditionState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState DataMinimizationState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState RetentionPolicyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public DataWarehouseLakehouseReadinessState StorageTierPolicyState { get; init; } = DataWarehouseLakehouseReadinessState.Deferred;
    public IReadOnlyDictionary<string, DataWarehouseLakehouseReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, DataWarehouseLakehouseReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long DataWarehouseLakehouseReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record DataWarehouseLakehouseReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    DataWarehouseLakehouseReadinessState DataWarehouseLakehouseReadinessState,
    DataWarehouseLakehouseReadinessState StorageLayerCatalogBoundaryState,
    DataWarehouseLakehouseReadinessState IngestionIntakeBoundaryState,
    DataWarehouseLakehouseReadinessState PartitioningScopeBoundaryState,
    DataWarehouseLakehouseReadinessState LineageControlBoundaryState,
    DataWarehouseLakehouseReadinessState WarehouseReviewBoundaryState,
    DataWarehouseLakehouseReadinessState AutomatedDecisionBoundaryState,
    DataWarehouseLakehouseReadinessState VaultDependencyState,
    DataWarehouseLakehouseReadinessState LoggingMonitoringDependencyState,
    DataWarehouseLakehouseReadinessState DataContractRegistryDependencyState,
    DataWarehouseLakehouseReadinessState NotificationDependencyState,
    DataWarehouseLakehouseReadinessState StewardshipPreconditionState,
    DataWarehouseLakehouseReadinessState DataMinimizationState,
    DataWarehouseLakehouseReadinessState RetentionPolicyState,
    DataWarehouseLakehouseReadinessState StorageTierPolicyState,
    IReadOnlyDictionary<string, DataWarehouseLakehouseReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long DataWarehouseLakehouseReadinessVersion,
    string? DeferredReason);

public sealed record DataWarehouseLakehouseReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    DataWarehouseLakehouseReadinessState DataWarehouseLakehouseReadinessState,
    DataWarehouseLakehouseReadinessState StorageLayerCatalogBoundaryState,
    DataWarehouseLakehouseReadinessState VaultDependencyState,
    DataWarehouseLakehouseReadinessState LineageControlBoundaryState,
    DataWarehouseLakehouseReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record DataWarehouseLakehouseAuditMetadataDto(
    Guid Id,
    string Code,
    DataWarehouseLakehouseReadinessState RetentionPolicyState,
    DataWarehouseLakehouseReadinessState StorageTierPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class DataWarehouseLakehouseMapper
{
    public static DataWarehouseLakehouseReadinessDto ToDto(DataWarehouseLakehouseReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DataWarehouseLakehouseReadinessState,
            entity.StorageLayerCatalogBoundaryState,
            entity.IngestionIntakeBoundaryState,
            entity.PartitioningScopeBoundaryState,
            entity.LineageControlBoundaryState,
            entity.WarehouseReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.VaultDependencyState,
            entity.LoggingMonitoringDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.StorageTierPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.DataWarehouseLakehouseReadinessVersion,
            entity.DeferredReason);

    public static DataWarehouseLakehouseReadinessListItemDto ToListItem(DataWarehouseLakehouseReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DataWarehouseLakehouseReadinessState,
            entity.StorageLayerCatalogBoundaryState,
            entity.VaultDependencyState,
            entity.LineageControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static DataWarehouseLakehouseAuditMetadataDto ToAuditMetadata(DataWarehouseLakehouseReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.StorageTierPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
