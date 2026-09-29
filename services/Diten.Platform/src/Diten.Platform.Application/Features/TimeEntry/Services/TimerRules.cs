using Diten.Platform.Application.Features.WorkingHours;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// MOD-0280-FU01 (pack §4.7, §19.1, A2, D3) — the timer arithmetic, in ONE place, pure so it is tested without a clock
/// or a database.
/// </summary>
public static class TimerRules
{
    /// <summary>A switch can be undone for this long after it happened (§19.1).</summary>
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(60);

    /// <summary>A2 — per task-or-category per local day, a sum under this writes no draft row ("too short to count").</summary>
    public const int MinimumCountableSeconds = 8 * 60;

    private const int StepSeconds = TimeEntryLimits.StepMinutes * 60;

    /// <summary>
    /// A2 — the draft minutes of a day's summed timer seconds: nearest 15 minutes, ties UP (22.5 → 30); under 8 minutes
    /// nothing (0). 7 + 8 minutes = 15 → 15; 3 + 4 = 7 → 0.
    /// </summary>
    public static int DraftMinutes(long totalSeconds)
    {
        if (totalSeconds < MinimumCountableSeconds)
        {
            return 0;
        }

        // Integer arithmetic: a tie (x·900 + 450 seconds) rounds up, never through a floating-point half.
        var steps = (totalSeconds + StepSeconds / 2) / StepSeconds;
        return (int)Math.Max(1, steps) * TimeEntryLimits.StepMinutes;
    }

    /// <summary>A meeting's scheduled duration as a suggestion: the same rounding, the same floor.</summary>
    public static int SuggestedMinutes(DateTimeOffset startAt, DateTimeOffset endAt)
        => Math.Min(TimeEntryLimits.MaxRowMinutes, DraftMinutes((long)Math.Max(0, (endAt - startAt).TotalSeconds)));

    /// <summary>
    /// D3 — the UTC instant the tenant-local day AFTER <paramref name="localDate"/> begins: where a segment started on
    /// <paramref name="localDate"/> is cut at the latest. Computed from the zone's own rules, so a DST night is 23 or 25
    /// real hours long, never a fixed 24.
    /// </summary>
    public static DateTimeOffset LocalMidnightAfter(DateOnly localDate, TimeZoneInfo zone)
    {
        var local = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A zone whose clocks jump AT midnight has no 00:00 that day; the first valid minute after it is the cut.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    /// <summary>
    /// Where a running segment ends if it is closed now: now, or its local midnight if that already passed (D3). A segment
    /// never crosses the midnight of the day it started on.
    /// </summary>
    public static (DateTimeOffset StopAt, bool AtMidnight) StopPoint(
        DateTimeOffset startedAtUtc, DateOnly localDate, TimeZoneInfo zone, DateTimeOffset requestedStopUtc)
    {
        var midnight = LocalMidnightAfter(localDate, zone);
        var stop = requestedStopUtc < startedAtUtc ? startedAtUtc : requestedStopUtc;
        return stop >= midnight ? (midnight, true) : (stop, false);
    }

    /// <summary>Has the segment's local midnight passed at <paramref name="nowUtc"/>?</summary>
    public static bool PastLocalMidnight(DateOnly localDate, TimeZoneInfo zone, DateTimeOffset nowUtc)
        => nowUtc >= LocalMidnightAfter(localDate, zone);

    /// <summary>D3 — minutes of [start, stop) outside the day's working windows (a weekend or holiday has none, so all of it).</summary>
    public static int OutsideWorkingMinutes(DateTimeOffset startUtc, DateTimeOffset stopUtc, WorkingDay? day)
    {
        var total = Math.Max(0, (stopUtc - startUtc).TotalSeconds);
        var inside = (day?.Windows ?? [])
            .Select(w => Math.Max(0, (Min(stopUtc, w.EndAt) - Max(startUtc, w.StartAt)).TotalSeconds))
            .Sum();
        return (int)Math.Floor(Math.Max(0, total - inside) / 60);
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
