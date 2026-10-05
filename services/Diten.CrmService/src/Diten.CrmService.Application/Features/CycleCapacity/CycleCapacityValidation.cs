using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CycleCapacity;

/// <summary>
/// MOD-0155 FU06 shared write-path validation. Kept in ONE place so create and update can never drift apart.
/// Everything here is <b>structural and performs no I/O</b>: the governed country check needs the reference-data seam
/// and the pin check needs the period reader, so both live in the handlers.
/// <para><b>The divide-by-zero guard lives here, not in the calculator.</b>
/// <c>PromoProductTime + NonPromoProductTime &gt; 0</c> is enforced on the write path, so a stored capacity can never
/// reach the arithmetic with a zero divisor. Guarding inside the calculator instead would mean the invalid record was
/// already saved and every future reader had to cope with it.</para>
/// </summary>
public static class CycleCapacityValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler must answer with. Nested so this file still declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    /// <summary>
    /// WP-CAP-MODEL (K-1) — the typical-visit triple as it arrives on a write. All three present switches the row onto
    /// the single visit-duration model; none present keeps the stored model (legacy on a new row); anything in between
    /// is refused (<c>typical_visit_incomplete</c>).
    /// </summary>
    public sealed record TypicalVisit(int? TypicalPromoCount, int? TypicalNonPromoCount, int? ReportMinutesPerVisit)
    {
        public static readonly TypicalVisit None = new(null, null, null);

        public bool IsEmpty => TypicalPromoCount is null && TypicalNonPromoCount is null && ReportMinutesPerVisit is null;

        public bool IsComplete => TypicalPromoCount is not null && TypicalNonPromoCount is not null
                                  && ReportMinutesPerVisit is not null;
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Normalises a date to UTC midnight. A period is a run of DAYS: keeping the caller's clock time would
    /// make a month boundary mean a different instant per client.</summary>
    public static DateTimeOffset ToDay(DateTimeOffset value) => new(value.UtcDateTime.Date, TimeSpan.Zero);

    public static Failure? ValidateDailyWorkMinutes(int dailyWorkMinutes)
        => dailyWorkMinutes is >= CycleCapacityLimits.MinDailyWorkMinutes and <= CycleCapacityLimits.MaxDailyWorkMinutes
            ? null
            : new Failure(
                $"DailyWorkMinutes must be between {CycleCapacityLimits.MinDailyWorkMinutes} and "
                + $"{CycleCapacityLimits.MaxDailyWorkMinutes}.",
                CycleCapacityReasonCodes.DailyWorkMinutesInvalid);

    /// <summary>
    /// The activity minute budget. Per-visit charges are capped at eight hours (a longer "visit" is a typo), per-day
    /// charges at a full day.
    /// </summary>
    public static Failure? ValidateActivityMinutes(
        int promoProductTime,
        int nonPromoProductTime,
        int travelingTime,
        int reportDuration,
        int quizDuration,
        int dailyWorkMinutes)
    {
        if (!InRange(promoProductTime, CycleCapacityLimits.MaxMinutesPerVisit))
        {
            return MinutesFailure("PromoProductTime", CycleCapacityLimits.MaxMinutesPerVisit);
        }

        if (!InRange(nonPromoProductTime, CycleCapacityLimits.MaxMinutesPerVisit))
        {
            return MinutesFailure("NonPromoProductTime", CycleCapacityLimits.MaxMinutesPerVisit);
        }

        if (!InRange(travelingTime, CycleCapacityLimits.MaxMinutesPerDay))
        {
            return MinutesFailure("TravelingTime", CycleCapacityLimits.MaxMinutesPerDay);
        }

        if (!InRange(reportDuration, CycleCapacityLimits.MaxMinutesPerDay))
        {
            return MinutesFailure("ReportDuration", CycleCapacityLimits.MaxMinutesPerDay);
        }

        if (!InRange(quizDuration, CycleCapacityLimits.MaxMinutesPerDay))
        {
            return MinutesFailure("QuizDuration", CycleCapacityLimits.MaxMinutesPerDay);
        }

        // The divisor. Refused here so the calculator can state "minutesPerVisit > 0" as a fact rather than a hope.
        if (promoProductTime + nonPromoProductTime <= 0)
        {
            return new Failure(
                "PromoProductTime and NonPromoProductTime cannot both be zero — a visit that costs no time would make "
                + "the capacity infinite.",
                CycleCapacityReasonCodes.VisitMinutesZero);
        }

        // A day whose fixed charges already consume it leaves no time for any visit, which is a modelling error rather
        // than a capacity of zero: the author meant something else.
        if (travelingTime + reportDuration + quizDuration >= dailyWorkMinutes)
        {
            return new Failure(
                $"Travelling, reporting and quiz time total {travelingTime + reportDuration + quizDuration} minutes, "
                + $"which leaves no time for visits in a {dailyWorkMinutes}-minute day.",
                CycleCapacityReasonCodes.DailySpendExceedsDay);
        }

        return null;
    }

    /// <summary>
    /// MOD-0155 FU06B — the between-visit buffer. A single scalar, range-checked; the value is already defaulted before
    /// it reaches here (a missing payload takes the configured default on the write path), so this only guards the
    /// authored range and never treats absence as a fault.
    /// </summary>
    public static Failure? ValidateBetweenVisitTime(int betweenVisitTimeMinutes)
        => betweenVisitTimeMinutes is >= 0 and <= CycleCapacityLimits.MaxBufferMinutes
            ? null
            : new Failure(
                $"BetweenVisitTimeMinutes must be between 0 and {CycleCapacityLimits.MaxBufferMinutes} minutes.",
                CycleCapacityReasonCodes.BetweenVisitTimeInvalid);

    /// <summary>WP-SB-3a — both per-visit product ceilings are within 1..10 (400 <c>max_products_out_of_range</c>).</summary>
    public static Failure? ValidateMaxProducts(int maxPromoProducts, int maxNonPromoProducts)
    {
        foreach (var (field, value) in new[] { ("MaxPromoProducts", maxPromoProducts), ("MaxNonPromoProducts", maxNonPromoProducts) })
        {
            if (value < CycleCapacityLimits.MinProductsPerVisit || value > CycleCapacityLimits.MaxProductsPerVisit)
            {
                return new Failure(
                    $"{field} must be between {CycleCapacityLimits.MinProductsPerVisit} and "
                    + $"{CycleCapacityLimits.MaxProductsPerVisit}.",
                    CycleCapacityReasonCodes.MaxProductsOutOfRange);
            }
        }

        return null;
    }

    /// <summary>
    /// WP-CAP-MODEL (K-1) — the typical visit. An EMPTY triple is valid (legacy model) and returns null; a partial one
    /// is <c>typical_visit_incomplete</c>; the counts must lie within 0..the per-visit ceilings
    /// (<c>typical_count_exceeds_max</c>) and may not both be zero (<c>typical_visit_empty</c>); the per-visit report
    /// charge is a per-visit minute field (0..480); and the resulting typical visit must take more than zero and at
    /// most <see cref="CycleCapacityLimits.MaxMinutesPerVisit"/> minutes.
    /// </summary>
    public static Failure? ValidateTypicalVisit(
        TypicalVisit typical,
        int promoProductTime,
        int nonPromoProductTime,
        int maxPromoProducts,
        int maxNonPromoProducts)
    {
        if (typical.IsEmpty)
        {
            return null;
        }

        if (!typical.IsComplete)
        {
            return new Failure(
                "TypicalPromoCount, TypicalNonPromoCount and ReportMinutesPerVisit are sent together or not at all.",
                CycleCapacityReasonCodes.TypicalVisitIncomplete);
        }

        var promo = typical.TypicalPromoCount!.Value;
        var nonPromo = typical.TypicalNonPromoCount!.Value;
        var report = typical.ReportMinutesPerVisit!.Value;

        if (promo < 0 || promo > maxPromoProducts)
        {
            return new Failure(
                $"TypicalPromoCount must be between 0 and MaxPromoProducts ({maxPromoProducts}).",
                CycleCapacityReasonCodes.TypicalCountExceedsMax);
        }

        if (nonPromo < 0 || nonPromo > maxNonPromoProducts)
        {
            return new Failure(
                $"TypicalNonPromoCount must be between 0 and MaxNonPromoProducts ({maxNonPromoProducts}).",
                CycleCapacityReasonCodes.TypicalCountExceedsMax);
        }

        if (promo == 0 && nonPromo == 0)
        {
            return new Failure(
                "A typical visit tells at least one product — TypicalPromoCount and TypicalNonPromoCount cannot both be "
                + "zero.",
                CycleCapacityReasonCodes.TypicalVisitEmpty);
        }

        if (!InRange(report, CycleCapacityLimits.MaxMinutesPerVisit))
        {
            return MinutesFailure("ReportMinutesPerVisit", CycleCapacityLimits.MaxMinutesPerVisit);
        }

        var minutes = ((long)promo * promoProductTime) + ((long)nonPromo * nonPromoProductTime) + report;
        if (minutes <= 0)
        {
            return new Failure(
                "The typical visit takes no time — the capacity would be infinite.",
                CycleCapacityReasonCodes.VisitMinutesZero);
        }

        return minutes <= CycleCapacityLimits.MaxMinutesPerVisit
            ? null
            : new Failure(
                $"The typical visit takes {minutes} minutes; a visit may take at most "
                + $"{CycleCapacityLimits.MaxMinutesPerVisit}.",
                CycleCapacityReasonCodes.ActivityMinutesInvalid);
    }

    public static Failure? ValidateDescription(string? description)
    {
        var value = Trim(description);
        if (value is null)
        {
            return null;
        }

        return value.Length <= CycleCapacityLimits.MaxDescriptionLength
            ? null
            : new Failure(
                $"Description must be at most {CycleCapacityLimits.MaxDescriptionLength} characters.",
                CycleCapacityReasonCodes.DescriptionInvalid);
    }

    /// <summary>
    /// The month rows: present, individually sane, uniquely addressed by (Year, MonthNumber), and — the rule that
    /// matters — each intersecting the pinned period's window.
    /// <para>A deduction total larger than the month's working days is deliberately NOT a validation error: the
    /// working-day count is not known at write time (it is read from the calendar at read time), so judging it here
    /// would require guessing. The calculator clamps field days to zero instead and the UI flags the month.</para>
    /// </summary>
    public static Failure? ValidateMonths(
        IReadOnlyList<CycleCapacityMonthInput> months,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        if (months.Count == 0)
        {
            return new Failure(
                "At least one month row is required — a capacity with no months estimates nothing.",
                CycleCapacityReasonCodes.MonthsRequired);
        }

        if (months.Count > CycleCapacityLimits.MaxMonths)
        {
            return new Failure(
                $"A capacity may carry at most {CycleCapacityLimits.MaxMonths} month rows.",
                CycleCapacityReasonCodes.MonthInvalid);
        }

        var seen = new HashSet<(int Year, int Month)>();

        foreach (var month in months)
        {
            if (month.Year is < CycleCapacityLimits.MinYear or > CycleCapacityLimits.MaxYear)
            {
                return new Failure(
                    $"Month year {month.Year} must be between {CycleCapacityLimits.MinYear} and "
                    + $"{CycleCapacityLimits.MaxYear}.",
                    CycleCapacityReasonCodes.MonthInvalid);
            }

            if (month.MonthNumber is < CycleCapacityLimits.MinMonthNumber or > CycleCapacityLimits.MaxMonthNumber)
            {
                return new Failure(
                    $"MonthNumber {month.MonthNumber} must be between {CycleCapacityLimits.MinMonthNumber} and "
                    + $"{CycleCapacityLimits.MaxMonthNumber}.",
                    CycleCapacityReasonCodes.MonthInvalid);
            }

            if (!seen.Add((month.Year, month.MonthNumber)))
            {
                return new Failure(
                    $"{month.Year}-{month.MonthNumber:00} appears more than once. A month is identified by "
                    + "(Year, MonthNumber), so it can appear at most once.",
                    CycleCapacityReasonCodes.MonthDuplicate);
            }

            if (!Rules.CycleCapacityMonthRules.Intersects(month.Year, month.MonthNumber, periodStart, periodEnd))
            {
                return new Failure(
                    $"{month.Year}-{month.MonthNumber:00} lies outside the pinned cycle period's window "
                    + $"({periodStart:yyyy-MM-dd} – {periodEnd:yyyy-MM-dd}).",
                    CycleCapacityReasonCodes.MonthOutOfPeriod);
            }

            if (DeductionFailure(month) is { } deductionFailure)
            {
                return deductionFailure;
            }

            // WP-CAP-MODEL (K-5) — an authored FTE: the published range, with 0 allowed (a vacant position).
            if (month.Fte is { } fte && (fte < CycleCapacityLimits.MinAuthoredFte || fte > CycleCapacityLimits.MaxFte))
            {
                return new Failure(
                    $"Fte of {month.Year}-{month.MonthNumber:00} must be between {CycleCapacityLimits.MinAuthoredFte} "
                    + $"and {CycleCapacityLimits.MaxFte}.",
                    CycleCapacityReasonCodes.MonthFteInvalid);
            }
        }

        return null;
    }

    /// <summary>
    /// FU07 — the FTE the SERVER stamped onto a month row (interim default only; an authored value is range-checked in
    /// <see cref="ValidateMonths"/>).
    /// <para>It is checked here rather than on the request, because the request carries no FTE at all: the caller
    /// cannot send one, so validating an input field would be a guard over something that never arrives. What CAN go
    /// wrong is a configured default outside the published range, and that is a server-side fault worth refusing
    /// loudly instead of storing.</para>
    /// </summary>
    public static Failure? ValidateStampedMonthFte(CycleCapacityMonth month)
        => month.Fte >= CycleCapacityLimits.MinFte && month.Fte <= CycleCapacityLimits.MaxFte
            ? null
            : new Failure(
                $"The configured FTE for {month.Year}-{month.MonthNumber:00} is {month.Fte}, which is outside the "
                + $"published range {CycleCapacityLimits.MinFte}–{CycleCapacityLimits.MaxFte}.",
                CycleCapacityReasonCodes.MonthFteInvalid);

    /// <summary>Every write goes through here, so create and update enforce one shape.</summary>
    public static Failure? ValidateShape(
        int dailyWorkMinutes,
        int promoProductTime,
        int nonPromoProductTime,
        int travelingTime,
        int reportDuration,
        int quizDuration,
        string? description)
        => ValidateDailyWorkMinutes(dailyWorkMinutes)
           ?? ValidateActivityMinutes(
               promoProductTime, nonPromoProductTime, travelingTime, reportDuration, quizDuration, dailyWorkMinutes)
           ?? ValidateDescription(description);

    public static IReadOnlyList<string> ToErrors(Failure failure)
        => failure.Code is null ? new[] { failure.Message } : new[] { failure.Message, failure.Code };

    private static Failure? DeductionFailure(CycleCapacityMonthInput month)
    {
        if (!InRange(month.MeetingDays, CycleCapacityLimits.MaxDeductionDays))
        {
            return DaysFailure("MeetingDays", month);
        }

        if (!InRange(month.TrainingDays, CycleCapacityLimits.MaxDeductionDays))
        {
            return DaysFailure("TrainingDays", month);
        }

        if (!InRange(month.VacationDays, CycleCapacityLimits.MaxDeductionDays))
        {
            return DaysFailure("VacationDays", month);
        }

        if (!InRange(month.MicroTargetingDayCount, CycleCapacityLimits.MaxDeductionDays))
        {
            return DaysFailure("MicroTargetingDayCount", month);
        }

        return InRange(month.MicroTargetingDuration, CycleCapacityLimits.MaxMinutesPerDay)
            ? null
            : new Failure(
                $"MicroTargetingDuration of {month.Year}-{month.MonthNumber:00} must be between 0 and "
                + $"{CycleCapacityLimits.MaxMinutesPerDay} minutes.",
                CycleCapacityReasonCodes.DeductionInvalid);
    }

    private static bool InRange(int value, int max) => value >= 0 && value <= max;

    private static Failure MinutesFailure(string field, int max)
        => new($"{field} must be between 0 and {max} minutes.", CycleCapacityReasonCodes.ActivityMinutesInvalid);

    private static Failure DaysFailure(string field, CycleCapacityMonthInput month)
        => new(
            $"{field} of {month.Year}-{month.MonthNumber:00} must be between 0 and "
            + $"{CycleCapacityLimits.MaxDeductionDays} days.",
            CycleCapacityReasonCodes.DeductionInvalid);
}
