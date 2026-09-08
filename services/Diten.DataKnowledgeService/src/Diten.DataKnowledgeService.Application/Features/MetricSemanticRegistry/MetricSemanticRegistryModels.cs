using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry;

public class MetricSemanticRegistryReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public MetricSemanticRegistryReadinessState MetricSemanticRegistryReadinessState { get; init; } = MetricSemanticRegistryReadinessState.Draft;
    public MetricSemanticRegistryReadinessState MetricIdentityCatalogBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState SemanticEntityIntakeBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState DimensionMeasureScopeBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState SemanticBindingControlBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState RegistryReviewBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState AutomatedDecisionBoundaryState { get; init; } = MetricSemanticRegistryReadinessState.Blocked;
    public MetricSemanticRegistryReadinessState DataSourceRegistryDependencyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState DataGovernancePolicyDependencyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState SemanticContractSourceDependencyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState NotificationDependencyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState StewardshipPreconditionState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState DataMinimizationState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState RetentionPolicyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public MetricSemanticRegistryReadinessState VersioningPolicyState { get; init; } = MetricSemanticRegistryReadinessState.Deferred;
    public IReadOnlyDictionary<string, MetricSemanticRegistryReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, MetricSemanticRegistryReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long MetricSemanticRegistryReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record MetricSemanticRegistryReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    MetricSemanticRegistryReadinessState MetricSemanticRegistryReadinessState,
    MetricSemanticRegistryReadinessState MetricIdentityCatalogBoundaryState,
    MetricSemanticRegistryReadinessState SemanticEntityIntakeBoundaryState,
    MetricSemanticRegistryReadinessState DimensionMeasureScopeBoundaryState,
    MetricSemanticRegistryReadinessState SemanticBindingControlBoundaryState,
    MetricSemanticRegistryReadinessState RegistryReviewBoundaryState,
    MetricSemanticRegistryReadinessState AutomatedDecisionBoundaryState,
    MetricSemanticRegistryReadinessState DataSourceRegistryDependencyState,
    MetricSemanticRegistryReadinessState DataGovernancePolicyDependencyState,
    MetricSemanticRegistryReadinessState SemanticContractSourceDependencyState,
    MetricSemanticRegistryReadinessState NotificationDependencyState,
    MetricSemanticRegistryReadinessState StewardshipPreconditionState,
    MetricSemanticRegistryReadinessState DataMinimizationState,
    MetricSemanticRegistryReadinessState RetentionPolicyState,
    MetricSemanticRegistryReadinessState VersioningPolicyState,
    IReadOnlyDictionary<string, MetricSemanticRegistryReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long MetricSemanticRegistryReadinessVersion,
    string? DeferredReason);

public sealed record MetricSemanticRegistryReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    MetricSemanticRegistryReadinessState MetricSemanticRegistryReadinessState,
    MetricSemanticRegistryReadinessState MetricIdentityCatalogBoundaryState,
    MetricSemanticRegistryReadinessState DataSourceRegistryDependencyState,
    MetricSemanticRegistryReadinessState SemanticBindingControlBoundaryState,
    MetricSemanticRegistryReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record MetricSemanticRegistryAuditMetadataDto(
    Guid Id,
    string Code,
    MetricSemanticRegistryReadinessState RetentionPolicyState,
    MetricSemanticRegistryReadinessState VersioningPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class MetricSemanticRegistryMapper
{
    public static MetricSemanticRegistryReadinessDto ToDto(MetricSemanticRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MetricSemanticRegistryReadinessState,
            entity.MetricIdentityCatalogBoundaryState,
            entity.SemanticEntityIntakeBoundaryState,
            entity.DimensionMeasureScopeBoundaryState,
            entity.SemanticBindingControlBoundaryState,
            entity.RegistryReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.DataSourceRegistryDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.SemanticContractSourceDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.VersioningPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.MetricSemanticRegistryReadinessVersion,
            entity.DeferredReason);

    public static MetricSemanticRegistryReadinessListItemDto ToListItem(MetricSemanticRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.MetricSemanticRegistryReadinessState,
            entity.MetricIdentityCatalogBoundaryState,
            entity.DataSourceRegistryDependencyState,
            entity.SemanticBindingControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static MetricSemanticRegistryAuditMetadataDto ToAuditMetadata(MetricSemanticRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.VersioningPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
