using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases;

public class OffboardingCaseCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public Guid EmployeeProjectionId { get; init; }
    public Guid? AssignmentOverlayId { get; init; }
    public string ExitReasonCode { get; init; } = string.Empty;
    public string ExitTypeCode { get; init; } = string.Empty;
    public DateTimeOffset? NoticeDate { get; init; }
    public DateTimeOffset PlannedExitDate { get; init; }
    public DateTimeOffset? ActualExitDate { get; init; }
    public OffboardingState OffboardingState { get; init; } = OffboardingState.Draft;
    public OffboardingChecklistState ChecklistState { get; init; } = OffboardingChecklistState.NotStarted;
    public OffboardingDependencyDecisionState DependencyDecisionState { get; init; } = OffboardingDependencyDecisionState.Deferred;
    public OffboardingTepHandoffState TepHandoffState { get; init; } = OffboardingTepHandoffState.NotRequired;
    public string? TepHandoffReferenceKey { get; init; }
    public string SourceContractVersion { get; init; } = string.Empty;
    public long OffboardingVersion { get; init; } = 1;
}

public sealed class OffboardingCaseUpdateRequest : OffboardingCaseCreateRequest;

public sealed class OffboardingCaseReviewRequest
{
    public OffboardingState OffboardingState { get; init; } = OffboardingState.ReviewRequired;
    public OffboardingDependencyDecisionState DependencyDecisionState { get; init; } = OffboardingDependencyDecisionState.Deferred;
    public string SourceContractVersion { get; init; } = string.Empty;
    public long OffboardingVersion { get; init; } = 1;
}

public sealed class OffboardingCaseHandoffRequest
{
    public OffboardingTepHandoffState TepHandoffState { get; init; } = OffboardingTepHandoffState.Planned;
    public string? TepHandoffReferenceKey { get; init; }
    public string SourceContractVersion { get; init; } = string.Empty;
    public long OffboardingVersion { get; init; } = 1;
}

public sealed record OffboardingCaseDto(
    Guid Id,
    string Code,
    Guid EmployeeProjectionId,
    Guid? AssignmentOverlayId,
    string ExitReasonCode,
    string ExitTypeCode,
    DateTimeOffset? NoticeDate,
    DateTimeOffset PlannedExitDate,
    DateTimeOffset? ActualExitDate,
    OffboardingState OffboardingState,
    OffboardingChecklistState ChecklistState,
    OffboardingSensitiveAccessDecisionState SensitiveAccessDecisionState,
    OffboardingDependencyDecisionState DependencyDecisionState,
    OffboardingTepHandoffState TepHandoffState,
    string? TepHandoffReferenceKey,
    string SourceContractVersion,
    long OffboardingVersion,
    DateTimeOffset? LastDependencyEvaluatedAt,
    DateTimeOffset? LastHandoffPlannedAt,
    string? DeferredReason);

public sealed record OffboardingCaseListItemDto(
    Guid Id,
    string Code,
    Guid EmployeeProjectionId,
    Guid? AssignmentOverlayId,
    OffboardingState OffboardingState,
    OffboardingChecklistState ChecklistState,
    OffboardingSensitiveAccessDecisionState SensitiveAccessDecisionState,
    OffboardingDependencyDecisionState DependencyDecisionState,
    OffboardingTepHandoffState TepHandoffState,
    DateTimeOffset PlannedExitDate);

public sealed record OffboardingCaseHealthDto(string OwnerKey, string Status);

internal static class OffboardingCaseMapper
{
    public static OffboardingCaseDto ToDto(OffboardingCase entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.EmployeeProjectionId,
            entity.AssignmentOverlayId,
            entity.ExitReasonCode,
            entity.ExitTypeCode,
            entity.NoticeDate,
            entity.PlannedExitDate,
            entity.ActualExitDate,
            entity.OffboardingState,
            entity.ChecklistState,
            entity.SensitiveAccessDecisionState,
            entity.DependencyDecisionState,
            entity.TepHandoffState,
            entity.TepHandoffReferenceKey,
            entity.SourceContractVersion,
            entity.OffboardingVersion,
            entity.LastDependencyEvaluatedAt,
            entity.LastHandoffPlannedAt,
            entity.DeferredReason);

    public static OffboardingCaseListItemDto ToListItem(OffboardingCase entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.EmployeeProjectionId,
            entity.AssignmentOverlayId,
            entity.OffboardingState,
            entity.ChecklistState,
            entity.SensitiveAccessDecisionState,
            entity.DependencyDecisionState,
            entity.TepHandoffState,
            entity.PlannedExitDate);
}
