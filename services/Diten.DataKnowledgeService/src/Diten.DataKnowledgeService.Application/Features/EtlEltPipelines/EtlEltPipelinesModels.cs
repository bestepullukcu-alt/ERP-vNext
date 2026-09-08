using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines;

public class EtlEltPipelinesReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EtlEltPipelinesReadinessState EtlEltPipelinesReadinessState { get; init; } = EtlEltPipelinesReadinessState.Draft;
    public EtlEltPipelinesReadinessState PipelineDefinitionCatalogBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState ExtractIntakeBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState TransformScopeBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState LoadControlBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState OrchestrationReviewBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState AutomatedDecisionBoundaryState { get; init; } = EtlEltPipelinesReadinessState.Blocked;
    public EtlEltPipelinesReadinessState LakehouseSourceDependencyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState JobOrchestrationDependencyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState DataContractRegistryDependencyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState NotificationDependencyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState StewardshipPreconditionState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState DataMinimizationState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState RetentionPolicyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public EtlEltPipelinesReadinessState MonitoringPolicyState { get; init; } = EtlEltPipelinesReadinessState.Deferred;
    public IReadOnlyDictionary<string, EtlEltPipelinesReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, EtlEltPipelinesReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long EtlEltPipelinesReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record EtlEltPipelinesReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    EtlEltPipelinesReadinessState EtlEltPipelinesReadinessState,
    EtlEltPipelinesReadinessState PipelineDefinitionCatalogBoundaryState,
    EtlEltPipelinesReadinessState ExtractIntakeBoundaryState,
    EtlEltPipelinesReadinessState TransformScopeBoundaryState,
    EtlEltPipelinesReadinessState LoadControlBoundaryState,
    EtlEltPipelinesReadinessState OrchestrationReviewBoundaryState,
    EtlEltPipelinesReadinessState AutomatedDecisionBoundaryState,
    EtlEltPipelinesReadinessState LakehouseSourceDependencyState,
    EtlEltPipelinesReadinessState JobOrchestrationDependencyState,
    EtlEltPipelinesReadinessState DataContractRegistryDependencyState,
    EtlEltPipelinesReadinessState NotificationDependencyState,
    EtlEltPipelinesReadinessState StewardshipPreconditionState,
    EtlEltPipelinesReadinessState DataMinimizationState,
    EtlEltPipelinesReadinessState RetentionPolicyState,
    EtlEltPipelinesReadinessState MonitoringPolicyState,
    IReadOnlyDictionary<string, EtlEltPipelinesReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long EtlEltPipelinesReadinessVersion,
    string? DeferredReason);

public sealed record EtlEltPipelinesReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    EtlEltPipelinesReadinessState EtlEltPipelinesReadinessState,
    EtlEltPipelinesReadinessState PipelineDefinitionCatalogBoundaryState,
    EtlEltPipelinesReadinessState LakehouseSourceDependencyState,
    EtlEltPipelinesReadinessState LoadControlBoundaryState,
    EtlEltPipelinesReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record EtlEltPipelinesAuditMetadataDto(
    Guid Id,
    string Code,
    EtlEltPipelinesReadinessState RetentionPolicyState,
    EtlEltPipelinesReadinessState MonitoringPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class EtlEltPipelinesMapper
{
    public static EtlEltPipelinesReadinessDto ToDto(EtlEltPipelinesReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EtlEltPipelinesReadinessState,
            entity.PipelineDefinitionCatalogBoundaryState,
            entity.ExtractIntakeBoundaryState,
            entity.TransformScopeBoundaryState,
            entity.LoadControlBoundaryState,
            entity.OrchestrationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.LakehouseSourceDependencyState,
            entity.JobOrchestrationDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.MonitoringPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.EtlEltPipelinesReadinessVersion,
            entity.DeferredReason);

    public static EtlEltPipelinesReadinessListItemDto ToListItem(EtlEltPipelinesReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EtlEltPipelinesReadinessState,
            entity.PipelineDefinitionCatalogBoundaryState,
            entity.LakehouseSourceDependencyState,
            entity.LoadControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static EtlEltPipelinesAuditMetadataDto ToAuditMetadata(EtlEltPipelinesReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.MonitoringPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
