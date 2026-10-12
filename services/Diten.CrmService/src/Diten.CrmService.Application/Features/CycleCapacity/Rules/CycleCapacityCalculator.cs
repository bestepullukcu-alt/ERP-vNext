using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CycleCapacity.Rules;

/// <summary>
/// MOD-0155 FU06 — the capacity arithmetic. <b>Pure by design</b>: no <c>HttpClient</c>, no repository, no
/// <c>ITenantContext</c>, no <c>DateTime.UtcNow</c>. It takes the stored inputs plus the ALREADY-RESOLVED working-day
/// count of each month and returns the estimate. That is what lets the formula be tested exhaustively without the
/// working calendar being up.
///
/// <para><b>The formula (normative).</b> For each month <c>m</c>:</para>
/// <code>
/// fieldDays(m)         = max(0, wcWorkingDays(m) − meeting − training − vacation)
/// availableMinutes(m)  = DailyWorkMinutes × fieldDays(m)
/// dailyFixedMinutes(m) = capacity.DailyFixedMinutes() × fieldDays(m)       // WP-CAP-MODEL: typical = travel + quiz
/// microTargeting(m)    = min(MicroTargetingDayCount(m), fieldDays(m)) × MicroTargetingDuration(m)   // E8 clip
/// spendMinutes(m)      = dailyFixedMinutes(m) + microTargeting(m)
/// visitMinutes(m)      = max(0, availableMinutes(m) − spendMinutes(m))     // = RemainingMinutes
/// minutesPerVisit      = capacity.TypicalVisitMinutes()                    // typical visit, or legacy promo + non-promo
/// TotalVisitNumber(m)  = max(0, round(visitMinutes(m) ÷ minutesPerVisit × Fte, AwayFromZero))
/// </code>
/// <para><b>WP-CAP-MODEL — one visit-duration model.</b> The per-visit and per-day minutes come from the domain methods
/// (<c>TypicalVisitMinutes</c> / <c>DailyFixedMinutes</c>) the visit planner also reads. A legacy row (no typical
/// fields) still answers exactly its pre-WP-CAP-MODEL figure, except where E8's clip removes micro-targeting days a
/// month could never hold.</para>
///
/// <para><b>Weekends and public holidays are NOT subtracted here, and that is the point.</b>
/// <c>wcWorkingDays</c> comes from the working calendar's <c>working-days-between</c>, which has already excluded
/// weekends, public holidays and company closures day by day. Subtracting them again — as a naive reading of the
/// legacy model would — would count them twice and systematically under-estimate the field force.</para>
///
/// <para><b>Why the arithmetic is expressed at MONTH level in minutes.</b> Micro-targeting is a monthly pool
/// (<c>DayCount × Duration</c>), not a per-day rate. Folding it into a per-day form would require amortising it over
/// <c>fieldDays</c>, which divides by zero exactly when a month is fully consumed by leave — the one case the model
/// most needs to answer cleanly. At month level the same equation yields the identical figure whenever
/// <c>fieldDays &gt; 0</c> and returns a clean zero when it does not.</para>
///
/// <para><b>Nothing here is ever persisted.</b> The caller projects this result into a response; no method on this
/// class writes to the entity.</para>
/// </summary>
public static class CycleCapacityCalculator
{
    /// <summary>One month's resolved working-day count, as read from the working calendar. The clipped range is
    /// carried along so the result can show WHICH days were counted.</summary>
    public sealed record ResolvedMonth(
        CycleCapacityMonthRules.MonthWindow Window,
        int WorkingDays);

    /// <summary>The per-month breakdown. Every intermediate step is exposed on purpose: a capacity figure nobody can
    /// trace is a figure nobody trusts, so the UI shows working days → deductions → field days → minutes → visits.</summary>
    public sealed record MonthCalculation(
        int Year,
        int MonthNumber,
        DateTimeOffset RangeStart,
        DateTimeOffset RangeEnd,
        int CalendarDays,

        /// <summary>From the working calendar. Weekends, public holidays and closures are ALREADY excluded.</summary>
        int WorkingDays,

        /// <summary>
        /// FU07 — <c>CalendarDays − WorkingDays</c>. A DERIVATION, not a second measurement: it is the same working
        /// calendar answer read the other way round, so it costs no extra call and cannot disagree with the count it
        /// came from.
        /// <para>It lumps weekends, public holidays AND company closures together, which is precisely why it is named
        /// "non-working days" rather than "holidays". Telling those three apart needs the dates, and the calendar has
        /// no range operation to give them (follow-up F-WC-HOLIDAY-RANGE).</para>
        /// </summary>
        int NonWorkingDays,

        int MeetingDays,
        int TrainingDays,
        int VacationDays,
        int DeductedDays,
        int FieldDays,
        int AvailableMinutes,
        int MicroTargetingMinutes,
        int SpendMinutes,
        int VisitMinutes,

        /// <summary>FU07 — the FTE of THIS month. A field force is seasonal, so the multiplier belongs to the row
        /// rather than to the cycle.</summary>
        decimal Fte,

        int TotalVisitNumber,

        /// <summary>WP-CAP-MODEL — the month's fixed per-day charges: <c>DailyFixedMinutes() × FieldDays</c>.</summary>
        int DailyFixedMinutes,

        /// <summary>WP-CAP-MODEL — minutes left for visits (the waterfall's last step; equals <see cref="VisitMinutes"/>).</summary>
        int RemainingMinutes,

        /// <summary>WP-CAP-MODEL — the per-visit divisor this month was divided by.</summary>
        int TypicalVisitMinutes);

    /// <summary>WP-CAP-MODEL — the cycle totals. Every figure is the SUM of the month rows (visits are rounded per
    /// month and never re-rounded here); <see cref="AverageFte"/> is the plain mean of the months' FTE, rounded to two
    /// decimals (AwayFromZero).</summary>
    public sealed record CapacityTotals(
        int WorkingDays,
        int DeductedDays,
        int FieldDays,
        int AvailableMinutes,
        int DailyFixedMinutes,
        int MicroTargetingMinutes,
        int RemainingMinutes,
        int Visits,
        decimal AverageFte);

    /// <summary>
    /// The whole answer. <see cref="TotalVisitNumber"/> is <c>null</c> — never <c>0</c> — whenever
    /// <see cref="Resolution"/> is not <c>resolved</c>: zero means "no time is left", null means "we do not know".
    /// <see cref="Months"/> is EMPTY on an unresolved answer, because a partial table looks authoritative and is wrong.
    /// <para><b>FU07 removed the cycle-wide <c>Fte</c>.</b> There is no such number any more: each month carries its
    /// own, and publishing a single one would have to invent an average nobody authored.</para>
    /// </summary>
    /// <remarks>WP-CAP-MODEL — <see cref="Totals"/> is <c>null</c> on an unresolved answer for the same reason
    /// <see cref="TotalVisitNumber"/> is.</remarks>
    public sealed record CapacityCalculation(
        string Resolution,
        bool IsEstimate,
        int? TotalVisitNumber,
        int MinutesPerVisit,
        IReadOnlyList<MonthCalculation> Months,
        IReadOnlyList<string> ReasonCodes,
        string Reason,
        string VisitModel,
        int TypicalVisitMinutes,
        int DailyFixedMinutes,
        CapacityTotals? Totals);

    public static CapacityCalculation Calculate(CapacityEntity capacity, IReadOnlyList<ResolvedMonth> resolvedMonths)
    {
        var minutesPerVisit = capacity.TypicalVisitMinutes();
        var dailyFixed = capacity.DailyFixedMinutes();

        var byMonth = capacity.Months.ToDictionary(m => (m.Year, m.MonthNumber));
        var months = new List<MonthCalculation>(resolvedMonths.Count);

        foreach (var resolved in resolvedMonths.OrderBy(r => r.Window.Year).ThenBy(r => r.Window.MonthNumber))
        {
            var input = byMonth.TryGetValue((resolved.Window.Year, resolved.Window.MonthNumber), out var row)
                ? row
                : new CycleCapacityMonth
                {
                    Year = resolved.Window.Year,
                    MonthNumber = resolved.Window.MonthNumber
                };

            var deducted = input.DeductedDays();
            var fieldDays = Math.Max(0, resolved.WorkingDays - deducted);

            // The days the period actually covers in this month — the clipped range, so a first or last month counts
            // only its own slice. Inclusive at both ends, like the window itself.
            var calendarDays = (int)(resolved.Window.RangeEnd.UtcDateTime.Date
                                     - resolved.Window.RangeStart.UtcDateTime.Date).TotalDays + 1;
            var availableMinutes = capacity.DailyWorkMinutes * fieldDays;
            // E8 — micro-targeting days beyond the month's field days cannot happen, so they are clipped.
            var microTargetingMinutes = input.MicroTargetingMinutes(fieldDays);
            var dailyFixedMinutes = dailyFixed * fieldDays;
            var spendMinutes = dailyFixedMinutes + microTargetingMinutes;
            var visitMinutes = Math.Max(0, availableMinutes - spendMinutes);

            months.Add(new MonthCalculation(
                resolved.Window.Year,
                resolved.Window.MonthNumber,
                resolved.Window.RangeStart,
                resolved.Window.RangeEnd,
                calendarDays,
                resolved.WorkingDays,
                Math.Max(0, calendarDays - resolved.WorkingDays),
                input.MeetingDays,
                input.TrainingDays,
                input.VacationDays,
                deducted,
                fieldDays,
                availableMinutes,
                microTargetingMinutes,
                spendMinutes,
                visitMinutes,
                input.Fte,
                // FU07 — the row's OWN multiplier. Reading a cycle-wide FTE here was what made a seasonal field force
                // unsayable.
                Visits(visitMinutes, minutesPerVisit, input.Fte),
                dailyFixedMinutes,
                visitMinutes,
                minutesPerVisit));
        }

        return new CapacityCalculation(
            CycleCapacityResolutions.Resolved,
            IsEstimate: true,
            months.Sum(m => m.TotalVisitNumber),
            minutesPerVisit,
            months,
            new[] { CycleCapacityReasonCodes.CapacityOk },
            "Capacity estimated from the published working calendar and the authored activity budget.",
            capacity.VisitModel(),
            minutesPerVisit,
            dailyFixed,
            Totals(months));
    }

    /// <summary>WP-CAP-MODEL — the cycle totals, each the sum of the month rows.</summary>
    public static CapacityTotals Totals(IReadOnlyList<MonthCalculation> months)
        => new(
            months.Sum(m => m.WorkingDays),
            months.Sum(m => m.DeductedDays),
            months.Sum(m => m.FieldDays),
            months.Sum(m => m.AvailableMinutes),
            months.Sum(m => m.DailyFixedMinutes),
            months.Sum(m => m.MicroTargetingMinutes),
            months.Sum(m => m.RemainingMinutes),
            months.Sum(m => m.TotalVisitNumber),
            months.Count == 0
                ? 0m
                : Math.Round(months.Average(m => m.Fte), 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The honest empty answer. Used whenever ANY month failed to resolve — a partial table is never returned, mirroring
    /// the working calendar's own "no partial count" rule.
    /// </summary>
    public static CapacityCalculation Unresolved(
        CapacityEntity capacity,
        string resolution,
        IReadOnlyList<string> reasonCodes,
        string reason)
        => new(
            resolution,
            IsEstimate: true,
            TotalVisitNumber: null,
            capacity.TypicalVisitMinutes(),
            Array.Empty<MonthCalculation>(),
            reasonCodes.Count == 0 ? new[] { CycleCapacityReasonCodes.CalendarUnresolved } : reasonCodes,
            reason,
            capacity.VisitModel(),
            capacity.TypicalVisitMinutes(),
            capacity.DailyFixedMinutes(),
            Totals: null);

    /// <summary>
    /// Visits from minutes. <c>minutesPerVisit</c> is guaranteed positive by the write path; the guard is kept anyway
    /// so a row written before that rule existed degrades to zero rather than throwing at read time.
    /// <para>Decimal arithmetic throughout, and rounded ONCE at the end: rounding the division first and multiplying
    /// afterwards would compound the error across a whole cycle.</para>
    /// </summary>
    private static int Visits(int visitMinutes, int minutesPerVisit, decimal fte)
    {
        if (minutesPerVisit <= 0 || visitMinutes <= 0 || fte <= 0m)
        {
            return 0;
        }

        var visits = Math.Round(
            visitMinutes / (decimal)minutesPerVisit * fte, 0, MidpointRounding.AwayFromZero);

        return visits <= 0m ? 0 : (int)Math.Min(visits, int.MaxValue);
    }
}
