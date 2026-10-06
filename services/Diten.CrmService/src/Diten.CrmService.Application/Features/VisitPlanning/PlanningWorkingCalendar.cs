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
/// per run (the range is resolved up front, so the run itself is the cache). Half days are not modelled (Faz 3).</para>
/// <para><b>Fallback, stated not hidden.</b> When the country cannot be established or the calendar does not answer (forbidden,
/// missing, unreachable), the FIRST failure stops the asking and Saturday + Sunday are excluded instead; the result is then
/// <see cref="PlanningCalendarStatuses.Unresolved"/> with the reason, so the screen can say "holidays were not
/// considered". A partially-resolved range is never mixed with the fallback.</para>
/// </summary>
public sealed class PlanningWorkingCalendar
{
    private readonly ICycleCapacityCountryResolver _countries;
    private readonly IWorkingDayChecker _checker;

    public PlanningWorkingCalendar(ICycleCapacityCountryResolver countries, IWorkingDayChecker checker)
    {
        _countries = countries;
        _checker = checker;
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
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var answer = await _checker.IsWorkingDayAsync(countryCode, country.LegalEntityId, day, cancellationToken);
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
        }

        return new PlanningCalendarResult(PlanningCalendarStatuses.Resolved, null, null, nonWorking);
    }

    /// <summary>Saturday + Sunday of the range — the documented fallback when the calendar cannot answer.</summary>
    public static PlanningCalendarResult WeekendFallback(DateOnly from, DateOnly to, string reasonCode, string reason)
    {
        var weekend = new List<DateOnly>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                weekend.Add(day);
            }
        }

        return new PlanningCalendarResult(PlanningCalendarStatuses.Unresolved, reasonCode, reason, weekend);
    }
}

/// <summary>The non-working days of one planning run plus whether the calendar actually answered.</summary>
public sealed record PlanningCalendarResult(
    string Status,
    string? ReasonCode,
    string? Reason,
    IReadOnlyList<DateOnly> NonWorkingDates);

/// <summary><c>resolved</c> — the platform calendar answered every day; <c>unresolved</c> — the Sat/Sun fallback ran.</summary>
public static class PlanningCalendarStatuses
{
    public const string Resolved = "resolved";
    public const string Unresolved = "unresolved";
}
