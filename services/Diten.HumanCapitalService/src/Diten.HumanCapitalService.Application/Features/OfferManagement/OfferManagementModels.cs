using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement;

public class OfferReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public OfferReadinessState OfferReadinessState { get; init; } = OfferReadinessState.Draft;
    public OfferReadinessState OfferWorkflowBoundaryState { get; init; } = OfferReadinessState.Blocked;
    public OfferReadinessState ApprovalWorkflowBoundaryState { get; init; } = OfferReadinessState.Blocked;
    public OfferReadinessState CandidateAcceptanceBoundaryState { get; init; } = OfferReadinessState.Blocked;
    public OfferReadinessState OfferDocumentBoundaryState { get; init; } = OfferReadinessState.Blocked;
    public OfferReadinessState CompensationDataBoundaryState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState BenefitsDataBoundaryState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState PayrollDataBoundaryState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState ConsentPreconditionState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState DataMinimizationState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState RetentionPolicyState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState EvidencePolicyState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState NotificationDependencyState { get; init; } = OfferReadinessState.Deferred;
    public OfferReadinessState DocumentDependencyState { get; init; } = OfferReadinessState.Deferred;
    public IReadOnlyDictionary<string, OfferReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, OfferReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long OfferReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record OfferReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    OfferReadinessState OfferReadinessState,
    OfferReadinessState OfferWorkflowBoundaryState,
    OfferReadinessState ApprovalWorkflowBoundaryState,
    OfferReadinessState CandidateAcceptanceBoundaryState,
    OfferReadinessState OfferDocumentBoundaryState,
    OfferReadinessState CompensationDataBoundaryState,
    OfferReadinessState BenefitsDataBoundaryState,
    OfferReadinessState PayrollDataBoundaryState,
    OfferReadinessState ConsentPreconditionState,
    OfferReadinessState DataMinimizationState,
    OfferReadinessState RetentionPolicyState,
    OfferReadinessState EvidencePolicyState,
    OfferReadinessState NotificationDependencyState,
    OfferReadinessState DocumentDependencyState,
    IReadOnlyDictionary<string, OfferReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long OfferReadinessVersion,
    string? DeferredReason);

public sealed record OfferReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    OfferReadinessState OfferReadinessState,
    OfferReadinessState OfferWorkflowBoundaryState,
    OfferReadinessState ApprovalWorkflowBoundaryState,
    OfferReadinessState CandidateAcceptanceBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record OfferAuditMetadataDto(
    Guid Id,
    string Code,
    OfferReadinessState RetentionPolicyState,
    OfferReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class OfferManagementMapper
{
    public static OfferReadinessDto ToDto(OfferReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.OfferReadinessState,
            entity.OfferWorkflowBoundaryState,
            entity.ApprovalWorkflowBoundaryState,
            entity.CandidateAcceptanceBoundaryState,
            entity.OfferDocumentBoundaryState,
            entity.CompensationDataBoundaryState,
            entity.BenefitsDataBoundaryState,
            entity.PayrollDataBoundaryState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.OfferReadinessVersion,
            entity.DeferredReason);

    public static OfferReadinessListItemDto ToListItem(OfferReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.OfferReadinessState,
            entity.OfferWorkflowBoundaryState,
            entity.ApprovalWorkflowBoundaryState,
            entity.CandidateAcceptanceBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static OfferAuditMetadataDto ToAuditMetadata(OfferReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
