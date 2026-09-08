using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations;

public class AssociationOperationsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public AssociationOperationsReadinessState AssociationOperationsReadinessState { get; init; } = AssociationOperationsReadinessState.Draft;
    public AssociationOperationsReadinessState MembershipCatalogBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState ServiceBindingIntakeBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState ProgramScopeBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState VisibilityControlBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState OperationsReviewBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState AutomatedDecisionBoundaryState { get; init; } = AssociationOperationsReadinessState.Blocked;
    public AssociationOperationsReadinessState MemberRegistrySourceDependencyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState SectorTrendSourceDependencyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState DataGovernancePolicyDependencyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState NotificationDependencyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState ConsentPreconditionState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState DataMinimizationState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState RetentionPolicyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public AssociationOperationsReadinessState PublicationPolicyState { get; init; } = AssociationOperationsReadinessState.Deferred;
    public IReadOnlyDictionary<string, AssociationOperationsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, AssociationOperationsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long AssociationOperationsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record AssociationOperationsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    AssociationOperationsReadinessState AssociationOperationsReadinessState,
    AssociationOperationsReadinessState MembershipCatalogBoundaryState,
    AssociationOperationsReadinessState ServiceBindingIntakeBoundaryState,
    AssociationOperationsReadinessState ProgramScopeBoundaryState,
    AssociationOperationsReadinessState VisibilityControlBoundaryState,
    AssociationOperationsReadinessState OperationsReviewBoundaryState,
    AssociationOperationsReadinessState AutomatedDecisionBoundaryState,
    AssociationOperationsReadinessState MemberRegistrySourceDependencyState,
    AssociationOperationsReadinessState SectorTrendSourceDependencyState,
    AssociationOperationsReadinessState DataGovernancePolicyDependencyState,
    AssociationOperationsReadinessState NotificationDependencyState,
    AssociationOperationsReadinessState ConsentPreconditionState,
    AssociationOperationsReadinessState DataMinimizationState,
    AssociationOperationsReadinessState RetentionPolicyState,
    AssociationOperationsReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, AssociationOperationsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long AssociationOperationsReadinessVersion,
    string? DeferredReason);

public sealed record AssociationOperationsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    AssociationOperationsReadinessState AssociationOperationsReadinessState,
    AssociationOperationsReadinessState MembershipCatalogBoundaryState,
    AssociationOperationsReadinessState MemberRegistrySourceDependencyState,
    AssociationOperationsReadinessState VisibilityControlBoundaryState,
    AssociationOperationsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record AssociationOperationsAuditMetadataDto(
    Guid Id,
    string Code,
    AssociationOperationsReadinessState RetentionPolicyState,
    AssociationOperationsReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class AssociationOperationsMapper
{
    public static AssociationOperationsReadinessDto ToDto(AssociationOperationsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.AssociationOperationsReadinessState,
            entity.MembershipCatalogBoundaryState,
            entity.ServiceBindingIntakeBoundaryState,
            entity.ProgramScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.OperationsReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MemberRegistrySourceDependencyState,
            entity.SectorTrendSourceDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.AssociationOperationsReadinessVersion,
            entity.DeferredReason);

    public static AssociationOperationsReadinessListItemDto ToListItem(AssociationOperationsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.AssociationOperationsReadinessState,
            entity.MembershipCatalogBoundaryState,
            entity.MemberRegistrySourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static AssociationOperationsAuditMetadataDto ToAuditMetadata(AssociationOperationsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
