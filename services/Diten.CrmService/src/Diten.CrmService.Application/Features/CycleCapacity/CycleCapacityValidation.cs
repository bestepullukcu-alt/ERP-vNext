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
        }

        return null;
    }

    /// <summary>
    /// FU07 — the FTE the SERVER stamped onto a month row.
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
