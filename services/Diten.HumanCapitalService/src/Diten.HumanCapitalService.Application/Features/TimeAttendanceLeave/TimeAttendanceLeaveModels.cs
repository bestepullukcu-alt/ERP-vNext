using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave;

public class TimeAttendanceLeaveReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public TimeAttendanceLeaveReadinessState TimeAttendanceLeaveReadinessState { get; init; } = TimeAttendanceLeaveReadinessState.Draft;
    public TimeAttendanceLeaveReadinessState TimesheetIntakeBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState AttendanceSyncBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState LeaveRequestBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState LeaveBalanceBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState ScheduleConsumptionBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState AutomatedDecisionBoundaryState { get; init; } = TimeAttendanceLeaveReadinessState.Blocked;
    public TimeAttendanceLeaveReadinessState TimeAttendanceSourceDependencyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState LeaveSourceDependencyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState DocumentDependencyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState NotificationDependencyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState ConsentPreconditionState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState DataMinimizationState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState RetentionPolicyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public TimeAttendanceLeaveReadinessState EvidencePolicyState { get; init; } = TimeAttendanceLeaveReadinessState.Deferred;
    public IReadOnlyDictionary<string, TimeAttendanceLeaveReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, TimeAttendanceLeaveReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long TimeAttendanceLeaveReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record TimeAttendanceLeaveReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    TimeAttendanceLeaveReadinessState TimeAttendanceLeaveReadinessState,
    TimeAttendanceLeaveReadinessState TimesheetIntakeBoundaryState,
    TimeAttendanceLeaveReadinessState AttendanceSyncBoundaryState,
    TimeAttendanceLeaveReadinessState LeaveRequestBoundaryState,
    TimeAttendanceLeaveReadinessState LeaveBalanceBoundaryState,
    TimeAttendanceLeaveReadinessState ScheduleConsumptionBoundaryState,
    TimeAttendanceLeaveReadinessState AutomatedDecisionBoundaryState,
    TimeAttendanceLeaveReadinessState TimeAttendanceSourceDependencyState,
    TimeAttendanceLeaveReadinessState LeaveSourceDependencyState,
    TimeAttendanceLeaveReadinessState DocumentDependencyState,
    TimeAttendanceLeaveReadinessState NotificationDependencyState,
    TimeAttendanceLeaveReadinessState ConsentPreconditionState,
    TimeAttendanceLeaveReadinessState DataMinimizationState,
    TimeAttendanceLeaveReadinessState RetentionPolicyState,
    TimeAttendanceLeaveReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, TimeAttendanceLeaveReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long TimeAttendanceLeaveReadinessVersion,
    string? DeferredReason);

public sealed record TimeAttendanceLeaveReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TimeAttendanceLeaveReadinessState TimeAttendanceLeaveReadinessState,
    TimeAttendanceLeaveReadinessState TimesheetIntakeBoundaryState,
    TimeAttendanceLeaveReadinessState TimeAttendanceSourceDependencyState,
    TimeAttendanceLeaveReadinessState LeaveBalanceBoundaryState,
    TimeAttendanceLeaveReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record TimeAttendanceLeaveAuditMetadataDto(
    Guid Id,
    string Code,
    TimeAttendanceLeaveReadinessState RetentionPolicyState,
    TimeAttendanceLeaveReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class TimeAttendanceLeaveMapper
{
    public static TimeAttendanceLeaveReadinessDto ToDto(TimeAttendanceLeaveReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TimeAttendanceLeaveReadinessState,
            entity.TimesheetIntakeBoundaryState,
            entity.AttendanceSyncBoundaryState,
            entity.LeaveRequestBoundaryState,
            entity.LeaveBalanceBoundaryState,
            entity.ScheduleConsumptionBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TimeAttendanceSourceDependencyState,
            entity.LeaveSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.TimeAttendanceLeaveReadinessVersion,
            entity.DeferredReason);

    public static TimeAttendanceLeaveReadinessListItemDto ToListItem(TimeAttendanceLeaveReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.TimeAttendanceLeaveReadinessState,
            entity.TimesheetIntakeBoundaryState,
            entity.TimeAttendanceSourceDependencyState,
            entity.LeaveBalanceBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static TimeAttendanceLeaveAuditMetadataDto ToAuditMetadata(TimeAttendanceLeaveReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
