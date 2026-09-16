using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class TimeAttendanceLeaveReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TimeAttendanceLeaveReadinessState TimeAttendanceLeaveReadinessState { get; set; }
    public TimeAttendanceLeaveReadinessState TimesheetIntakeBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState AttendanceSyncBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState LeaveRequestBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState LeaveBalanceBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState ScheduleConsumptionBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState AutomatedDecisionBoundaryState { get; set; }
    public TimeAttendanceLeaveReadinessState TimeAttendanceSourceDependencyState { get; set; }
    public TimeAttendanceLeaveReadinessState LeaveSourceDependencyState { get; set; }
    public TimeAttendanceLeaveReadinessState DocumentDependencyState { get; set; }
    public TimeAttendanceLeaveReadinessState NotificationDependencyState { get; set; }
    public TimeAttendanceLeaveReadinessState ConsentPreconditionState { get; set; }
    public TimeAttendanceLeaveReadinessState DataMinimizationState { get; set; }
    public TimeAttendanceLeaveReadinessState RetentionPolicyState { get; set; }
    public TimeAttendanceLeaveReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, TimeAttendanceLeaveReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long TimeAttendanceLeaveReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
