using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-FIX-1 (C2 + C3) — which days of a planning run are NOT working days. The planner hands the answer to the route
/// optimizer (<see cref="RouteOptimization.OptimizationPeriod.NonWorkingDates"/>), so a weekend, a public holiday or a
/// company closure is never a candidate day.
/// <para><b>Source.</b> The platform working calendar, through the existing tenant-openable seam
/// (<see cref="IWorkingDayChecker"/>, op <c>is-working-day</c>), for the country the cycle period / capacity resolves to
/// (<see cref="ICycleCapacityCountryResolver"/> — the same decision the capacity estimate uses). Each date is asked ONCE
/// per run (the range is resolved up front, so the run itself is the cache).</para>
/// <para><b>WP-VP-3B — day kinds (MK-9).</b> Every day of the range is classified <c>working</c> · <c>half</c> ·
/// <c>holiday</c> · <c>weekend</c> (<see cref="PlanningDayKinds"/>): a working day the platform marks a half day
/// (<see cref="WorkingDayCheckResult.IsHalfDay"/>) is <c>half</c>; a non-working day is <c>weekend</c> when the platform's
/// reason says weekend (or, without such a reason, it is a Saturday / Sunday), otherwise <c>holiday</c> (public holiday,
/// company closure).</para>
/// <para><b>WP-VP-3B — request count (4b).</b> The platform has no "day kinds of a range" operation (only the per-day
/// <c>is-working-day</c> carries the half-day flag; <c>working-days-between</c> is a bare count), so the first run still
/// asks day by day. Resolved answers are then kept for a few minutes in the tenant-keyed
/// <see cref="PlanningCalendarDayCache"/>, so every further preview / approve of the same plan (each manual reorder is a
/// preview) asks nothing. Failures are never cached.</para>
/// <para><b>Fallback, stated not hidden.</b> When the country cannot be established or the calendar does not answer (forbidden,
/// missing, unreachable), the FIRST failure stops the asking and Saturday + Sunday are excluded instead; the result is then
/// <see cref="PlanningCalendarStatuses.Unresolved"/> with the reason, so the screen can say "holidays were not
/// considered". A partially-resolved range is never mixed with the fallback.</para>
/// </summary>
public sealed class PlanningWorkingCalendar
{
    private readonly ICycleCapacityCountryResolver _countries;
    private readonly IWorkingDayChecker _checker;
    private readonly PlanningCalendarDayCache? _cache;
    private readonly ITenantContext? _tenant;

    public PlanningWorkingCalendar(
        ICycleCapacityCountryResolver countries,
        IWorkingDayChecker checker,
        PlanningCalendarDayCache? cache = null,
        ITenantContext? tenant = null)
    {
        _countries = countries;
        _checker = checker;
        _cache = cache;
        _tenant = tenant;
    }

    public async Task<PlanningCalendarResult> ResolveAsync(
        CyclePeriodSnapshot period,
        string? authoredCountryCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return new PlanningCalendarResult(PlanningCalendarStatuses.Resolved, null, null, Array.Empty<DateOnly>());
        }

        var country = _countries.Resolve(period, authoredCountryCode);
        if (country.CountryCode is not { } countryCode)
        {
            return WeekendFallback(
                from, to, CycleCapacityReasonCodes.CountryUnderivable,
                "No calendar country could be established for this cycle period.");
        }

        var nonWorking = new List<DateOnly>();
        var kinds = new Dictionary<DateOnly, string>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var answer = await AskAsync(countryCode, country.LegalEntityId, day, cancellationToken);
            if (!string.Equals(answer.Resolution, CycleCapacityResolutions.Resolved, StringComparison.Ordinal)
                || answer.IsWorkingDay is not { } isWorkingDay)
            {
                return WeekendFallback(
                    from, to,
                    answer.ReasonCodes.FirstOrDefault() ?? CycleCapacityReasonCodes.CalendarUnresolved,
                    answer.Reason);
            }

            if (!isWorkingDay)
            {
                nonWorking.Add(day);
            }

            kinds[day] = KindOf(day, answer);
        }

        return new PlanningCalendarResult(PlanningCalendarStatuses.Resolved, null, null, nonWorking, kinds);
    }

    /// <summary>WP-VP-3B — one day's answer, from the tenant-keyed cache when a resolved answer is still fresh.</summary>
    private async Task<WorkingDayCheckResult> AskAsync(
        string countryCode, Guid? legalEntityId, DateOnly day, CancellationToken cancellationToken)
    {
        var key = _cache is not null && _tenant?.TenantId is { } tenantId
            ? new PlanningCalendarDayCache.Key(tenantId, countryCode, legalEntityId, day)
            : (PlanningCalendarDayCache.Key?)null;
        if (key is { } k && _cache!.TryGet(k, out var cached))
        {
            return cached;
        }

        var answer = await _checker.IsWorkingDayAsync(countryCode, legalEntityId, day, cancellationToken);
        if (key is { } put && string.Equals(answer.Resolution, CycleCapacityResolutions.Resolved, StringComparison.Ordinal)
            && answer.IsWorkingDay is not null)
        {
            _cache!.Set(put, answer);
        }

        return answer;
    }

    /// <summary>WP-VP-3B — a day's kind from the platform's answer (rule in the class summary).</summary>
    public static string KindOf(DateOnly day, WorkingDayCheckResult answer)
    {
        if (answer.IsWorkingDay == true)
        {
            return answer.IsHalfDay ? PlanningDayKinds.Half : PlanningDayKinds.Working;
        }

        var reasons = answer.ReasonCodes ?? Array.Empty<string>();
        if (reasons.Any(r => r.StartsWith("weekend", StringComparison.OrdinalIgnoreCase)))
        {
            return PlanningDayKinds.Weekend;
        }

        if (reasons.Any(r => r is "public_holiday" or "company_closure"))
        {
            return PlanningDayKinds.Holiday;
        }

        return day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? PlanningDayKinds.Weekend : PlanningDayKinds.Holiday;
    }

    /// <summary>Saturday + Sunday of the range — the documented fallback when the calendar cannot answer.</summary>
    public static PlanningCalendarResult WeekendFallback(DateOnly from, DateOnly to, string reasonCode, string reason)
    {
        var weekend = new List<DateOnly>();
        var kinds = new Dictionary<DateOnly, string>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (isWeekend)
            {
                weekend.Add(day);
            }

            kinds[day] = isWeekend ? PlanningDayKinds.Weekend : PlanningDayKinds.Working;
        }

        return new PlanningCalendarResult(PlanningCalendarStatuses.Unresolved, reasonCode, reason, weekend, kinds);
    }
}

/// <summary>The non-working days of one planning run plus whether the calendar actually answered. WP-VP-3B —
/// <see cref="DayKinds"/>: every day of the range with its <see cref="PlanningDayKinds"/> kind (null on an empty range).</summary>
public sealed record PlanningCalendarResult(
    string Status,
    string? ReasonCode,
    string? Reason,
    IReadOnlyList<DateOnly> NonWorkingDates,
    IReadOnlyDictionary<DateOnly, string>? DayKinds = null)
{
    /// <summary>A day's kind; a day outside the resolved range is <c>working</c> unless it is listed non-working.</summary>
    public string KindOf(DateOnly day)
        => DayKinds is not null && DayKinds.TryGetValue(day, out var kind)
            ? kind
            : NonWorkingDates.Contains(day)
                ? (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? PlanningDayKinds.Weekend : PlanningDayKinds.Holiday)
                : PlanningDayKinds.Working;

    /// <summary>The half days of the range, ascending.</summary>
    public IReadOnlyList<DateOnly> HalfDayDates
        => DayKinds is null
            ? Array.Empty<DateOnly>()
            : DayKinds.Where(d => d.Value == PlanningDayKinds.Half).Select(d => d.Key).OrderBy(d => d).ToList();
}

/// <summary><c>resolved</c> — the platform calendar answered every day; <c>unresolved</c> — the Sat/Sun fallback ran.</summary>
public static class PlanningCalendarStatuses
{
    public const string Resolved = "resolved";
    public const string Unresolved = "unresolved";
}

/// <summary>WP-VP-3B — the kind of one calendar day in a planning run.</summary>
public static class PlanningDayKinds
{
    public const string Working = "working";
    public const string Half = "half";
    public const string Holiday = "holiday";
    public const string Weekend = "weekend";
}
