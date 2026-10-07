using System.Globalization;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3A (MK-3) — the period plan's weeks, in ONE place (pure, no I/O). A week is a calendar week Monday→Sunday; the
/// period's weeks are every Monday-week that touches the period, its working window clipped to the period. "Today" is
/// the UTC calendar day (the WP-E2E-FIX-1 source).
/// <para><b>Week status</b> (<see cref="Derive"/>), in this order:</para>
/// <list type="number">
/// <item><c>past</c> — the week's Sunday is before today (a week stays current through its Sunday; it is past from the
/// next Monday on), approved or not;</item>
/// <item><c>approved</c> — stored as approved (its visits are written and frozen);</item>
/// <item><c>draft</c> — not approved and at least one visit falls in it;</item>
/// <item><c>empty</c> — not approved and no visit falls in it.</item>
/// </list>
/// A reopened week is neither approved nor past here: it is draft / empty again, by its visits.
/// </summary>
public static class PlanningWeekCalendar
{
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>The Monday of the calendar week containing <paramref name="day"/>.</summary>
    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>"Today" — the UTC calendar day of <paramref name="now"/>.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    /// <summary>Every Monday-week touching [<paramref name="periodStart"/>, <paramref name="periodEnd"/>], in order.</summary>
    public static IReadOnlyList<PlanningWeekSpan> PeriodWeeks(DateOnly periodStart, DateOnly periodEnd)
    {
        var weeks = new List<PlanningWeekSpan>();
        if (periodEnd < periodStart)
        {
            return weeks;
        }

        for (var monday = MondayOf(periodStart); monday <= periodEnd; monday = monday.AddDays(7))
        {
            var from = monday < periodStart ? periodStart : monday;
            var sunday = monday.AddDays(6);
            weeks.Add(new PlanningWeekSpan(monday, from, sunday > periodEnd ? periodEnd : sunday));
        }

        return weeks;
    }

    /// <summary>A requested week: a yyyy-MM-dd MONDAY whose week touches the period; false otherwise (invalid_week).</summary>
    public static bool TryParseWeek(
        string? weekStart, DateOnly periodStart, DateOnly periodEnd, out PlanningWeekSpan week)
    {
        week = default!;
        if (string.IsNullOrWhiteSpace(weekStart)
            || !DateOnly.TryParseExact(weekStart.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var monday)
            || monday.DayOfWeek != DayOfWeek.Monday)
        {
            return false;
        }

        var hit = PeriodWeeks(periodStart, periodEnd).FirstOrDefault(w => w.Monday == monday);
        if (hit is null)
        {
            return false;
        }

        week = hit;
        return true;
    }

    /// <summary>The week is over: its Sunday is before today.</summary>
    public static bool IsPast(PlanningWeekSpan week, DateOnly today) => week.Monday.AddDays(6) < today;

    /// <summary>The derived status (see the class note). <paramref name="visitCount"/> = the visits falling in the week.</summary>
    public static string Derive(PlanningWeekSpan week, DateOnly today, PlanningWeek? stored, int visitCount)
    {
        if (IsPast(week, today))
        {
            return PlanningWeekDisplayStatus.Past;
        }

        if (stored is not null && stored.IsApproved())
        {
            return PlanningWeekDisplayStatus.Approved;
        }

        return visitCount > 0 ? PlanningWeekDisplayStatus.Draft : PlanningWeekDisplayStatus.Empty;
    }

    /// <summary>The read model of one week.</summary>
    public static PlanningWeekDto ToDto(PlanningWeekSpan week, string status, int? visitCount, PlanningWeek? stored)
        => new(
            week.WeekStart,
            week.IsoWeek,
            week.From.ToString(DateFormat, CultureInfo.InvariantCulture),
            week.To.ToString(DateFormat, CultureInfo.InvariantCulture),
            status,
            visitCount,
            stored?.Status,
            stored?.ApprovedAt,
            stored?.ApprovedBy,
            stored?.History.Select(h => new PlanningWeekHistoryDto(h.At, h.By, h.Action, h.Reason)).ToList());
}

/// <summary>WP-VP-3A — one Monday-week of a period: <see cref="Monday"/> (the key) and its working window
/// [<see cref="From"/>, <see cref="To"/>] clipped to the period.</summary>
public sealed record PlanningWeekSpan(DateOnly Monday, DateOnly From, DateOnly To)
{
    public string WeekStart => Monday.ToString(PlanningWeekCalendar.DateFormat, CultureInfo.InvariantCulture);
    public int IsoWeek => ISOWeek.GetWeekOfYear(Monday.ToDateTime(TimeOnly.MinValue));
}
