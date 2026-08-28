using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar;

public static class WorkingCalendarMapper
{
    /// <summary>
    /// Maps to the detail DTO. <paramref name="countryCalendar"/> is passed so an override row can report the weekend
    /// it actually resolves to — the UI must be able to say "inherited: saturday, sunday" rather than render an empty
    /// control that reads as "no weekend defined". Null is fine: the flags then simply say nothing was inherited.
    /// </summary>
    public static WorkingCalendarDto ToDto(Wc calendar, Wc? countryCalendar = null, bool readOnly = false)
    {
        var effectiveWeekend = WorkingCalendarResolveEngine.EffectiveWeekend(
            countryCalendar ?? (calendar.IsCountryLayer ? calendar : null), calendar, out var inherited);

        return new WorkingCalendarDto(
            calendar.Id,
            calendar.TenantId,
            calendar.CalendarCode,
            calendar.CalendarName,
            calendar.Description,
            calendar.CountryCode,
            calendar.CalendarYear,
            calendar.ScopeType,
            calendar.OrganizationUnitId,
            calendar.LegalEntityId,
            calendar.WeekendDays,
            effectiveWeekend,
            inherited && !calendar.IsCountryLayer,
            calendar.CalendarStatus,
            calendar.Source,
            calendar.Notes,
            calendar.IsCountryLayer,
            calendar.Days
                .OrderBy(d => d.ObservedDate ?? d.Date)
                .ThenBy(d => d.DayCode, StringComparer.Ordinal)
                .Select(ToDayDto)
                .ToList(),
            calendar.ActiveDays().Count(),
            calendar.ActivatedAt,
            calendar.ActivatedBy,
            calendar.ArchivedAt,
            calendar.ArchivedBy,
            calendar.CreatedAt,
            calendar.CreatedBy,
            calendar.UpdatedAt,
            calendar.UpdatedBy,
            calendar.Version,
            readOnly);
    }

    /// <param name="readOnly">
    /// Set by the tenant override surface for inherited country rows. The mapper cannot infer it: the SAME country
    /// row is writable on the platform surface and read-only on the tenant one, so the caller — which knows which
    /// surface it is serving — decides.
    /// </param>
    public static WorkingCalendarListItemDto ToListItem(Wc calendar, Wc? countryCalendar = null, bool readOnly = false)
    {
        var effectiveWeekend = WorkingCalendarResolveEngine.EffectiveWeekend(
            countryCalendar ?? (calendar.IsCountryLayer ? calendar : null), calendar, out var inherited);

        return new WorkingCalendarListItemDto(
            calendar.Id,
            calendar.TenantId,
            calendar.CalendarCode,
            calendar.CalendarName,
            calendar.CountryCode,
            calendar.CalendarYear,
            calendar.ScopeType,
            calendar.OrganizationUnitId,
            calendar.LegalEntityId,
            effectiveWeekend,
            inherited && !calendar.IsCountryLayer,
            calendar.CalendarStatus,
            calendar.Source,
            calendar.IsCountryLayer,
            calendar.ActiveDays().Count(),
            calendar.CreatedAt,
            calendar.UpdatedAt,
            calendar.Version,
            readOnly);
    }

    public static WorkingCalendarDayDto ToDayDto(WorkingCalendarDay day) => new(
        day.DayId,
        day.DayCode,
        day.DayName,
        day.Date,
        day.ObservedDate,
        day.EffectiveDate,
        day.DayType,
        day.Recurrence,
        day.IsHalfDay,
        day.DayStatus,
        day.Notes);

    public static WorkingDayResolveDto ToResolveDto(string operation, WorkingDayResult result) => new(
        operation, result.Resolution, result.IsWorkingDay, null, null, result.Date, null,
        result.CountryCode, null, null, result.ResolvedCalendarId, result.ResolvedOverrideCalendarId,
        result.Holiday, result.SelectionReason, result.ReasonCodes);

    public static WorkingDayResolveDto ToResolveDto(string operation, HolidayLookupResult result) => new(
        operation, result.Resolution, null, null, null, result.Date, null,
        result.CountryCode, null, null, result.ResolvedCalendarId, result.ResolvedOverrideCalendarId,
        result.Holiday, result.SelectionReason, result.ReasonCodes);

    public static WorkingDayResolveDto ToResolveDto(string operation, WorkingDateResult result) => new(
        operation, result.Resolution, null, result.Date, null, result.InputDate, null,
        result.CountryCode, null, null, null, null, null, result.SelectionReason, result.ReasonCodes);

    public static WorkingDayResolveDto ToResolveDto(string operation, WorkingDayCountResult result) => new(
        operation, result.Resolution, null, null, result.Count, result.FromDate, result.ToDate,
        result.CountryCode, null, null, null, null, null, result.SelectionReason, result.ReasonCodes);
}
