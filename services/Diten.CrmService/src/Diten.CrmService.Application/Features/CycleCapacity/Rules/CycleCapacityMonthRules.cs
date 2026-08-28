namespace Diten.CrmService.Application.Features.CycleCapacity.Rules;

/// <summary>
/// MOD-0155 FU06 — the month arithmetic of a period window. <b>Pure</b>: no repository, no HttpClient, no clock. It
/// turns "this period runs 15 Mar – 20 May" into the explicit month rows the capacity is authored against, and it
/// answers whether a given (year, month) belongs to a period at all.
/// <para><b>Everything here compares NORMALISED days, never instants.</b> The period's window is
/// <c>DateTimeOffset</c>, and comparing raw instants would put "1 March 00:00+03:00" in February for a UTC reader —
/// the documented CRM DateTimeOffset trap. Nothing here is ever used as a Mongo index or sort key either, for the same
/// family of reasons (the parallel-array trap).</para>
/// </summary>
public static class CycleCapacityMonthRules
{
    /// <summary>
    /// One month of a period, clipped to the period. <see cref="RangeStart"/> / <see cref="RangeEnd"/> are the
    /// INCLUSIVE day bounds actually covered — a period starting on the 15th contributes 15..end-of-month for its
    /// first month, not the whole month. This is what makes the working-day question honest at both edges.
    /// </summary>
    public sealed record MonthWindow(
        int Year,
        int MonthNumber,
        DateTimeOffset RangeStart,
        DateTimeOffset RangeEnd)
    {
        public DateOnly FromDate() => DateOnly.FromDateTime(RangeStart.UtcDateTime);

        public DateOnly ToDate() => DateOnly.FromDateTime(RangeEnd.UtcDateTime);
    }

    public static DateTimeOffset ToDay(DateTimeOffset value) => new(value.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>
    /// The month rows a period touches, in calendar order. Both ends of the period are INCLUSIVE (that is how
    /// <c>CyclePeriod</c> defines them), so a period ending on 1 May still contributes a May row — with a one-day
    /// range.
    /// <para>A period whose window is inverted yields no rows rather than throwing: the window is validated by
    /// <c>CyclePeriod</c> itself, and a pure function should not be the thing that crashes on data it did not
    /// author.</para>
    /// </summary>
    public static IReadOnlyList<MonthWindow> Derive(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        var start = ToDay(periodStart);
        var end = ToDay(periodEnd);

        if (end < start)
        {
            return Array.Empty<MonthWindow>();
        }

        var windows = new List<MonthWindow>();
        var cursor = new DateTime(start.UtcDateTime.Year, start.UtcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = new DateTime(end.UtcDateTime.Year, end.UtcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        while (cursor <= lastMonth)
        {
            var monthFirst = new DateTimeOffset(cursor, TimeSpan.Zero);
            var monthLast = new DateTimeOffset(
                cursor.AddMonths(1).AddDays(-1), TimeSpan.Zero);

            windows.Add(new MonthWindow(
                cursor.Year,
                cursor.Month,
                monthFirst > start ? monthFirst : start,
                monthLast < end ? monthLast : end));

            cursor = cursor.AddMonths(1);
        }

        return windows;
    }

    /// <summary>Does this (year, month) share at least one day with the period? The question the month-row validator
    /// asks, expressed once so the validator and <see cref="Derive"/> cannot disagree.</summary>
    public static bool Intersects(int year, int monthNumber, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (monthNumber is < 1 or > 12 || year is < 1 or > 9999)
        {
            return false;
        }

        var start = ToDay(periodStart);
        var end = ToDay(periodEnd);
        if (end < start)
        {
            return false;
        }

        var monthFirst = new DateTimeOffset(new DateTime(year, monthNumber, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
        var monthLast = monthFirst.AddMonths(1).AddDays(-1);

        return monthFirst <= end && start <= monthLast;
    }
}
