namespace Diten.Platform.Domain.Enums;

public enum TimeAttendanceEventType
{
    TimeEntry = 1,
    ClockIn = 2,
    ClockOut = 3,
    BreakStart = 4,
    BreakEnd = 5,
    AttendanceAdjustment = 6,
    LeaveRequest = 90,
    PayrollRun = 91,
    RosterOptimization = 92
}
