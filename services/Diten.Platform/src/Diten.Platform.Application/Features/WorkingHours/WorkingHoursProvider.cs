using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.WorkingHours;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 — the v1 resolution of <see cref="IWorkingHoursProvider"/>.
///
/// <para><b>The chain.</b> Registered <see cref="IWorkingHoursRing"/>s are asked in <see cref="IWorkingHoursRing.Order"/>
/// (person → assignment → unit → legal entity); the first that answers wins. v1 registers none, so the TENANT
/// DEFAULT answers for everybody. This class is the ONLY reader of the tenant's default hours
/// (<c>TaskCalendarGuardTests</c>).</para>
///
/// <para><b>The day type</b> comes from <see cref="IWorkingCalendarProvider"/>, scoped to the person's home unit and
/// its legal entity (primary seat first, as <see cref="ITaskSeatDirectory.ActiveForUserAsync"/> orders them) and the
/// tenant's country. When the calendar cannot answer, the day counts as a working day and says
/// <see cref="WorkingDay.CalendarUnresolved"/> — a guessed day off would hide work, a flagged working day does not.</para>
///
/// <para><b>Never throws</b>: an unreadable tenant or org chart degrades to UTC / no scope, and is logged.</para>
/// </summary>
public sealed class WorkingHoursProvider : IWorkingHoursProvider
{
    /// <summary>A range longer than this is answered with no days — the calendar feed caps at 42 anyway.</summary>
    private const int MaxDays = 366;

    private readonly ITenantRegistryRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IWorkingCalendarProvider _calendar;
    private readonly IReadOnlyList<IWorkingHoursRing> _rings;
    private readonly ILogger<WorkingHoursProvider>? _logger;
    private readonly IOrganizationReportingGraphRepository? _unitGraph;

    public WorkingHoursProvider(
        ITenantRegistryRepository tenants,
        ITenantContext tenantContext,
        ITaskSeatDirectory seats,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IWorkingCalendarProvider calendar,
        IEnumerable<IWorkingHoursRing> rings,
        ILogger<WorkingHoursProvider>? logger = null,
        IOrganizationReportingGraphRepository? unitGraph = null)
    {
        // BL-484 — the units of many people in one read. Optional: a host that does not register the graph repository
        // (older test hosts) reads each distinct unit by id instead, with the same answer.
        _unitGraph = unitGraph;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _seats = seats;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _calendar = calendar;
        _rings = rings.OrderBy(r => r.Order).ToList();
        _logger = logger;
    }

    public async Task<WorkingHoursResult> GetWorkingWindowsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenant = await ReadTenantAsync(ct);
        var timeZone = ResolveTimeZone(tenant?.Settings?.Timezone, tenant?.DefaultTimezone);

        if (IsUnanswerable(from, to))
        {
            return new WorkingHoursResult(timeZone, []);
        }

        // ── Which days: the working calendar, scoped to the person's home unit / legal entity ──────────────
        var scope = await ResolveCalendarScopeAsync(userId, tenant?.Country, ct);

        return await ComposeAsync(tenant, timeZone, userId, from, to, scope, dayKinds: null, ct);
    }

    /// <summary>
    /// BL-484 — many (person, range) questions with the inputs read ONCE: the tenant, the seats, the positions, the
    /// units. Every answer goes through the same <see cref="ComposeAsync"/> as the single question, with the scope the
    /// single question would have resolved for that person, so the two cannot answer differently. A (day, scope) the
    /// calendar already answered inside this call is not asked again.
    /// </summary>
    public async Task<IReadOnlyDictionary<WorkingHoursRequest, WorkingHoursResult>> GetWorkingWindowsForManyAsync(
        IReadOnlyCollection<WorkingHoursRequest> requests, CancellationToken ct = default)
    {
        var answers = new Dictionary<WorkingHoursRequest, WorkingHoursResult>();
        var wanted = requests.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return answers;
        }

        var tenant = await ReadTenantAsync(ct);
        var timeZone = ResolveTimeZone(tenant?.Settings?.Timezone, tenant?.DefaultTimezone);

        var answerable = wanted.Where(r => !IsUnanswerable(r.From, r.To)).ToList();
        var scopes = await ResolveCalendarScopesAsync(answerable.Select(r => r.UserId).Distinct().ToList(), tenant?.Country, ct);
        var dayKinds = new Dictionary<(DateOnly Date, WorkingCalendarScope Scope), (string, string?, bool, bool)>();

        foreach (var request in wanted)
        {
            answers[request] = IsUnanswerable(request.From, request.To)
                ? new WorkingHoursResult(timeZone, [])
                : await ComposeAsync(tenant, timeZone, request.UserId, request.From, request.To, scopes[request.UserId], dayKinds, ct);
        }

        return answers;
    }

    private static bool IsUnanswerable(DateOnly from, DateOnly to)
        => to < from || to.DayNumber - from.DayNumber + 1 > MaxDays;

    /// <summary>The days of one person's range, from inputs that were read by the caller — one person at a time or for
    /// many at once. <paramref name="dayKinds"/> remembers the calendar's answers across the people of one call.</summary>
    private async Task<WorkingHoursResult> ComposeAsync(
        Domain.Entities.Tenant? tenant, TimeZoneInfo timeZone, Guid userId, DateOnly from, DateOnly to,
        WorkingCalendarScope? scope,
        Dictionary<(DateOnly Date, WorkingCalendarScope Scope), (string, string?, bool, bool)>? dayKinds,
        CancellationToken ct)
    {
        // ── Which window: the first ring that answers, else the tenant default ──────────────────────────────
        var (schedule, source) = await ResolveScheduleAsync(userId, ct);
        schedule ??= tenant is null
            ? null
            : new WorkingHoursRingSchedule(tenant.DefaultWorkdayStart, tenant.DefaultWorkdayEnd);

        // ── How much: the tenant's daily target (MOD-0280-FU01 D13), never derived from the window ────────────
        var dailyTarget = tenant is null ? 0 : Math.Max(0, tenant.DefaultDailyTargetMinutes);

        var days = new List<WorkingDay>(to.DayNumber - from.DayNumber + 1);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            (string, string?, bool, bool) resolved;
            if (scope is null || dayKinds is null)
            {
                resolved = await ResolveDayKindAsync(date, scope, ct);
            }
            else if (!dayKinds.TryGetValue((date, scope), out resolved))
            {
                resolved = dayKinds[(date, scope)] = await ResolveDayKindAsync(date, scope, ct);
            }

            var (kind, holidayName, unresolved, halfDay) = resolved;
            var windows = kind == WorkingDayKinds.WorkingDay && schedule is not null
                ? WindowFor(date, schedule, timeZone)
                : [];
            var target = kind != WorkingDayKinds.WorkingDay ? 0 : halfDay ? dailyTarget / 2 : dailyTarget;
            days.Add(new WorkingDay(date, kind, holidayName, windows, source, unresolved, target, halfDay));
        }

        return new WorkingHoursResult(timeZone, days);
    }

    private async Task<Domain.Entities.Tenant?> ReadTenantAsync(CancellationToken ct)
    {
        try
        {
            return await _tenants.GetByIdAsync(_tenantContext.TenantId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Working hours: tenant {TenantId} could not be read; answering with UTC and no window.",
                _tenantContext.TenantId);
            return null;
        }
    }

    private async Task<(WorkingHoursRingSchedule? Schedule, string Source)> ResolveScheduleAsync(
        Guid userId, CancellationToken ct)
    {
        foreach (var ring in _rings)
        {
            try
            {
                var answer = await ring.ResolveAsync(userId, ct);
                if (answer is not null)
                {
                    return (answer, ring.Source);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A broken ring must not take the calendar down; the next ring (ultimately the tenant) answers.
                _logger?.LogError(ex, "Working hours ring {Source} failed for {UserId}; falling through.", ring.Source, userId);
            }
        }

        return (null, WorkingHoursSources.TenantDefault);
    }

    private async Task<WorkingCalendarScope?> ResolveCalendarScopeAsync(Guid userId, string? country, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        try
        {
            var seats = await _seats.ActiveForUserAsync(userId, ct);
            if (seats.Count == 0)
            {
                return new WorkingCalendarScope(country);
            }

            var position = await _positions.GetByIdAsync(seats[0].PositionId, ct);
            if (position is null)
            {
                return new WorkingCalendarScope(country);
            }

            var unit = await _organizationUnits.GetByIdAsync(position.OrganizationUnitId, ct);
            return new WorkingCalendarScope(country, position.OrganizationUnitId, unit?.LegalEntityId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Working hours: org scope for {UserId} could not be read; using the country only.", userId);
            return new WorkingCalendarScope(country);
        }
    }

    /// <summary>
    /// <see cref="ResolveCalendarScopeAsync"/> for many people: one read of the seats, one of their home positions, one
    /// of those positions' units. Person by person the rule is the single one, step for step — no country → no scope;
    /// no seat → the country; the PRIMARY seat first; a position that is gone → the country; a unit that is gone → the
    /// position's unit with no legal entity; the org chart unreadable → the country for everybody.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, WorkingCalendarScope?>> ResolveCalendarScopesAsync(
        IReadOnlyCollection<Guid> userIds, string? country, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return userIds.ToDictionary(id => id, _ => (WorkingCalendarScope?)null);
        }

        try
        {
            var wanted = userIds.ToHashSet();
            var homePositionByUser = (await _seats.ActiveAsync(ct))
                .Where(seat => wanted.Contains(seat.UserId))
                .GroupBy(seat => seat.UserId)
                // The same order ActiveForUserAsync gives one person: PRIMARY first, the rest as stored.
                .ToDictionary(seats => seats.Key, seats => seats.OrderBy(seat => seat.AssignmentType).First().PositionId);

            var positions = (await _positions.GetByIdsAsync(homePositionByUser.Values.Distinct().ToList(), ct))
                .ToDictionary(position => position.Id);
            var units = await ReadUnitsAsync(positions.Values.Select(position => position.OrganizationUnitId).Distinct().ToList(), ct);

            return userIds.ToDictionary(id => id, id =>
            {
                if (!homePositionByUser.TryGetValue(id, out var positionId) || !positions.TryGetValue(positionId, out var position))
                {
                    return (WorkingCalendarScope?)new WorkingCalendarScope(country);
                }

                units.TryGetValue(position.OrganizationUnitId, out var unit);
                return new WorkingCalendarScope(country, position.OrganizationUnitId, unit?.LegalEntityId);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Working hours: org scope for {Count} people could not be read; using the country only.", userIds.Count);
            return userIds.ToDictionary(id => id, _ => (WorkingCalendarScope?)new WorkingCalendarScope(country));
        }
    }

    private async Task<IReadOnlyDictionary<Guid, Domain.Entities.Organization.OrganizationUnit>> ReadUnitsAsync(
        IReadOnlyCollection<Guid> unitIds, CancellationToken ct)
    {
        if (unitIds.Count == 0)
        {
            return new Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit>();
        }

        if (_unitGraph is not null)
        {
            return (await _unitGraph.GetByIdsAsync(unitIds, ct)).ToDictionary(unit => unit.Id);
        }

        var units = new Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit>();
        foreach (var unitId in unitIds)
        {
            if (await _organizationUnits.GetByIdAsync(unitId, ct) is { } unit)
            {
                units[unitId] = unit;
            }
        }

        return units;
    }

    private async Task<(string Kind, string? HolidayName, bool Unresolved, bool HalfDay)> ResolveDayKindAsync(
        DateOnly date, WorkingCalendarScope? scope, CancellationToken ct)
    {
        if (scope is null)
        {
            return (WorkingDayKinds.WorkingDay, null, true, false);
        }

        var answer = await _calendar.IsWorkingDayAsync(date, scope, ct);
        if (answer.Resolution != WorkingCalendarResolution.Resolved || answer.IsWorkingDay is null)
        {
            return (WorkingDayKinds.WorkingDay, null, true, false);
        }

        if (answer.IsWorkingDay.Value)
        {
            // A half-day holiday is reported by the calendar as a WORKING day that still carries its holiday
            // (half_day_treated_as_working). The window and the holiday name stay exactly as before — the calendar
            // feed reads both — and only the target halves (D13).
            return (WorkingDayKinds.WorkingDay, null, false, answer.Holiday is { IsHalfDay: true });
        }

        return answer.Holiday is { } holiday
            ? (WorkingDayKinds.Holiday, holiday.DayName, false, false)
            : (WorkingDayKinds.Weekend, null, false, false);
    }

    private static IReadOnlyList<WorkingWindow> WindowFor(
        DateOnly date, WorkingHoursRingSchedule schedule, TimeZoneInfo timeZone)
    {
        if (schedule.End <= schedule.Start)
        {
            return [];
        }

        return [new WorkingWindow(ToUtc(date, schedule.Start, timeZone), ToUtc(date, schedule.End, timeZone))];
    }

    /// <summary>A local wall-clock moment as a UTC instant. A time skipped by a DST jump moves forward an hour.</summary>
    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    /// <summary>
    /// The tenant's effective zone: the runtime override (<c>Settings.Timezone</c>, what the settings screen writes)
    /// when present, else the profile default. An id the host cannot resolve answers UTC rather than throwing.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string? runtime, string? profileDefault)
    {
        foreach (var id in new[] { runtime, profileDefault })
        {
            if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.Utc;
    }
}
