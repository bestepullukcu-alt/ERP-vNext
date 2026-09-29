using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What one recomputation did.</summary>
public enum TimerDraftOutcome
{
    /// <summary>The draft already said this — nothing written.</summary>
    Unchanged = 0,

    /// <summary>The draft row was created, changed or removed.</summary>
    Written = 1,

    /// <summary>The day's week is not an open Draft (submitted, approved, outside the window): nothing written — the
    /// minutes are reported as timer time outside an open week (§13), and the segments are minimised at once (v2 F3).</summary>
    WeekNotWritable = 2
}

public interface ITimerDraftWriter
{
    /// <summary>Recomputes the person's timer draft row for one (local day, task-or-category) from its closed segments.</summary>
    Task<TimerDraftOutcome> ApplyAsync(Guid userId, DateOnly localDate, Guid? taskItemId, string? categoryCode, CancellationToken ct = default);

    /// <summary>v2 F5 — recomputes EVERY timer draft row of one person's week from the segments: every (day, target) a closed
    /// segment or a timer row names. Idempotent; run at every close, at the START of a save and a submit of that week, and
    /// in the midnight job, so a draft a failed close never wrote is written by the next of them.</summary>
    Task ApplyWeekAsync(Guid userId, string weekKey, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D3 / A2 / Z-2 — the ONLY writer of <see cref="TimeEntrySource.Timer"/> rows. Called after a segment
/// closes (by whichever path closed it). The row is a DRAFT: it lands in the week's open Draft revision, is never
/// submitted by itself, and the person still has to submit the week (Z-2).
///
/// <para><b>A recomputation, never an increment.</b> The row's minutes are the day's closed segments for that target,
/// summed and rounded to 15 (ties up); under 8 minutes there is no row (A2). Running it twice writes the same row.</para>
///
/// <para><b>Only into an open Draft.</b> A day of a submitted or approved week gets no row — the segments stay, and the
/// week read reports them as "time outside an open week" (pack §13); a locked revision is never changed behind the
/// person's back. A row the person edited (<see cref="TimeEntryRow.EditedFromTimer"/>) is theirs and is left alone.</para>
/// </summary>
public sealed class TimerDraftWriter : ITimerDraftWriter
{
    private const int MaxAttempts = 5;

    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimerSegmentRepository _segments;
    private readonly ITenantContext _tenantContext;

    public TimerDraftWriter(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ITimerSegmentRepository segments,
        ITenantContext tenantContext)
    {
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _segments = segments;
        _tenantContext = tenantContext;
    }

    public async Task ApplyWeekAsync(Guid userId, string weekKey, CancellationToken ct = default)
    {
        if (!WeekCalendar.TryParse(weekKey, out var monday))
        {
            return;
        }

        var cells = (await _segments.ListForWeekAsync(userId, weekKey, ct))
            .Where(s => !s.IsRunning)
            .Select(s => (s.LocalDate, s.TaskItemId, s.CategoryCode))
            .ToHashSet();
        var open = (await _reader.LoadAsync(userId, monday, ct)).Open;
        if (open is not null)
        {
            foreach (var row in (await _entries.ListByWeekAsync(open.Id, ct)).Where(r => r.Source == TimeEntrySource.Timer))
            {
                cells.Add((row.LocalDate, row.TaskItemId, row.CategoryCode));
            }
        }

        foreach (var (date, task, category) in cells.OrderBy(c => c.LocalDate))
        {
            await ApplyAsync(userId, date, task, category, ct);
        }
    }

    public async Task<TimerDraftOutcome> ApplyAsync(
        Guid userId, DateOnly localDate, Guid? taskItemId, string? categoryCode, CancellationToken ct = default)
    {
        var closed = (await _segments.ListClosedForDayAsync(userId, localDate, ct))
            .Where(s => s.TaskItemId == taskItemId && s.CategoryCode == categoryCode)
            .ToList();
        var minutes = TimerRules.DraftMinutes(closed.Sum(s => (long)s.DurationSeconds));
        var outside = closed.Sum(s => s.OutsideWorkingMinutes);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var context = await _reader.LoadAsync(userId, WeekCalendar.MondayOf(localDate), ct);
            if (TimesheetRules.WriteRefusal(context) is not null)
            {
                // Submitted / approved / outside the window: the segments stay and are reported, no row is written.
                return TimerDraftOutcome.WeekNotWritable;
            }

            var week = context.Open;
            if (week is null)
            {
                if (minutes == 0)
                {
                    return TimerDraftOutcome.Unchanged; // nothing to count and no draft to touch — do not open a week
                }

                week = TimesheetRules.NewRevision(context, _tenantContext.TenantId, 1);
                if (!await _weeks.TryCreateAsync(week, ct))
                {
                    continue; // another writer opened the week first — read it again
                }
            }

            var rows = (await _entries.ListByWeekAsync(week.Id, ct)).ToList();
            var row = rows.FirstOrDefault(r => r.Source == TimeEntrySource.Timer && r.LocalDate == localDate
                                               && r.TaskItemId == taskItemId && r.CategoryCode == categoryCode);
            if (row is { EditedFromTimer: true })
            {
                return TimerDraftOutcome.Unchanged; // the person's own number now
            }

            if ((row is null && minutes == 0) || (row is not null && row.DurationMinutes == minutes && row.OutsideWorkingMinutes == outside))
            {
                return TimerDraftOutcome.Unchanged;
            }

            // Claim the week first (compare-and-set on its version), the same order the manual save uses: a concurrent
            // save of the week loses cleanly instead of both writing.
            var after = rows.Where(r => r.Id != row?.Id).ToList();
            if (minutes > 0)
            {
                after.Add(new TimeEntryRow
                {
                    TenantId = _tenantContext.TenantId, TimesheetWeekId = week.Id, UserId = userId, WeekKey = week.WeekKey,
                    LocalDate = localDate, DurationMinutes = minutes
                });
            }

            var dayTotals = TimesheetRules.DayTotals(after);
            week.TotalMinutes = dayTotals.Values.Sum();
            week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
            if (!await _weeks.UpdateAsync(week, week.Version, ct))
            {
                continue;
            }

            if (minutes == 0)
            {
                if (row is not null)
                {
                    await _entries.SoftDeleteAsync([row.Id], ct);
                }
            }
            else if (row is null)
            {
                await _entries.CreateAsync(new TimeEntryRow
                {
                    TenantId = _tenantContext.TenantId,
                    TimesheetWeekId = week.Id,
                    UserId = userId,
                    WeekKey = week.WeekKey,
                    LocalDate = localDate,
                    DurationMinutes = minutes,
                    TaskItemId = taskItemId,
                    CategoryCode = categoryCode,
                    Source = TimeEntrySource.Timer,
                    OutsideWorkingMinutes = outside,
                    CreatedBy = userId.ToString()
                }, ct);
            }
            else
            {
                row.DurationMinutes = minutes;
                row.OutsideWorkingMinutes = outside;
                row.UpdatedBy = userId.ToString();
                await _entries.UpdateAsync(row, ct);
            }

            return TimerDraftOutcome.Written;
        }

        throw new InvalidOperationException($"The timer draft of {localDate} kept colliding with other writes to its week.");
    }
}
