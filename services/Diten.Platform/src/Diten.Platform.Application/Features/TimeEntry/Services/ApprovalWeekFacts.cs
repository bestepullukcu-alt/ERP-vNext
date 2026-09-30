using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What an approver's list row and read-only week carry beyond the stored week (T2b, U4).</summary>
public sealed record ApprovalWeekMarks(
    Guid? ApprovalTaskId,
    int? ApprovalTaskVersion,
    IReadOnlyList<DateOnly> AutoClosedDates,
    int OutsideWorkingMinutes,
    IReadOnlyList<DateOnly> HolidayDates);

public interface IApprovalWeekFacts
{
    /// <summary>The week's MOD-0023 approval task (the Task Center's work-item id) and its marks. READ ONLY.</summary>
    Task<ApprovalWeekMarks> MarksAsync(
        TimesheetWeek week, IReadOnlyList<TimeEntryRow> rows, IReadOnlyList<WorkingDay>? days, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 T2b — the approver's facts about a submitted week, computed on read, never stored:
/// <list type="bullet">
/// <item>the OPEN MOD-0023 approval task of the current submission — its id IS the work-item id the Task Center approves
/// and rejects through, so the approvals page uses the same action path (no second approve route, D6);</item>
/// <item>the marks a bulk approval must not skip: days a timer was cut at local midnight (D3), minutes outside working
/// hours (D3), time on a holiday (D13). The 11-hour days stay <see cref="TimesheetWeek.FlaggedDates"/>.</item>
/// </list>
/// </summary>
public sealed class ApprovalWeekFacts : IApprovalWeekFacts
{
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly ITimerSegmentRepository _segments;
    private readonly IWorkingHoursProvider _workingHours;

    public ApprovalWeekFacts(IApprovalTaskRepository approvalTasks, ITimerSegmentRepository segments, IWorkingHoursProvider workingHours)
    {
        _approvalTasks = approvalTasks;
        _segments = segments;
        _workingHours = workingHours;
    }

    public async Task<ApprovalWeekMarks> MarksAsync(
        TimesheetWeek week, IReadOnlyList<TimeEntryRow> rows, IReadOnlyList<WorkingDay>? days, CancellationToken ct = default)
    {
        var task = week.WorkflowInstanceId is { } instanceId
            ? await _approvalTasks.GetActiveByInstanceIdAsync(instanceId, ct)
            : null;

        var autoClosed = (await _segments.ListForWeekAsync(week.UserId, week.WeekKey, ct))
            .Where(s => s.StopReason == TimerStopReason.LocalMidnight)
            .Select(s => s.LocalDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        days ??= (await _workingHours.GetWorkingWindowsAsync(week.UserId, week.WeekStartDate, week.WeekStartDate.AddDays(6), ct)).Days;
        var holidays = days
            .Where(d => d.DayKind == WorkingDayKinds.Holiday || !string.IsNullOrWhiteSpace(d.HolidayName))
            .Select(d => d.Date)
            .ToHashSet();
        var onHolidays = rows.Where(r => r.DurationMinutes > 0 && holidays.Contains(r.LocalDate))
            .Select(r => r.LocalDate).Distinct().OrderBy(d => d).ToList();

        return new ApprovalWeekMarks(
            task?.Id,
            task?.Version,
            autoClosed,
            rows.Sum(r => r.OutsideWorkingMinutes),
            onHolidays);
    }

    /// <summary>
    /// A correction's difference from the in-force revision, row by row: a row is (day, task-or-category, source, meeting).
    /// Same minutes on both sides ⇒ not listed. Only minutes are compared — the approver decides on time, the note is shown
    /// on the row itself.
    /// </summary>
    public static IReadOnlyList<CorrectionChangeDto> Changes(IReadOnlyList<TimeEntryRow> previous, IReadOnlyList<TimeEntryRow> current)
    {
        static (DateOnly, Guid?, string?, TimeEntrySource, string?) Key(TimeEntryRow r)
            => (r.LocalDate, r.TaskItemId, r.CategoryCode, r.Source, r.Source == TimeEntrySource.Meeting ? r.SourceRef : null);

        var before = previous.GroupBy(Key).ToDictionary(g => g.Key, g => g.Sum(r => r.DurationMinutes));
        var after = current.GroupBy(Key).ToDictionary(g => g.Key, g => g.Sum(r => r.DurationMinutes));

        return before.Keys.Union(after.Keys)
            .Select(key => (key, was: before.TryGetValue(key, out var p) ? p : (int?)null, now: after.TryGetValue(key, out var c) ? c : (int?)null))
            .Where(x => x.was != x.now)
            .OrderBy(x => x.key.Item1).ThenBy(x => x.key.Item2).ThenBy(x => x.key.Item3)
            .Select(x => new CorrectionChangeDto(
                x.key.Item1, x.key.Item2, x.key.Item3, x.key.Item4.ToString(), x.key.Item5, x.was, x.now))
            .ToList();
    }
}
