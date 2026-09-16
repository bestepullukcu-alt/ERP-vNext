using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement;

public class HrCaseManagementReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HrCaseManagementReadinessState HrCaseManagementReadinessState { get; init; } = HrCaseManagementReadinessState.Draft;
    public HrCaseManagementReadinessState CaseIntakeBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState CaseTriageBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState InvestigationTrackingBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState DisciplinaryActionBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState ResolutionClosureBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState AutomatedDecisionBoundaryState { get; init; } = HrCaseManagementReadinessState.Blocked;
    public HrCaseManagementReadinessState EmployeeRecordDependencyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState SensitiveAccessPolicyDependencyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState DocumentDependencyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState NotificationDependencyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState ConsentPreconditionState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState DataMinimizationState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState RetentionPolicyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public HrCaseManagementReadinessState EvidencePolicyState { get; init; } = HrCaseManagementReadinessState.Deferred;
    public IReadOnlyDictionary<string, HrCaseManagementReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HrCaseManagementReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HrCaseManagementReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HrCaseManagementReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrCaseManagementReadinessState HrCaseManagementReadinessState,
    HrCaseManagementReadinessState CaseIntakeBoundaryState,
    HrCaseManagementReadinessState CaseTriageBoundaryState,
    HrCaseManagementReadinessState InvestigationTrackingBoundaryState,
    HrCaseManagementReadinessState DisciplinaryActionBoundaryState,
    HrCaseManagementReadinessState ResolutionClosureBoundaryState,
    HrCaseManagementReadinessState AutomatedDecisionBoundaryState,
    HrCaseManagementReadinessState EmployeeRecordDependencyState,
    HrCaseManagementReadinessState SensitiveAccessPolicyDependencyState,
    HrCaseManagementReadinessState DocumentDependencyState,
    HrCaseManagementReadinessState NotificationDependencyState,
    HrCaseManagementReadinessState ConsentPreconditionState,
    HrCaseManagementReadinessState DataMinimizationState,
    HrCaseManagementReadinessState RetentionPolicyState,
    HrCaseManagementReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HrCaseManagementReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HrCaseManagementReadinessVersion,
    string? DeferredReason);

public sealed record HrCaseManagementReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrCaseManagementReadinessState HrCaseManagementReadinessState,
    HrCaseManagementReadinessState CaseIntakeBoundaryState,
    HrCaseManagementReadinessState EmployeeRecordDependencyState,
    HrCaseManagementReadinessState DisciplinaryActionBoundaryState,
    HrCaseManagementReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HrCaseManagementAuditMetadataDto(
    Guid Id,
    string Code,
    HrCaseManagementReadinessState RetentionPolicyState,
    HrCaseManagementReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HrCaseManagementMapper
{
    public static HrCaseManagementReadinessDto ToDto(HrCaseManagementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrCaseManagementReadinessState,
            entity.CaseIntakeBoundaryState,
            entity.CaseTriageBoundaryState,
            entity.InvestigationTrackingBoundaryState,
            entity.DisciplinaryActionBoundaryState,
            entity.ResolutionClosureBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.EmployeeRecordDependencyState,
            entity.SensitiveAccessPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HrCaseManagementReadinessVersion,
            entity.DeferredReason);

    public static HrCaseManagementReadinessListItemDto ToListItem(HrCaseManagementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrCaseManagementReadinessState,
            entity.CaseIntakeBoundaryState,
            entity.EmployeeRecordDependencyState,
            entity.DisciplinaryActionBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HrCaseManagementAuditMetadataDto ToAuditMetadata(HrCaseManagementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
