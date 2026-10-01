using Diten.Platform.Application.Features.WorkingHours;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 — the pure arithmetic of a personal plan block. No I/O and no clock, so the plan
/// handler and the calendar feed compute the same numbers from the same facts.
///
/// <para>⚠ No wall-clock constant lives here or anywhere in task/calendar code (TaskCalendarGuardTests):
/// when a day's working window matters, it arrives as a <see cref="WorkingDay"/> from
/// <see cref="IWorkingHoursProvider"/>.</para>
/// </summary>
public static class TaskPlanBlockRules
{
    /// <summary>A block is a whole number of these, and at least one.</summary>
    public const int StepMinutes = 15;

    /// <summary>The block length when the caller gives none and the task has no estimate.</summary>
    public const int FallbackDurationMinutes = 60;

    public static bool IsValidDuration(int minutes) => minutes >= StepMinutes && minutes % StepMinutes == 0;

    /// <summary>The task's estimate in minutes, rounded UP to a whole step; null when there is no usable estimate.</summary>
    public static int? EstimateMinutes(decimal? estimateHours)
    {
        if (estimateHours is not { } hours || hours <= 0)
        {
            return null;
        }

        var minutes = (int)Math.Ceiling(hours * 60m);
        return (int)Math.Ceiling(minutes / (decimal)StepMinutes) * StepMinutes;
    }

    /// <summary>No duration given: the estimate, else <see cref="FallbackDurationMinutes"/>.</summary>
    public static int DefaultDuration(decimal? estimateHours)
        => EstimateMinutes(estimateHours) ?? FallbackDurationMinutes;

    /// <summary>
    /// What is left of the estimate after this block — DERIVED, never stored. Null without a block or an estimate.
    /// </summary>
    public static int? RemainingMinutes(decimal? estimateHours, int? blockMinutes)
    {
        if (blockMinutes is not { } block || EstimateMinutes(estimateHours) is not { } estimate)
        {
            return null;
        }

        return Math.Max(0, estimate - block);
    }

    /// <summary>Half-open intervals: a block ending at 10:00 and one starting at 10:00 do not overlap.</summary>
    public static bool Overlaps(DateTimeOffset startA, DateTimeOffset endA, DateTimeOffset startB, DateTimeOffset endB)
        => startA < endB && startB < endA;

    /// <summary>
    /// Fits a block into its day's working window.
    ///
    /// <para>A block that STARTS inside a window and runs past its end is cut at the end (down to a whole step), and
    /// reports <see cref="BlockFit.Truncated"/>. A block that starts outside every window — before the day starts,
    /// after it ends, or on a weekend/holiday — is kept as asked and reports
    /// <see cref="BlockFit.OutsideWorkingHours"/>: a warning, never a refusal. A cut that would leave less than one
    /// step is not made; the block is kept and flagged outside instead.</para>
    /// </summary>
    public static BlockFit Fit(DateTimeOffset start, int durationMinutes, WorkingDay? day)
    {
        if (day is null)
        {
            return new BlockFit(durationMinutes, Truncated: false, OutsideWorkingHours: false);
        }

        var window = day.Windows.FirstOrDefault(w => w.StartAt <= start && start < w.EndAt);
        if (window is null)
        {
            return new BlockFit(durationMinutes, Truncated: false, OutsideWorkingHours: true);
        }

        var end = start.AddMinutes(durationMinutes);
        if (end <= window.EndAt)
        {
            return new BlockFit(durationMinutes, Truncated: false, OutsideWorkingHours: false);
        }

        var available = (int)Math.Floor((window.EndAt - start).TotalMinutes / StepMinutes) * StepMinutes;
        return available >= StepMinutes
            ? new BlockFit(available, Truncated: true, OutsideWorkingHours: false)
            : new BlockFit(durationMinutes, Truncated: false, OutsideWorkingHours: true);
    }
}

/// <summary>The block length that will be stored, and what fitting it into the day found.</summary>
public sealed record BlockFit(int DurationMinutes, bool Truncated, bool OutsideWorkingHours);
