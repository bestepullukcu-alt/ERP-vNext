using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget;

public class HeadcountBudgetReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HeadcountBudgetReadinessState HeadcountBudgetReadinessState { get; init; } = HeadcountBudgetReadinessState.Draft;
    public HeadcountBudgetReadinessState HeadcountRequisitionBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState PositionBudgetBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState BudgetAllocationBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState BudgetApprovalBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState BudgetReconciliationBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState AutomatedDecisionBoundaryState { get; init; } = HeadcountBudgetReadinessState.Blocked;
    public HeadcountBudgetReadinessState OrganizationStructureDependencyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState PositionFrameworkDependencyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState DocumentDependencyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState NotificationDependencyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState ConsentPreconditionState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState DataMinimizationState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState RetentionPolicyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public HeadcountBudgetReadinessState EvidencePolicyState { get; init; } = HeadcountBudgetReadinessState.Deferred;
    public IReadOnlyDictionary<string, HeadcountBudgetReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HeadcountBudgetReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HeadcountBudgetReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HeadcountBudgetReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HeadcountBudgetReadinessState HeadcountBudgetReadinessState,
    HeadcountBudgetReadinessState HeadcountRequisitionBoundaryState,
    HeadcountBudgetReadinessState PositionBudgetBoundaryState,
    HeadcountBudgetReadinessState BudgetAllocationBoundaryState,
    HeadcountBudgetReadinessState BudgetApprovalBoundaryState,
    HeadcountBudgetReadinessState BudgetReconciliationBoundaryState,
    HeadcountBudgetReadinessState AutomatedDecisionBoundaryState,
    HeadcountBudgetReadinessState OrganizationStructureDependencyState,
    HeadcountBudgetReadinessState PositionFrameworkDependencyState,
    HeadcountBudgetReadinessState DocumentDependencyState,
    HeadcountBudgetReadinessState NotificationDependencyState,
    HeadcountBudgetReadinessState ConsentPreconditionState,
    HeadcountBudgetReadinessState DataMinimizationState,
    HeadcountBudgetReadinessState RetentionPolicyState,
    HeadcountBudgetReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HeadcountBudgetReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HeadcountBudgetReadinessVersion,
    string? DeferredReason);

public sealed record HeadcountBudgetReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HeadcountBudgetReadinessState HeadcountBudgetReadinessState,
    HeadcountBudgetReadinessState HeadcountRequisitionBoundaryState,
    HeadcountBudgetReadinessState OrganizationStructureDependencyState,
    HeadcountBudgetReadinessState BudgetApprovalBoundaryState,
    HeadcountBudgetReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HeadcountBudgetAuditMetadataDto(
    Guid Id,
    string Code,
    HeadcountBudgetReadinessState RetentionPolicyState,
    HeadcountBudgetReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HeadcountBudgetMapper
{
    public static HeadcountBudgetReadinessDto ToDto(HeadcountBudgetReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HeadcountBudgetReadinessState,
            entity.HeadcountRequisitionBoundaryState,
            entity.PositionBudgetBoundaryState,
            entity.BudgetAllocationBoundaryState,
            entity.BudgetApprovalBoundaryState,
            entity.BudgetReconciliationBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.OrganizationStructureDependencyState,
            entity.PositionFrameworkDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HeadcountBudgetReadinessVersion,
            entity.DeferredReason);

    public static HeadcountBudgetReadinessListItemDto ToListItem(HeadcountBudgetReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HeadcountBudgetReadinessState,
            entity.HeadcountRequisitionBoundaryState,
            entity.OrganizationStructureDependencyState,
            entity.BudgetApprovalBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HeadcountBudgetAuditMetadataDto ToAuditMetadata(HeadcountBudgetReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
