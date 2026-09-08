using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation;

public class TalentDataFoundationReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public TalentDataFoundationReadinessState TalentDataFoundationReadinessState { get; init; } = TalentDataFoundationReadinessState.Draft;
    public TalentDataFoundationReadinessState TalentEntityCatalogBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState DataIngestionBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState IdentityResolutionBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState DataQualityBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState LineageTrackingBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState AutomatedDecisionBoundaryState { get; init; } = TalentDataFoundationReadinessState.Blocked;
    public TalentDataFoundationReadinessState HcmFoundationDependencyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState ConsentPolicyDependencyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState DocumentDependencyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState NotificationDependencyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState ConsentPreconditionState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState DataMinimizationState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState RetentionPolicyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public TalentDataFoundationReadinessState EvidencePolicyState { get; init; } = TalentDataFoundationReadinessState.Deferred;
    public IReadOnlyDictionary<string, TalentDataFoundationReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, TalentDataFoundationReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long TalentDataFoundationReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record TalentDataFoundationReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentDataFoundationReadinessState TalentDataFoundationReadinessState,
    TalentDataFoundationReadinessState TalentEntityCatalogBoundaryState,
    TalentDataFoundationReadinessState DataIngestionBoundaryState,
    TalentDataFoundationReadinessState IdentityResolutionBoundaryState,
    TalentDataFoundationReadinessState DataQualityBoundaryState,
    TalentDataFoundationReadinessState LineageTrackingBoundaryState,
    TalentDataFoundationReadinessState AutomatedDecisionBoundaryState,
    TalentDataFoundationReadinessState HcmFoundationDependencyState,
    TalentDataFoundationReadinessState ConsentPolicyDependencyState,
    TalentDataFoundationReadinessState DocumentDependencyState,
    TalentDataFoundationReadinessState NotificationDependencyState,
    TalentDataFoundationReadinessState ConsentPreconditionState,
    TalentDataFoundationReadinessState DataMinimizationState,
    TalentDataFoundationReadinessState RetentionPolicyState,
    TalentDataFoundationReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, TalentDataFoundationReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long TalentDataFoundationReadinessVersion,
    string? DeferredReason);

public sealed record TalentDataFoundationReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TalentDataFoundationReadinessState TalentDataFoundationReadinessState,
    TalentDataFoundationReadinessState TalentEntityCatalogBoundaryState,
    TalentDataFoundationReadinessState HcmFoundationDependencyState,
    TalentDataFoundationReadinessState DataQualityBoundaryState,
    TalentDataFoundationReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record TalentDataFoundationAuditMetadataDto(
    Guid Id,
    string Code,
    TalentDataFoundationReadinessState RetentionPolicyState,
    TalentDataFoundationReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class TalentDataFoundationMapper
{
    public static TalentDataFoundationReadinessDto ToDto(TalentDataFoundationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentDataFoundationReadinessState,
            entity.TalentEntityCatalogBoundaryState,
            entity.DataIngestionBoundaryState,
            entity.IdentityResolutionBoundaryState,
            entity.DataQualityBoundaryState,
            entity.LineageTrackingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.HcmFoundationDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.TalentDataFoundationReadinessVersion,
            entity.DeferredReason);

    public static TalentDataFoundationReadinessListItemDto ToListItem(TalentDataFoundationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TalentDataFoundationReadinessState,
            entity.TalentEntityCatalogBoundaryState,
            entity.HcmFoundationDependencyState,
            entity.DataQualityBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static TalentDataFoundationAuditMetadataDto ToAuditMetadata(TalentDataFoundationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
