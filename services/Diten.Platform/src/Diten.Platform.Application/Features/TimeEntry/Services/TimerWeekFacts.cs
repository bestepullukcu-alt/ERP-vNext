using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What the week read reports about timer time that did NOT become a draft row — derived from the closed
/// segments, never stored.</summary>
public static class TimerWeekFacts
{
    /// <summary>A2 — (day, target) groups whose closed timer time sums to more than nothing and less than 8 minutes.</summary>
    public static IReadOnlyList<TimerTooShortDto> TooShort(IEnumerable<TimerSegment> closedSegments)
        => closedSegments
            .GroupBy(s => (s.LocalDate, s.TaskItemId, s.CategoryCode))
            .Select(g => (g.Key, Seconds: g.Sum(s => s.DurationSeconds)))
            .Where(x => x.Seconds > 0 && x.Seconds < TimerRules.MinimumCountableSeconds)
            .OrderBy(x => x.Key.LocalDate)
            .Select(x => new TimerTooShortDto(x.Key.LocalDate, x.Key.TaskItemId, x.Key.CategoryCode, x.Seconds))
            .ToList();

    /// <summary>§13 — on a submitted or approved week: timer minutes of a (day, target) beyond what the locked revision's
    /// timer row already carries. Kept, never added to the locked revision, never silently dropped.</summary>
    public static IReadOnlyList<TimerOutsideOpenWeekDto> OutsideOpenWeek(
        IEnumerable<TimerSegment> closedSegments, IReadOnlyCollection<TimeEntryRow> lockedRows)
        => closedSegments
            .GroupBy(s => (s.LocalDate, s.TaskItemId, s.CategoryCode))
            .Select(g =>
            {
                var counted = lockedRows
                    .Where(r => r.Source == TimeEntrySource.Timer && r.LocalDate == g.Key.LocalDate
                                && r.TaskItemId == g.Key.TaskItemId && r.CategoryCode == g.Key.CategoryCode)
                    .Sum(r => r.DurationMinutes);
                return (g.Key, Minutes: TimerRules.DraftMinutes(g.Sum(s => (long)s.DurationSeconds)) - counted);
            })
            .Where(x => x.Minutes > 0)
            .OrderBy(x => x.Key.LocalDate)
            .Select(x => new TimerOutsideOpenWeekDto(x.Key.LocalDate, x.Key.TaskItemId, x.Key.CategoryCode, x.Minutes))
            .ToList();
}
