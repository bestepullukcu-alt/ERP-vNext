using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges;

public class EmploymentChangeReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EmploymentChangeReadinessState EmploymentChangeReadinessState { get; init; } = EmploymentChangeReadinessState.Draft;
    public EmploymentChangeReadinessState ChangeLifecycleBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState TransferBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState PromotionBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState ApprovalBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState PositionAssignmentBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState EmployeeActionBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState ManagerActionBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState CompensationDataBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState BenefitsDataBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState PayrollDataBoundaryState { get; init; } = EmploymentChangeReadinessState.Blocked;
    public EmploymentChangeReadinessState DocumentDependencyState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public EmploymentChangeReadinessState NotificationDependencyState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public EmploymentChangeReadinessState ConsentPreconditionState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public EmploymentChangeReadinessState DataMinimizationState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public EmploymentChangeReadinessState RetentionPolicyState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public EmploymentChangeReadinessState EvidencePolicyState { get; init; } = EmploymentChangeReadinessState.Deferred;
    public IReadOnlyDictionary<string, EmploymentChangeReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, EmploymentChangeReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long EmploymentChangeReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record EmploymentChangeReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    EmploymentChangeReadinessState EmploymentChangeReadinessState,
    EmploymentChangeReadinessState ChangeLifecycleBoundaryState,
    EmploymentChangeReadinessState TransferBoundaryState,
    EmploymentChangeReadinessState PromotionBoundaryState,
    EmploymentChangeReadinessState ApprovalBoundaryState,
    EmploymentChangeReadinessState PositionAssignmentBoundaryState,
    EmploymentChangeReadinessState EmployeeActionBoundaryState,
    EmploymentChangeReadinessState ManagerActionBoundaryState,
    EmploymentChangeReadinessState CompensationDataBoundaryState,
    EmploymentChangeReadinessState BenefitsDataBoundaryState,
    EmploymentChangeReadinessState PayrollDataBoundaryState,
    EmploymentChangeReadinessState DocumentDependencyState,
    EmploymentChangeReadinessState NotificationDependencyState,
    EmploymentChangeReadinessState ConsentPreconditionState,
    EmploymentChangeReadinessState DataMinimizationState,
    EmploymentChangeReadinessState RetentionPolicyState,
    EmploymentChangeReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, EmploymentChangeReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long EmploymentChangeReadinessVersion,
    string? DeferredReason);

public sealed record EmploymentChangeReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    EmploymentChangeReadinessState EmploymentChangeReadinessState,
    EmploymentChangeReadinessState ChangeLifecycleBoundaryState,
    EmploymentChangeReadinessState ApprovalBoundaryState,
    EmploymentChangeReadinessState PositionAssignmentBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record EmploymentChangeAuditMetadataDto(
    Guid Id,
    string Code,
    EmploymentChangeReadinessState RetentionPolicyState,
    EmploymentChangeReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class EmploymentChangeMapper
{
    public static EmploymentChangeReadinessDto ToDto(EmploymentChangeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EmploymentChangeReadinessState,
            entity.ChangeLifecycleBoundaryState,
            entity.TransferBoundaryState,
            entity.PromotionBoundaryState,
            entity.ApprovalBoundaryState,
            entity.PositionAssignmentBoundaryState,
            entity.EmployeeActionBoundaryState,
            entity.ManagerActionBoundaryState,
            entity.CompensationDataBoundaryState,
            entity.BenefitsDataBoundaryState,
            entity.PayrollDataBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.EmploymentChangeReadinessVersion,
            entity.DeferredReason);

    public static EmploymentChangeReadinessListItemDto ToListItem(EmploymentChangeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EmploymentChangeReadinessState,
            entity.ChangeLifecycleBoundaryState,
            entity.ApprovalBoundaryState,
            entity.PositionAssignmentBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static EmploymentChangeAuditMetadataDto ToAuditMetadata(EmploymentChangeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
