using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// Everything a week handler needs to decide anything: the person's revisions of the week, which one is open and
/// which one is in force, the tenant-zone calendar of its seven days (targets, holidays) and the person's local today.
/// </summary>
public sealed record TimesheetWeekContext(
    Guid UserId,
    string WeekKey,
    DateOnly Monday,
    TimeZoneInfo Zone,
    DateOnly LocalToday,
    IReadOnlyList<TimesheetWeek> Revisions,
    IReadOnlyList<WorkingDay> Days)
{
    /// <summary>The Draft or Submitted revision, if any (at most one — a unique index says so).</summary>
    public TimesheetWeek? Open => Revisions.FirstOrDefault(r => r.IsOpen);

    /// <summary>The approved revision in force, if any.</summary>
    public TimesheetWeek? InForce => Revisions.FirstOrDefault(r => r.InForce);

    /// <summary>What the person looks at: the open revision, else the one in force, else the latest.</summary>
    public TimesheetWeek? Current => Open ?? InForce ?? Revisions.LastOrDefault();

    public bool InsideEditWindow => WeekCalendar.IsInsideEditWindow(Monday, LocalToday);

    public DateOnly Sunday => Monday.AddDays(6);
}

public interface ITimesheetWeekReader
{
    Task<TimesheetWeekContext> LoadAsync(Guid userId, DateOnly monday, CancellationToken ct = default);
}

public sealed class TimesheetWeekReader : ITimesheetWeekReader
{
    private readonly ITimesheetWeekRepository _weeks;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly TimeProvider _clock;

    public TimesheetWeekReader(ITimesheetWeekRepository weeks, IWorkingHoursProvider workingHours, TimeProvider clock)
    {
        _weeks = weeks;
        _workingHours = workingHours;
        _clock = clock;
    }

    public async Task<TimesheetWeekContext> LoadAsync(Guid userId, DateOnly monday, CancellationToken ct = default)
    {
        var weekKey = WeekCalendar.KeyOf(monday);
        var hours = await _workingHours.GetWorkingWindowsAsync(userId, monday, monday.AddDays(6), ct);
        var localToday = WeekCalendar.LocalDateOf(_clock.GetUtcNow(), hours.TimeZone);
        var revisions = await _weeks.ListRevisionsAsync(userId, weekKey, ct);
        return new TimesheetWeekContext(userId, weekKey, monday, hours.TimeZone, localToday, revisions, hours.Days);
    }
}

/// <summary>The write rules every week command shares, so submit, save and correction cannot drift apart.</summary>
public static class TimesheetRules
{
    /// <summary>
    /// Why the person may NOT write to the week's open draft right now, or null when they may (pack §12 row "any write
    /// to a week"): the revision must be a Draft, and inside the edit window unless a time admin reopened it or it is a
    /// correction (corrections are not bound by the window in v1 — §20).
    /// </summary>
    public static string? WriteRefusal(TimesheetWeekContext context)
    {
        var open = context.Open;
        if (open is null)
        {
            // No open revision: either nothing exists yet (revision 1 will be created) or the week is approved/locked.
            if (context.Revisions.Count > 0)
            {
                return TimeEntryReasonCodes.WeekNotOpen;
            }

            return context.InsideEditWindow ? null : TimeEntryReasonCodes.WeekOutsideEditWindow;
        }

        if (open.Status != TimesheetWeekStatus.Draft)
        {
            return TimeEntryReasonCodes.WeekNotOpen;
        }

        if (!context.InsideEditWindow && !open.ReopenActive && open.CorrectionOfRevision is null)
        {
            return TimeEntryReasonCodes.WeekOutsideEditWindow;
        }

        return null;
    }

    /// <summary>Per-day totals of a set of rows.</summary>
    public static IReadOnlyDictionary<DateOnly, int> DayTotals(IEnumerable<TimeEntryRow> entries)
        => entries.GroupBy(e => e.LocalDate).ToDictionary(g => g.Key, g => g.Sum(e => e.DurationMinutes));

    /// <summary>A3 — days above the 660-minute flag threshold, in date order.</summary>
    public static List<DateOnly> FlaggedDates(IReadOnlyDictionary<DateOnly, int> dayTotals)
        => dayTotals.Where(d => d.Value > TimeEntryLimits.FlagDayMinutes).Select(d => d.Key).OrderBy(d => d).ToList();

    /// <summary>One row as both the person and the approver read it — including whether it was corrected from its captured
    /// value, and what that value was (v3 G2).</summary>
    public static TimeEntryDto ToDto(TimeEntryRow entry) => new(
        entry.Id, entry.LocalDate, entry.DurationMinutes, entry.TaskItemId, entry.CategoryCode,
        entry.Source.ToString(), entry.Note,
        OutsideWorkingMinutes: entry.OutsideWorkingMinutes,
        EditedFromTimer: entry.EditedFromTimer,
        SourceRef: entry.SourceRef,
        CapturedMinutes: entry.CapturedMinutes);

    public static IReadOnlyList<TimesheetDayDto> Days(
        IReadOnlyList<WorkingDay> days, DateOnly monday, IReadOnlyDictionary<DateOnly, int> dayTotals, DateOnly localToday)
    {
        var byDate = days.ToDictionary(d => d.Date);
        return WeekCalendar.DaysOf(monday).Select(date =>
        {
            byDate.TryGetValue(date, out var day);
            var recorded = dayTotals.TryGetValue(date, out var minutes) ? minutes : 0;
            return new TimesheetDayDto(
                date,
                day?.DayKind ?? WorkingDayKinds.WorkingDay,
                day?.IsHalfDay ?? false,
                day?.HolidayName,
                day?.TargetMinutes ?? 0,
                recorded,
                recorded > TimeEntryLimits.FlagDayMinutes,
                date > localToday,
                day?.CalendarUnresolved ?? false);
        }).ToList();
    }

    /// <summary>A fresh open Draft revision of the context's week.</summary>
    public static TimesheetWeek NewRevision(TimesheetWeekContext context, Guid tenantId, int revisionNumber, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenantId,
        UserId = context.UserId,
        WeekKey = context.WeekKey,
        WeekStartDate = context.Monday,
        TimeZoneId = context.Zone.Id,
        RevisionNumber = revisionNumber,
        Status = TimesheetWeekStatus.Draft,
        IsOpen = true
    };

    public static TimesheetWeekMutationDto ToMutation(TimesheetWeek week) => new(
        week.Id, week.WeekKey, week.RevisionNumber, week.Status.ToString(), week.Version,
        week.FlaggedDates, week.TotalMinutes);
}
