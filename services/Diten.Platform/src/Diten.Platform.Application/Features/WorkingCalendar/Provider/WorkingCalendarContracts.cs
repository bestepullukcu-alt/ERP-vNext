namespace Diten.Platform.Application.Features.WorkingCalendar.Provider;

/// <summary>
/// Why a working-day question could (or could not) be answered.
/// <para>
/// This exists because a bare <c>bool</c> would be a lie. If no calendar has been entered for a country/year, a
/// boolean has to say <c>true</c> or <c>false</c>, and the consumer then plans against a fact nobody ever stated.
/// The result carries the resolution instead, and the value is <c>null</c> whenever it is not <see cref="Resolved"/>
/// — a default is never invented.
/// </para>
/// </summary>
public static class WorkingCalendarResolution
{
    /// <summary>An active calendar answered the question.</summary>
    public const string Resolved = "resolved";

    /// <summary>No active calendar exists for that country at all.</summary>
    public const string CalendarMissing = "calendar_missing";

    /// <summary>A calendar exists for the country but not for the requested year (including one year of a multi-year
    /// range). A partial count is never returned in this case.</summary>
    public const string YearMissing = "year_missing";

    /// <summary>The country code is not in the MOD-0048 <c>countries</c> reference set.</summary>
    public const string CountryUnknown = "country_unknown";

    /// <summary>
    /// The request itself is malformed (start after end, or a range beyond the scan limit). Added beyond the four
    /// resolutions the pack enumerated: the in-process provider must never throw into a consumer, so a bad range has
    /// to come back as a value. The HTTP surface maps this to 400 <c>invalid_date_range</c>.
    /// </summary>
    public const string InvalidRange = "invalid_range";
}

/// <summary>Canonical reason codes. Every result carries at least one — an outcome is never silent.</summary>
public static class WorkingCalendarReasonCodes
{
    public const string WorkingDay = "working_day";
    public const string WeekendDay = "weekend_day";
    public const string PublicHoliday = "public_holiday";
    public const string CompanyClosure = "company_closure";
    public const string WorkingDayOverrideApplied = "working_day_override_applied";
    public const string HalfDayTreatedAsWorking = "half_day_treated_as_working";
    public const string TenantOverrideApplied = "tenant_override_applied";
    public const string WeekendInheritedFromCountry = "weekend_inherited_from_country";
    public const string WeekendFromTenantOverride = "weekend_from_tenant_override";
    public const string CalendarMissing = "calendar_missing";
    public const string YearMissing = "year_missing";
    public const string CountryUnknown = "country_unknown";
    public const string CalendarNotActive = "calendar_not_active";
    public const string InvalidDateRange = "invalid_date_range";
}

/// <summary>
/// What to resolve against: a country, optionally narrowed to one organization unit. The tenant dimension is NOT a
/// parameter — it comes from the ambient tenant context, so a caller can never ask about someone else's overrides.
/// </summary>
public sealed record WorkingCalendarScope(
    string CountryCode,
    Guid? OrganizationUnitId = null,
    Guid? LegalEntityId = null);

/// <summary>Which holiday/closure governs a date, when one does.</summary>
public sealed record HolidayInfo(
    Guid DayId,
    string DayCode,
    string DayName,
    DateOnly Date,
    DateOnly EffectiveDate,
    string DayType,
    bool IsHalfDay,
    bool FromTenantOverride);

/// <summary>Is this date a working day? <see cref="IsWorkingDay"/> is null unless <see cref="Resolution"/> is resolved.</summary>
public sealed record WorkingDayResult(
    string Resolution,
    bool? IsWorkingDay,
    DateOnly Date,
    string CountryCode,
    Guid? ResolvedCalendarId,
    Guid? ResolvedOverrideCalendarId,
    HolidayInfo? Holiday,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes);

/// <summary>Which holiday a date falls on. <see cref="Holiday"/> is null both when unresolved and when the date is
/// simply not a holiday — <see cref="Resolution"/> distinguishes the two.</summary>
public sealed record HolidayLookupResult(
    string Resolution,
    HolidayInfo? Holiday,
    DateOnly Date,
    string CountryCode,
    Guid? ResolvedCalendarId,
    Guid? ResolvedOverrideCalendarId,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes);

/// <summary>A computed date (next working day / add N working days). <see cref="Date"/> is null unless resolved.</summary>
public sealed record WorkingDateResult(
    string Resolution,
    DateOnly? Date,
    DateOnly InputDate,
    string CountryCode,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes);

/// <summary>A working-day count. <see cref="Count"/> is null unless resolved — a partial count is never returned.</summary>
public sealed record WorkingDayCountResult(
    string Resolution,
    int? Count,
    DateOnly FromDate,
    DateOnly ToDate,
    string CountryCode,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes);
