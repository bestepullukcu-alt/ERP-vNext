using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog;

public class KpiCatalogReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public KpiCatalogReadinessState KpiCatalogReadinessState { get; init; } = KpiCatalogReadinessState.Draft;
    public KpiCatalogReadinessState KpiIdentityCatalogBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState DefinitionBindingIntakeBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState OwnershipScopeBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState PublicationControlBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState CatalogReviewBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState AutomatedDecisionBoundaryState { get; init; } = KpiCatalogReadinessState.Blocked;
    public KpiCatalogReadinessState MetricSemanticRegistrySourceDependencyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState DataDictionaryDependencyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState DataContractRegistryDependencyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState NotificationDependencyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState StewardshipPreconditionState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState DataMinimizationState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState RetentionPolicyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public KpiCatalogReadinessState PublicationPolicyState { get; init; } = KpiCatalogReadinessState.Deferred;
    public IReadOnlyDictionary<string, KpiCatalogReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, KpiCatalogReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long KpiCatalogReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record KpiCatalogReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    KpiCatalogReadinessState KpiCatalogReadinessState,
    KpiCatalogReadinessState KpiIdentityCatalogBoundaryState,
    KpiCatalogReadinessState DefinitionBindingIntakeBoundaryState,
    KpiCatalogReadinessState OwnershipScopeBoundaryState,
    KpiCatalogReadinessState PublicationControlBoundaryState,
    KpiCatalogReadinessState CatalogReviewBoundaryState,
    KpiCatalogReadinessState AutomatedDecisionBoundaryState,
    KpiCatalogReadinessState MetricSemanticRegistrySourceDependencyState,
    KpiCatalogReadinessState DataDictionaryDependencyState,
    KpiCatalogReadinessState DataContractRegistryDependencyState,
    KpiCatalogReadinessState NotificationDependencyState,
    KpiCatalogReadinessState StewardshipPreconditionState,
    KpiCatalogReadinessState DataMinimizationState,
    KpiCatalogReadinessState RetentionPolicyState,
    KpiCatalogReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, KpiCatalogReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long KpiCatalogReadinessVersion,
    string? DeferredReason);

public sealed record KpiCatalogReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    KpiCatalogReadinessState KpiCatalogReadinessState,
    KpiCatalogReadinessState KpiIdentityCatalogBoundaryState,
    KpiCatalogReadinessState MetricSemanticRegistrySourceDependencyState,
    KpiCatalogReadinessState PublicationControlBoundaryState,
    KpiCatalogReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record KpiCatalogAuditMetadataDto(
    Guid Id,
    string Code,
    KpiCatalogReadinessState RetentionPolicyState,
    KpiCatalogReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class KpiCatalogMapper
{
    public static KpiCatalogReadinessDto ToDto(KpiCatalogReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.KpiCatalogReadinessState,
            entity.KpiIdentityCatalogBoundaryState,
            entity.DefinitionBindingIntakeBoundaryState,
            entity.OwnershipScopeBoundaryState,
            entity.PublicationControlBoundaryState,
            entity.CatalogReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.DataDictionaryDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.KpiCatalogReadinessVersion,
            entity.DeferredReason);

    public static KpiCatalogReadinessListItemDto ToListItem(KpiCatalogReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.KpiCatalogReadinessState,
            entity.KpiIdentityCatalogBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.PublicationControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static KpiCatalogAuditMetadataDto ToAuditMetadata(KpiCatalogReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
