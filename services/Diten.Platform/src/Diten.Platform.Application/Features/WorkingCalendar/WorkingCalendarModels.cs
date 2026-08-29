using Diten.Platform.Application.Features.WorkingCalendar.Provider;

namespace Diten.Platform.Application.Features.WorkingCalendar;

/// <summary>All working-calendar DTOs in one file, per the golden-reference convention.</summary>
public sealed record WorkingCalendarDayDto(
    Guid DayId,
    string DayCode,
    string DayName,
    DateOnly Date,
    DateOnly? ObservedDate,
    DateOnly EffectiveDate,
    string DayType,
    string Recurrence,
    bool IsHalfDay,
    string DayStatus,
    string? Notes);

public sealed record WorkingCalendarDto(
    Guid Id,
    Guid? TenantId,
    string CalendarCode,
    string CalendarName,
    string? Description,
    string CountryCode,
    int CalendarYear,
    string ScopeType,
    Guid? OrganizationUnitId,
    Guid? LegalEntityId,
    IReadOnlyList<string>? WeekendDays,
    // EffectiveWeekendDays = what the weekend resolves to once inheritance is applied, so the UI can render
    // "inherited from the country calendar: saturday, sunday" instead of an empty control the user misreads as
    // "no weekend defined". WeekendInherited says which layer it came from.
    IReadOnlyList<string> EffectiveWeekendDays,
    bool WeekendInherited,
    string CalendarStatus,
    string Source,
    string? Notes,
    bool IsCountryLayer,
    IReadOnlyList<WorkingCalendarDayDto> Days,
    int ActiveDayCount,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int Version,
    /// <summary>
    /// True when this detail was opened through the tenant surface as an inherited country row. Rendering only;
    /// write enforcement remains in <c>WorkingCalendarWriteGuard</c>.
    /// </summary>
    bool IsReadOnly = false);

public sealed record WorkingCalendarListItemDto(
    Guid Id,
    Guid? TenantId,
    string CalendarCode,
    string CalendarName,
    string CountryCode,
    int CalendarYear,
    string ScopeType,
    Guid? OrganizationUnitId,
    Guid? LegalEntityId,
    IReadOnlyList<string> EffectiveWeekendDays,
    bool WeekendInherited,
    string CalendarStatus,
    string Source,
    bool IsCountryLayer,
    int ActiveDayCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Version,
    /// <summary>
    /// True when the caller may read this row but not write it. On the tenant override surface every country row
    /// carries it, so the page can render the row without offering Edit/Archive.
    /// <para>
    /// This is a RENDERING hint, never the enforcement. The write path is closed independently: a country row's id
    /// is invisible to <c>GetOwnOverrideByIdAsync</c>, so update/activate/archive answer 404 whatever the UI does.
    /// A page that ignored this flag would still be unable to change a country calendar.
    /// </para>
    /// </summary>
    bool IsReadOnly = false);

public sealed record WorkingCalendarListDto(int TotalCount, IReadOnlyList<WorkingCalendarListItemDto> Items);

/// <summary>Feature flags + supported vocabulary. Every dropdown in both UIs is fed from here — there is no hardcoded
/// vocabulary list in any view or JS file.</summary>
public sealed record WorkingCalendarContractDto(
    string Capability,
    string ContractVersion,
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<string> DayOfWeek,
    IReadOnlyList<string> DayTypes,
    IReadOnlyList<string> Recurrences,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> DayStatuses,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> WritableSources,
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    int MaxDaysPerCalendar,
    IReadOnlyList<string> Limitations);

/// <summary>The tenant-facing slice of the contract: <c>country</c> is absent from the scope list and the
/// country-layer day types are absent from the day-type list, so the override form structurally cannot offer them.</summary>
public sealed record WorkingCalendarOverrideContractDto(
    string Capability,
    string ContractVersion,
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<string> DayOfWeek,
    IReadOnlyList<string> DayTypes,
    IReadOnlyList<string> Recurrences,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> DayStatuses,
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    int MaxDaysPerCalendar,
    IReadOnlyList<string> Limitations);

/// <summary>Everything the resolve endpoints can return, in one envelope so the UI preview and any external consumer
/// read the same shape regardless of which operation was asked for.</summary>
public sealed record WorkingDayResolveDto(
    string Operation,
    string Resolution,
    bool? IsWorkingDay,
    DateOnly? ResultDate,
    int? WorkingDayCount,
    DateOnly RequestedDate,
    DateOnly? RequestedToDate,
    string CountryCode,
    Guid? OrganizationUnitId,
    Guid? LegalEntityId,
    Guid? ResolvedCalendarId,
    Guid? ResolvedOverrideCalendarId,
    HolidayInfo? Holiday,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes);

/// <summary>Write payload for a day inside the embedded list.</summary>
public sealed record WorkingCalendarDayInput(
    Guid? DayId,
    string DayCode,
    string DayName,
    DateOnly Date,
    DateOnly? ObservedDate,
    string DayType,
    string Recurrence,
    bool IsHalfDay,
    string? Notes);
