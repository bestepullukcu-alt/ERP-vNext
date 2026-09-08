using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership;

public class MetricDefinitionsOwnershipReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public MetricDefinitionsOwnershipReadinessState MetricDefinitionsOwnershipReadinessState { get; init; } = MetricDefinitionsOwnershipReadinessState.Draft;
    public MetricDefinitionsOwnershipReadinessState DefinitionCatalogBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState OwnershipAssignmentIntakeBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState StewardshipScopeBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState ApprovalControlBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState DefinitionReviewBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState AutomatedDecisionBoundaryState { get; init; } = MetricDefinitionsOwnershipReadinessState.Blocked;
    public MetricDefinitionsOwnershipReadinessState MetricSemanticRegistrySourceDependencyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState KpiCatalogSourceDependencyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState DataContractRegistryDependencyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState NotificationDependencyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState StewardshipPreconditionState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState DataMinimizationState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState RetentionPolicyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public MetricDefinitionsOwnershipReadinessState ApprovalPolicyState { get; init; } = MetricDefinitionsOwnershipReadinessState.Deferred;
    public IReadOnlyDictionary<string, MetricDefinitionsOwnershipReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, MetricDefinitionsOwnershipReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long MetricDefinitionsOwnershipReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record MetricDefinitionsOwnershipReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    MetricDefinitionsOwnershipReadinessState MetricDefinitionsOwnershipReadinessState,
    MetricDefinitionsOwnershipReadinessState DefinitionCatalogBoundaryState,
    MetricDefinitionsOwnershipReadinessState OwnershipAssignmentIntakeBoundaryState,
    MetricDefinitionsOwnershipReadinessState StewardshipScopeBoundaryState,
    MetricDefinitionsOwnershipReadinessState ApprovalControlBoundaryState,
    MetricDefinitionsOwnershipReadinessState DefinitionReviewBoundaryState,
    MetricDefinitionsOwnershipReadinessState AutomatedDecisionBoundaryState,
    MetricDefinitionsOwnershipReadinessState MetricSemanticRegistrySourceDependencyState,
    MetricDefinitionsOwnershipReadinessState KpiCatalogSourceDependencyState,
    MetricDefinitionsOwnershipReadinessState DataContractRegistryDependencyState,
    MetricDefinitionsOwnershipReadinessState NotificationDependencyState,
    MetricDefinitionsOwnershipReadinessState StewardshipPreconditionState,
    MetricDefinitionsOwnershipReadinessState DataMinimizationState,
    MetricDefinitionsOwnershipReadinessState RetentionPolicyState,
    MetricDefinitionsOwnershipReadinessState ApprovalPolicyState,
    IReadOnlyDictionary<string, MetricDefinitionsOwnershipReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long MetricDefinitionsOwnershipReadinessVersion,
    string? DeferredReason);

public sealed record MetricDefinitionsOwnershipReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    MetricDefinitionsOwnershipReadinessState MetricDefinitionsOwnershipReadinessState,
    MetricDefinitionsOwnershipReadinessState DefinitionCatalogBoundaryState,
    MetricDefinitionsOwnershipReadinessState MetricSemanticRegistrySourceDependencyState,
    MetricDefinitionsOwnershipReadinessState ApprovalControlBoundaryState,
    MetricDefinitionsOwnershipReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record MetricDefinitionsOwnershipAuditMetadataDto(
    Guid Id,
    string Code,
    MetricDefinitionsOwnershipReadinessState RetentionPolicyState,
    MetricDefinitionsOwnershipReadinessState ApprovalPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class MetricDefinitionsOwnershipMapper
{
    public static MetricDefinitionsOwnershipReadinessDto ToDto(MetricDefinitionsOwnershipReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MetricDefinitionsOwnershipReadinessState,
            entity.DefinitionCatalogBoundaryState,
            entity.OwnershipAssignmentIntakeBoundaryState,
            entity.StewardshipScopeBoundaryState,
            entity.ApprovalControlBoundaryState,
            entity.DefinitionReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.KpiCatalogSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.ApprovalPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.MetricDefinitionsOwnershipReadinessVersion,
            entity.DeferredReason);

    public static MetricDefinitionsOwnershipReadinessListItemDto ToListItem(MetricDefinitionsOwnershipReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MetricDefinitionsOwnershipReadinessState,
            entity.DefinitionCatalogBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.ApprovalControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static MetricDefinitionsOwnershipAuditMetadataDto ToAuditMetadata(MetricDefinitionsOwnershipReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.ApprovalPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
