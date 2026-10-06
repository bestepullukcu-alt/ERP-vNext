using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
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

    /// <summary>
    /// BL-484 — the same marks for a whole page of weeks, keyed by week id. The page's rows, open approval tasks and
    /// midnight-cut segments are ONE read each, whatever the page size; the working calendar is asked once per distinct
    /// (person, week). Each week's marks are exactly what <see cref="MarksAsync"/> computes for it. READ ONLY.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ApprovalWeekMarks>> MarksForWeeksAsync(
        IReadOnlyList<TimesheetWeek> weeks, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 T2b — the approver's facts about a submitted week, computed on read, never stored:
/// <list type="bullet">
/// <item>the OPEN MOD-0023 approval task of the current submission — its id IS the work-item id the Task Center approves
/// and rejects through, so the approvals page uses the same action path (no second approve route, D6);</item>
/// <item>the marks a bulk approval must not skip: days a timer was cut at local midnight (D3), minutes outside working
/// hours (D3), time on a holiday (D13). The 11-hour days stay <see cref="TimesheetWeek.FlaggedDates"/>.</item>
/// </list>
/// <para>BL-484 — one week (<see cref="MarksAsync"/>) and a page of weeks (<see cref="MarksForWeeksAsync"/>) differ only in
/// HOW the facts are read; both hand them to the same <see cref="Compose"/>, so the two cannot answer differently.</para>
/// </summary>
public sealed class ApprovalWeekFacts : IApprovalWeekFacts
{
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly ITimerSegmentRepository _segments;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly ITimeEntryRepository _entries;

    public ApprovalWeekFacts(
        IApprovalTaskRepository approvalTasks, ITimerSegmentRepository segments, IWorkingHoursProvider workingHours,
        ITimeEntryRepository entries)
    {
        _approvalTasks = approvalTasks;
        _segments = segments;
        _workingHours = workingHours;
        _entries = entries;
    }

    public async Task<ApprovalWeekMarks> MarksAsync(
        TimesheetWeek week, IReadOnlyList<TimeEntryRow> rows, IReadOnlyList<WorkingDay>? days, CancellationToken ct = default)
    {
        var task = week.WorkflowInstanceId is { } instanceId
            ? await _approvalTasks.GetActiveByInstanceIdAsync(instanceId, ct)
            : null;

        var segments = await _segments.ListForWeekAsync(week.UserId, week.WeekKey, ct);

        days ??= (await _workingHours.GetWorkingWindowsAsync(week.UserId, week.WeekStartDate, week.WeekStartDate.AddDays(6), ct)).Days;
        return Compose(task, segments, days, rows);
    }

    public async Task<IReadOnlyDictionary<Guid, ApprovalWeekMarks>> MarksForWeeksAsync(
        IReadOnlyList<TimesheetWeek> weeks, CancellationToken ct = default)
    {
        if (weeks.Count == 0)
        {
            return new Dictionary<Guid, ApprovalWeekMarks>();
        }

        // Rows: one read for every week on the page.
        var rowsByWeek = (await _entries.ListByWeekIdsAsync(weeks.Select(w => w.Id).Distinct().ToList(), ct))
            .GroupBy(r => r.TimesheetWeekId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeEntryRow>)g.ToList());

        // Open approval tasks: one read, newest first — the first one listed per instance is the single read's answer.
        var instanceIds = weeks.Select(w => w.WorkflowInstanceId).OfType<Guid>().Distinct().ToList();
        var taskByInstance = new Dictionary<Guid, ApprovalTask>();
        foreach (var task in await _approvalTasks.ListActiveByInstanceIdsAsync(instanceIds, ct))
        {
            taskByInstance.TryAdd(task.WorkflowInstanceId, task);
        }

        // Midnight-cut segments: one read over the page's exact (person, week) pairs.
        var segmentsByWeek = (await _segments.ListClosedAtMidnightForWeeksAsync(
                weeks.Select(w => (w.UserId, w.WeekKey)).Distinct().ToList(), ct))
            .GroupBy(s => (s.UserId, s.WeekKey))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimerSegment>)g.ToList());

        // The working calendar is per person (their unit and legal entity): one question for the page's distinct
        // (person, week) pairs — the provider reads the tenant, the seats, the positions and the units once for all.
        var calendar = await _workingHours.GetWorkingWindowsForManyAsync(
            weeks.Select(w => new WorkingHoursRequest(w.UserId, w.WeekStartDate, w.WeekStartDate.AddDays(6))).Distinct().ToList(), ct);
        var daysByWeek = calendar.ToDictionary(
            answer => (answer.Key.UserId, WeekStart: answer.Key.From), answer => answer.Value.Days);

        var marks = new Dictionary<Guid, ApprovalWeekMarks>();
        foreach (var week in weeks)
        {
            marks[week.Id] = Compose(
                week.WorkflowInstanceId is { } instanceId && taskByInstance.TryGetValue(instanceId, out var task) ? task : null,
                segmentsByWeek.TryGetValue((week.UserId, week.WeekKey), out var segments) ? segments : [],
                daysByWeek[(week.UserId, week.WeekStartDate)],
                rowsByWeek.TryGetValue(week.Id, out var rows) ? rows : []);
        }

        return marks;
    }

    /// <summary>The marks from the facts, however they were read. <paramref name="segments"/> may hold any of the week's
    /// segments; only the ones a midnight close ended count.</summary>
    private static ApprovalWeekMarks Compose(
        ApprovalTask? task, IReadOnlyList<TimerSegment> segments, IReadOnlyList<WorkingDay> days, IReadOnlyList<TimeEntryRow> rows)
    {
        var autoClosed = segments
            .Where(s => s.StopReason == TimerStopReason.LocalMidnight)
            .Select(s => s.LocalDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

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
