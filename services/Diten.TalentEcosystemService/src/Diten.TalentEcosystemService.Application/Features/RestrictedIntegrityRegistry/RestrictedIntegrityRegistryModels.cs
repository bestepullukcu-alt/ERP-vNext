using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry;

public class RestrictedIntegrityRegistryReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public RestrictedIntegrityRegistryReadinessState RestrictedIntegrityRegistryReadinessState { get; init; } = RestrictedIntegrityRegistryReadinessState.Draft;
    public RestrictedIntegrityRegistryReadinessState IntegrityCaseCatalogBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState RestrictionScopeBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState EvidenceChainBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState DisclosureControlBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState CaseReviewBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState AutomatedDecisionBoundaryState { get; init; } = RestrictedIntegrityRegistryReadinessState.Blocked;
    public RestrictedIntegrityRegistryReadinessState EarlyWarningSourceDependencyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState ConsentPolicyDependencyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState LegalHoldDependencyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState NotificationDependencyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState ConsentPreconditionState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState DataMinimizationState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState RetentionPolicyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public RestrictedIntegrityRegistryReadinessState EvidencePolicyState { get; init; } = RestrictedIntegrityRegistryReadinessState.Deferred;
    public IReadOnlyDictionary<string, RestrictedIntegrityRegistryReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, RestrictedIntegrityRegistryReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long RestrictedIntegrityRegistryReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record RestrictedIntegrityRegistryReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    RestrictedIntegrityRegistryReadinessState RestrictedIntegrityRegistryReadinessState,
    RestrictedIntegrityRegistryReadinessState IntegrityCaseCatalogBoundaryState,
    RestrictedIntegrityRegistryReadinessState RestrictionScopeBoundaryState,
    RestrictedIntegrityRegistryReadinessState EvidenceChainBoundaryState,
    RestrictedIntegrityRegistryReadinessState DisclosureControlBoundaryState,
    RestrictedIntegrityRegistryReadinessState CaseReviewBoundaryState,
    RestrictedIntegrityRegistryReadinessState AutomatedDecisionBoundaryState,
    RestrictedIntegrityRegistryReadinessState EarlyWarningSourceDependencyState,
    RestrictedIntegrityRegistryReadinessState ConsentPolicyDependencyState,
    RestrictedIntegrityRegistryReadinessState LegalHoldDependencyState,
    RestrictedIntegrityRegistryReadinessState NotificationDependencyState,
    RestrictedIntegrityRegistryReadinessState ConsentPreconditionState,
    RestrictedIntegrityRegistryReadinessState DataMinimizationState,
    RestrictedIntegrityRegistryReadinessState RetentionPolicyState,
    RestrictedIntegrityRegistryReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, RestrictedIntegrityRegistryReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long RestrictedIntegrityRegistryReadinessVersion,
    string? DeferredReason);

public sealed record RestrictedIntegrityRegistryReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    RestrictedIntegrityRegistryReadinessState RestrictedIntegrityRegistryReadinessState,
    RestrictedIntegrityRegistryReadinessState IntegrityCaseCatalogBoundaryState,
    RestrictedIntegrityRegistryReadinessState EarlyWarningSourceDependencyState,
    RestrictedIntegrityRegistryReadinessState DisclosureControlBoundaryState,
    RestrictedIntegrityRegistryReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record RestrictedIntegrityRegistryAuditMetadataDto(
    Guid Id,
    string Code,
    RestrictedIntegrityRegistryReadinessState RetentionPolicyState,
    RestrictedIntegrityRegistryReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class RestrictedIntegrityRegistryMapper
{
    public static RestrictedIntegrityRegistryReadinessDto ToDto(RestrictedIntegrityRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.RestrictedIntegrityRegistryReadinessState,
            entity.IntegrityCaseCatalogBoundaryState,
            entity.RestrictionScopeBoundaryState,
            entity.EvidenceChainBoundaryState,
            entity.DisclosureControlBoundaryState,
            entity.CaseReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.EarlyWarningSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.LegalHoldDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.RestrictedIntegrityRegistryReadinessVersion,
            entity.DeferredReason);

    public static RestrictedIntegrityRegistryReadinessListItemDto ToListItem(RestrictedIntegrityRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.RestrictedIntegrityRegistryReadinessState,
            entity.IntegrityCaseCatalogBoundaryState,
            entity.EarlyWarningSourceDependencyState,
            entity.DisclosureControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static RestrictedIntegrityRegistryAuditMetadataDto ToAuditMetadata(RestrictedIntegrityRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
