using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding;

public class EmployeeOnboardingReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EmployeeOnboardingReadinessState OnboardingReadinessState { get; init; } = EmployeeOnboardingReadinessState.Draft;
    public EmployeeOnboardingReadinessState LifecycleBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Blocked;
    public EmployeeOnboardingReadinessState ChecklistBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Blocked;
    public EmployeeOnboardingReadinessState ManagerActionBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Blocked;
    public EmployeeOnboardingReadinessState EmployeeActionBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Blocked;
    public EmployeeOnboardingReadinessState CandidateTransitionBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState IdentityProvisioningBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState AccessProvisioningBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState DeviceEquipmentProvisioningBoundaryState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState DocumentDependencyState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState NotificationDependencyState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState ConsentPreconditionState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState DataMinimizationState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState RetentionPolicyState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public EmployeeOnboardingReadinessState EvidencePolicyState { get; init; } = EmployeeOnboardingReadinessState.Deferred;
    public IReadOnlyDictionary<string, EmployeeOnboardingReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, EmployeeOnboardingReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long OnboardingReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record EmployeeOnboardingReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    EmployeeOnboardingReadinessState OnboardingReadinessState,
    EmployeeOnboardingReadinessState LifecycleBoundaryState,
    EmployeeOnboardingReadinessState ChecklistBoundaryState,
    EmployeeOnboardingReadinessState ManagerActionBoundaryState,
    EmployeeOnboardingReadinessState EmployeeActionBoundaryState,
    EmployeeOnboardingReadinessState CandidateTransitionBoundaryState,
    EmployeeOnboardingReadinessState IdentityProvisioningBoundaryState,
    EmployeeOnboardingReadinessState AccessProvisioningBoundaryState,
    EmployeeOnboardingReadinessState DeviceEquipmentProvisioningBoundaryState,
    EmployeeOnboardingReadinessState DocumentDependencyState,
    EmployeeOnboardingReadinessState NotificationDependencyState,
    EmployeeOnboardingReadinessState ConsentPreconditionState,
    EmployeeOnboardingReadinessState DataMinimizationState,
    EmployeeOnboardingReadinessState RetentionPolicyState,
    EmployeeOnboardingReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, EmployeeOnboardingReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long OnboardingReadinessVersion,
    string? DeferredReason);

public sealed record EmployeeOnboardingReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    EmployeeOnboardingReadinessState OnboardingReadinessState,
    EmployeeOnboardingReadinessState LifecycleBoundaryState,
    EmployeeOnboardingReadinessState ChecklistBoundaryState,
    EmployeeOnboardingReadinessState ManagerActionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record EmployeeOnboardingAuditMetadataDto(
    Guid Id,
    string Code,
    EmployeeOnboardingReadinessState RetentionPolicyState,
    EmployeeOnboardingReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class EmployeeOnboardingMapper
{
    public static EmployeeOnboardingReadinessDto ToDto(EmployeeOnboardingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.OnboardingReadinessState,
            entity.LifecycleBoundaryState,
            entity.ChecklistBoundaryState,
            entity.ManagerActionBoundaryState,
            entity.EmployeeActionBoundaryState,
            entity.CandidateTransitionBoundaryState,
            entity.IdentityProvisioningBoundaryState,
            entity.AccessProvisioningBoundaryState,
            entity.DeviceEquipmentProvisioningBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.OnboardingReadinessVersion,
            entity.DeferredReason);

    public static EmployeeOnboardingReadinessListItemDto ToListItem(EmployeeOnboardingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.OnboardingReadinessState,
            entity.LifecycleBoundaryState,
            entity.ChecklistBoundaryState,
            entity.ManagerActionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static EmployeeOnboardingAuditMetadataDto ToAuditMetadata(EmployeeOnboardingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
