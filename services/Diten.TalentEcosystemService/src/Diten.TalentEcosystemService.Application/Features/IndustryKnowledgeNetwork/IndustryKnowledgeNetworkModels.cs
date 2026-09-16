using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork;

public class IndustryKnowledgeNetworkReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IndustryKnowledgeNetworkReadinessState IndustryKnowledgeNetworkReadinessState { get; init; } = IndustryKnowledgeNetworkReadinessState.Draft;
    public IndustryKnowledgeNetworkReadinessState KnowledgeCatalogBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState ContentBindingIntakeBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState NetworkScopeBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState VisibilityControlBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState KnowledgeReviewBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState AutomatedDecisionBoundaryState { get; init; } = IndustryKnowledgeNetworkReadinessState.Blocked;
    public IndustryKnowledgeNetworkReadinessState KnowledgeSourceRegistryDependencyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState SectorTrendSourceDependencyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState DataGovernancePolicyDependencyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState AssociationOperationsSourceDependencyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState ConsentPreconditionState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState DataMinimizationState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState RetentionPolicyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IndustryKnowledgeNetworkReadinessState PublicationPolicyState { get; init; } = IndustryKnowledgeNetworkReadinessState.Deferred;
    public IReadOnlyDictionary<string, IndustryKnowledgeNetworkReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, IndustryKnowledgeNetworkReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long IndustryKnowledgeNetworkReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record IndustryKnowledgeNetworkReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustryKnowledgeNetworkReadinessState IndustryKnowledgeNetworkReadinessState,
    IndustryKnowledgeNetworkReadinessState KnowledgeCatalogBoundaryState,
    IndustryKnowledgeNetworkReadinessState ContentBindingIntakeBoundaryState,
    IndustryKnowledgeNetworkReadinessState NetworkScopeBoundaryState,
    IndustryKnowledgeNetworkReadinessState VisibilityControlBoundaryState,
    IndustryKnowledgeNetworkReadinessState KnowledgeReviewBoundaryState,
    IndustryKnowledgeNetworkReadinessState AutomatedDecisionBoundaryState,
    IndustryKnowledgeNetworkReadinessState KnowledgeSourceRegistryDependencyState,
    IndustryKnowledgeNetworkReadinessState SectorTrendSourceDependencyState,
    IndustryKnowledgeNetworkReadinessState DataGovernancePolicyDependencyState,
    IndustryKnowledgeNetworkReadinessState AssociationOperationsSourceDependencyState,
    IndustryKnowledgeNetworkReadinessState ConsentPreconditionState,
    IndustryKnowledgeNetworkReadinessState DataMinimizationState,
    IndustryKnowledgeNetworkReadinessState RetentionPolicyState,
    IndustryKnowledgeNetworkReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, IndustryKnowledgeNetworkReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long IndustryKnowledgeNetworkReadinessVersion,
    string? DeferredReason);

public sealed record IndustryKnowledgeNetworkReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    IndustryKnowledgeNetworkReadinessState IndustryKnowledgeNetworkReadinessState,
    IndustryKnowledgeNetworkReadinessState KnowledgeCatalogBoundaryState,
    IndustryKnowledgeNetworkReadinessState KnowledgeSourceRegistryDependencyState,
    IndustryKnowledgeNetworkReadinessState VisibilityControlBoundaryState,
    IndustryKnowledgeNetworkReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record IndustryKnowledgeNetworkAuditMetadataDto(
    Guid Id,
    string Code,
    IndustryKnowledgeNetworkReadinessState RetentionPolicyState,
    IndustryKnowledgeNetworkReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class IndustryKnowledgeNetworkMapper
{
    public static IndustryKnowledgeNetworkReadinessDto ToDto(IndustryKnowledgeNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustryKnowledgeNetworkReadinessState,
            entity.KnowledgeCatalogBoundaryState,
            entity.ContentBindingIntakeBoundaryState,
            entity.NetworkScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.KnowledgeReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.KnowledgeSourceRegistryDependencyState,
            entity.SectorTrendSourceDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.AssociationOperationsSourceDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.IndustryKnowledgeNetworkReadinessVersion,
            entity.DeferredReason);

    public static IndustryKnowledgeNetworkReadinessListItemDto ToListItem(IndustryKnowledgeNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IndustryKnowledgeNetworkReadinessState,
            entity.KnowledgeCatalogBoundaryState,
            entity.KnowledgeSourceRegistryDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static IndustryKnowledgeNetworkAuditMetadataDto ToAuditMetadata(IndustryKnowledgeNetworkReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
