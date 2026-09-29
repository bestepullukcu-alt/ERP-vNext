namespace Diten.Platform.Application.Features.WorkingHours;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 (BL-451 decision note) — THE ONE SEAM for "when does person P work on day D".
///
/// <para><b>Why a seam and not a tenant setting.</b> The owner asked whether fixing working hours later would
/// cause regressions. It does not, as long as nobody reads the tenant's default hours directly: every consumer —
/// the plan rule, the calendar feed, anything after them — asks this provider, and the provider resolves the chain
/// the way Oracle HCM resolves a work schedule: person → assignment → unit → legal entity → tenant default. v1
/// fills only the last ring (plus the working calendar's day types); when person/shift schedules arrive with
/// MOD-0280 they fill an earlier ring and no consumer changes. <c>TaskCalendarGuardTests</c> fails the build
/// if any other code reads <c>Tenant.DefaultWorkdayStart</c>/<c>DefaultWorkdayEnd</c>.</para>
///
/// <para><b>Never throws</b>, like <c>IWorkingCalendarProvider</c> it builds on: an unresolved calendar still
/// answers — the day counts as a working day and says <see cref="WorkingDay.CalendarUnresolved"/>.</para>
/// </summary>
public interface IWorkingHoursProvider
{
    /// <summary>
    /// Working windows for <paramref name="userId"/>, one entry per local calendar day from
    /// <paramref name="from"/> to <paramref name="to"/> inclusive, in the tenant's time zone. Windows are UTC
    /// instants. An inverted range answers with no days.
    /// </summary>
    Task<WorkingHoursResult> GetWorkingWindowsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>Which ring of the chain supplied a day's hours. Wire spelling, lowerCamel.</summary>
public static class WorkingHoursSources
{
    public const string Person = "person";
    public const string Assignment = "assignment";
    public const string Unit = "unit";
    public const string LegalEntity = "legalEntity";
    public const string TenantDefault = "tenantDefault";
}

/// <summary>What kind of day it is. Wire spelling, lowerCamel.</summary>
public static class WorkingDayKinds
{
    public const string WorkingDay = "workingDay";
    public const string Weekend = "weekend";
    public const string Holiday = "holiday";
}

/// <summary>One working window, as absolute UTC instants.</summary>
public sealed record WorkingWindow(DateTimeOffset StartAt, DateTimeOffset EndAt);

/// <summary>
/// One local day. <see cref="Windows"/> is empty on a weekend or holiday. <see cref="CalendarUnresolved"/> is true
/// when the working calendar could not answer (no country, no calendar for the year…): the day is then COUNTED as a
/// working day rather than guessed to be off, and says so.
/// </summary>
public sealed record WorkingDay(
    DateOnly Date,
    string DayKind,
    string? HolidayName,
    IReadOnlyList<WorkingWindow> Windows,
    string ResolvedFrom,
    bool CalendarUnresolved);

/// <summary>The provider's answer. <see cref="TimeZone"/> is what every local-day computation must use.</summary>
public sealed record WorkingHoursResult(TimeZoneInfo TimeZone, IReadOnlyList<WorkingDay> Days)
{
    /// <summary>The tenant time zone's id, for the wire.</summary>
    public string TimeZoneId => TimeZone.Id;

    /// <summary>The tenant-local calendar day an instant falls on.</summary>
    public DateOnly LocalDateOf(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

    /// <summary>The day entry for a local date, or null when it is outside the requested range.</summary>
    public WorkingDay? DayOf(DateOnly date) => Days.FirstOrDefault(d => d.Date == date);
}

/// <summary>A ring's answer: the daily window it prescribes, in local wall-clock time.</summary>
public sealed record WorkingHoursRingSchedule(TimeOnly Start, TimeOnly End);

/// <summary>
/// An EARLIER ring of the chain (person, assignment, unit, legal entity). The extension point: v1 registers none,
/// so every person falls through to the tenant default. A ring answers null when it has nothing to say for the
/// person, and the next ring is asked.
/// </summary>
public interface IWorkingHoursRing
{
    /// <summary>One of <see cref="WorkingHoursSources"/> except <see cref="WorkingHoursSources.TenantDefault"/>.</summary>
    string Source { get; }

    /// <summary>Lower is asked first: person 10, assignment 20, unit 30, legal entity 40.</summary>
    int Order { get; }

    Task<WorkingHoursRingSchedule?> ResolveAsync(Guid userId, CancellationToken ct = default);
}
