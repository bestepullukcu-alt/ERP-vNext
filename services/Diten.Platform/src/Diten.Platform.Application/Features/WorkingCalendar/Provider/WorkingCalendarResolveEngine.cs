using Diten.Platform.Domain.Entities.WorkingCalendar;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Provider;

/// <summary>
/// The single place the layering order lives. Pure and deterministic: it takes the already-loaded country row and
/// tenant override row and decides. No I/O, no tenant lookup, no clock — so the order can be tested exhaustively and
/// can never drift into a second copy inside a handler.
/// </summary>
public static class WorkingCalendarResolveEngine
{
    /// <summary>
    /// Resolution order (highest priority first):
    /// <list type="number">
    /// <item>tenant/org <c>working-day-override</c> → WORKING (a compensation day beats everything)</item>
    /// <item>country <c>working-day-override</c> → WORKING</item>
    /// <item>tenant/org holiday or closure → NOT working</item>
    /// <item>country holiday → NOT working</item>
    /// <item>the day is in the effective weekend set → NOT working</item>
    /// <item>otherwise → WORKING</item>
    /// </list>
    /// The effective weekend set is the override's when it declares one, else the country's (D3: <c>null</c> means
    /// inherit, an empty list means "no weekend" and is NOT inheritance).
    /// </summary>
    public static WorkingDayResult ResolveWorkingDay(
        DateOnly date, string countryCode, Wc? countryCalendar, Wc? overrideCalendar)
    {
        var reasons = new List<string>();

        if (countryCalendar is null)
        {
            return new WorkingDayResult(
                WorkingCalendarResolution.CalendarMissing, null, date, countryCode, null, null, null,
                $"No active working calendar exists for country '{countryCode}' in {date.Year}; whether this is a " +
                "working day is genuinely unknown and no default is assumed.",
                new[] { WorkingCalendarReasonCodes.CalendarMissing });
        }

        if (!countryCalendar.IsEffectiveOn(date))
        {
            return new WorkingDayResult(
                WorkingCalendarResolution.YearMissing, null, date, countryCode, countryCalendar.Id, null, null,
                $"The resolved calendar covers {countryCalendar.CalendarYear}, not {date.Year}.",
                new[] { WorkingCalendarReasonCodes.YearMissing });
        }

        var overrideId = overrideCalendar?.Id;
        if (overrideCalendar is not null)
        {
            reasons.Add(WorkingCalendarReasonCodes.TenantOverrideApplied);
        }

        // 1 + 2 — a compensation/bridge day forces a working day, overriding weekend AND holiday.
        var overrideForced = FindDay(overrideCalendar, date, onlyWorkingDayOverride: true);
        var countryForced = FindDay(countryCalendar, date, onlyWorkingDayOverride: true);
        var forced = overrideForced ?? countryForced;
        if (forced is not null)
        {
            reasons.Add(WorkingCalendarReasonCodes.WorkingDayOverrideApplied);
            reasons.Add(WorkingCalendarReasonCodes.WorkingDay);
            return new WorkingDayResult(
                WorkingCalendarResolution.Resolved, true, date, countryCode, countryCalendar.Id, overrideId,
                ToHolidayInfo(forced, fromOverride: overrideForced is not null),
                $"'{forced.DayName}' marks {date:yyyy-MM-dd} as a working day, overriding the weekend and any holiday.",
                reasons);
        }

        // 3 — tenant company holiday / closure.
        var overrideDay = FindDay(overrideCalendar, date, onlyWorkingDayOverride: false);
        if (overrideDay is not null)
        {
            return NonWorkingFromDay(
                date, countryCode, countryCalendar.Id, overrideId, overrideDay, fromOverride: true, reasons);
        }

        // 4 — country holiday.
        var countryDay = FindDay(countryCalendar, date, onlyWorkingDayOverride: false);
        if (countryDay is not null)
        {
            return NonWorkingFromDay(
                date, countryCode, countryCalendar.Id, overrideId, countryDay, fromOverride: false, reasons);
        }

        // 5 — weekend, from whichever layer actually declares it.
        var weekend = EffectiveWeekend(countryCalendar, overrideCalendar, out var inherited);
        reasons.Add(inherited
            ? WorkingCalendarReasonCodes.WeekendInheritedFromCountry
            : WorkingCalendarReasonCodes.WeekendFromTenantOverride);

        var weekdayKey = WorkingCalendarDayOfWeek.FromDate(date);
        if (weekend.Contains(weekdayKey, StringComparer.Ordinal))
        {
            reasons.Add(WorkingCalendarReasonCodes.WeekendDay);
            return new WorkingDayResult(
                WorkingCalendarResolution.Resolved, false, date, countryCode, countryCalendar.Id, overrideId, null,
                $"{weekdayKey} is a non-working weekday in the effective weekend definition.",
                reasons);
        }

        // 6 — ordinary working day.
        reasons.Add(WorkingCalendarReasonCodes.WorkingDay);
        return new WorkingDayResult(
            WorkingCalendarResolution.Resolved, true, date, countryCode, countryCalendar.Id, overrideId, null,
            $"{date:yyyy-MM-dd} is not a holiday, a closure or a weekend day.",
            reasons);
    }

    /// <summary>
    /// A half day is reported as a WORKING day — the business still operates — but the caller is told so explicitly
    /// via <c>half_day_treated_as_working</c> rather than having the nuance disappear. Hour-level handling is out of
    /// scope, so silently treating it as a full holiday would be the worse lie.
    /// </summary>
    private static WorkingDayResult NonWorkingFromDay(
        DateOnly date, string countryCode, Guid countryId, Guid? overrideId,
        WorkingCalendarDay day, bool fromOverride, List<string> reasons)
    {
        var info = ToHolidayInfo(day, fromOverride);

        if (day.IsHalfDay)
        {
            reasons.Add(WorkingCalendarReasonCodes.HalfDayTreatedAsWorking);
            reasons.Add(WorkingCalendarReasonCodes.WorkingDay);
            return new WorkingDayResult(
                WorkingCalendarResolution.Resolved, true, date, countryCode, countryId, overrideId, info,
                $"'{day.DayName}' is a half day; v1 counts a half day as a working day and flags it rather than " +
                "modelling hours.",
                reasons);
        }

        reasons.Add(fromOverride
            ? WorkingCalendarReasonCodes.CompanyClosure
            : WorkingCalendarReasonCodes.PublicHoliday);

        return new WorkingDayResult(
            WorkingCalendarResolution.Resolved, false, date, countryCode, countryId, overrideId, info,
            $"'{day.DayName}' ({day.DayType}) makes {date:yyyy-MM-dd} a non-working day.",
            reasons);
    }

    private static WorkingCalendarDay? FindDay(Wc? calendar, DateOnly date, bool onlyWorkingDayOverride)
    {
        if (calendar is null)
        {
            return null;
        }

        return calendar.ActiveDays()
            .Where(d => d.EffectiveDate == date)
            .Where(d => d.IsWorkingDayOverride == onlyWorkingDayOverride)
            .OrderBy(d => d.DayCode, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// D3 inheritance. <c>null</c> on the override means "inherit the country weekend"; an EMPTY list means the
    /// organization genuinely has no weekend and is NOT inheritance. Collapsing the two would silently give a
    /// seven-day organization the country's Saturday/Sunday.
    /// </summary>
    public static IReadOnlyList<string> EffectiveWeekend(Wc? countryCalendar, Wc? overrideCalendar, out bool inherited)
    {
        if (overrideCalendar?.WeekendDays is { } declared)
        {
            inherited = false;
            return declared;
        }

        inherited = true;
        return (IReadOnlyList<string>?)countryCalendar?.WeekendDays ?? Array.Empty<string>();
    }

    private static HolidayInfo ToHolidayInfo(WorkingCalendarDay day, bool fromOverride) => new(
        day.DayId, day.DayCode, day.DayName, day.Date, day.EffectiveDate, day.DayType, day.IsHalfDay, fromOverride);
}
