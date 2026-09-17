using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation;

public class HrDocumentationReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HrDocumentationReadinessState HrDocumentationReadinessState { get; init; } = HrDocumentationReadinessState.Draft;
    public HrDocumentationReadinessState DocumentWorkspaceBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState EvidenceLinkBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState DocumentClassificationBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState LegalHoldBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState DispositionScheduleBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState AutomatedDecisionBoundaryState { get; init; } = HrDocumentationReadinessState.Blocked;
    public HrDocumentationReadinessState DocumentRepositoryDependencyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState EvidenceStoreDependencyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState DocumentDependencyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState NotificationDependencyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState ConsentPreconditionState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState DataMinimizationState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState RetentionPolicyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public HrDocumentationReadinessState EvidencePolicyState { get; init; } = HrDocumentationReadinessState.Deferred;
    public IReadOnlyDictionary<string, HrDocumentationReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HrDocumentationReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HrDocumentationReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HrDocumentationReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrDocumentationReadinessState HrDocumentationReadinessState,
    HrDocumentationReadinessState DocumentWorkspaceBoundaryState,
    HrDocumentationReadinessState EvidenceLinkBoundaryState,
    HrDocumentationReadinessState DocumentClassificationBoundaryState,
    HrDocumentationReadinessState LegalHoldBoundaryState,
    HrDocumentationReadinessState DispositionScheduleBoundaryState,
    HrDocumentationReadinessState AutomatedDecisionBoundaryState,
    HrDocumentationReadinessState DocumentRepositoryDependencyState,
    HrDocumentationReadinessState EvidenceStoreDependencyState,
    HrDocumentationReadinessState DocumentDependencyState,
    HrDocumentationReadinessState NotificationDependencyState,
    HrDocumentationReadinessState ConsentPreconditionState,
    HrDocumentationReadinessState DataMinimizationState,
    HrDocumentationReadinessState RetentionPolicyState,
    HrDocumentationReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HrDocumentationReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HrDocumentationReadinessVersion,
    string? DeferredReason);

public sealed record HrDocumentationReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrDocumentationReadinessState HrDocumentationReadinessState,
    HrDocumentationReadinessState DocumentWorkspaceBoundaryState,
    HrDocumentationReadinessState DocumentRepositoryDependencyState,
    HrDocumentationReadinessState LegalHoldBoundaryState,
    HrDocumentationReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HrDocumentationAuditMetadataDto(
    Guid Id,
    string Code,
    HrDocumentationReadinessState RetentionPolicyState,
    HrDocumentationReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HrDocumentationMapper
{
    public static HrDocumentationReadinessDto ToDto(HrDocumentationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrDocumentationReadinessState,
            entity.DocumentWorkspaceBoundaryState,
            entity.EvidenceLinkBoundaryState,
            entity.DocumentClassificationBoundaryState,
            entity.LegalHoldBoundaryState,
            entity.DispositionScheduleBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.DocumentRepositoryDependencyState,
            entity.EvidenceStoreDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HrDocumentationReadinessVersion,
            entity.DeferredReason);

    public static HrDocumentationReadinessListItemDto ToListItem(HrDocumentationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrDocumentationReadinessState,
            entity.DocumentWorkspaceBoundaryState,
            entity.DocumentRepositoryDependencyState,
            entity.LegalHoldBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HrDocumentationAuditMetadataDto ToAuditMetadata(HrDocumentationReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
