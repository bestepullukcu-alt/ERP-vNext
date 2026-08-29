using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Services;

/// <summary>
/// MOD-0155 FU06 — "resolve the months, then do the arithmetic", in ONE place.
///
/// <para>Two callers need this: the SAVED capacity's <c>/calculation</c> endpoint, and the live
/// <c>/calculation-preview</c> the form calls while an author is still typing. The inputs arrive differently — one
/// loads a stored row, the other builds a transient one — but from the moment there is a capacity and a period, the
/// rule is identical. Leaving that rule in both handlers would put the FAIL-CLOSED policy in two places, and the day
/// they drift the preview would show a number the saved record refuses to.</para>
///
/// <para><b>It writes nothing and it persists nothing.</b> It holds no repository at all: a capacity is handed to it,
/// never fetched or stored by it. That is what lets the preview path pass a transient object with no id and no tenant
/// row behind it.</para>
///
/// <para><b>The arithmetic stays where it was.</b> <see cref="CycleCapacityCalculator"/> is untouched and still pure —
/// this class only decides WHICH months to ask about and what to do when the calendar cannot answer.</para>
/// </summary>
public sealed class CycleCapacityEstimator
{
    private readonly ICycleCapacityCountryResolver _countries;
    private readonly IWorkingDayCounter _workingDays;

    public CycleCapacityEstimator(ICycleCapacityCountryResolver countries, IWorkingDayCounter workingDays)
    {
        _countries = countries;
        _workingDays = workingDays;
    }

    /// <summary>
    /// The estimate, plus the address the calendar was actually asked about. The calculation's own <c>Resolution</c>
    /// says whether the figures are usable — a caller must branch on it rather than reading
    /// <c>TotalVisitNumber</c> and finding null.
    /// <para><see cref="CalendarCountryCode"/> is the RESOLVED code, which is not always the one the caller supplied:
    /// a country-scoped period derives its own and the caller's value is ignored. It is reported so a surface can show
    /// the country the number was actually computed against — displaying the requested one instead would put two
    /// different countries on the same screen.</para>
    /// </summary>
    public sealed record Result(
        CycleCapacityCalculator.CapacityCalculation Calculation,
        Guid? CalendarLegalEntityId,
        string? CalendarCountryCode);

    /// <summary>
    /// Estimates one capacity against one period.
    /// <para><paramref name="period"/> may be null — a saved capacity whose period can no longer be read is a real
    /// situation, and it answers "unresolved" rather than throwing, because the window is what the months come
    /// from.</para>
    /// </summary>
    public async Task<Result> EstimateAsync(
        CapacityEntity capacity,
        CyclePeriodSnapshot? period,
        CancellationToken cancellationToken)
    {
        if (period is null)
        {
            // The window comes from the period, so without it there is nothing to count.
            return Unresolved(
                capacity,
                null,
                CycleCapacityResolutions.CalendarUnresolved,
                new[] { CycleCapacityReasonCodes.PeriodNotFound },
                "The pinned cycle period could not be read, so the period window is unknown and no month can be "
                + "counted.");
        }

        var country = _countries.Resolve(period, capacity.CalendarCountryCode);
        if (country.CountryCode is null)
        {
            return Unresolved(
                capacity,
                null,
                CycleCapacityResolutions.CalendarUnresolved,
                new[] { CycleCapacityReasonCodes.CountryUnderivable },
                "No calendar country could be established for this capacity, so the working calendar cannot be "
                + "queried.");
        }

        var windows = CycleCapacityMonthRules.Derive(period.StartDate, period.EndDate);
        if (windows.Count == 0)
        {
            return Unresolved(
                capacity,
                country.LegalEntityId,
                CycleCapacityResolutions.CalendarUnresolved,
                new[] { CycleCapacityReasonCodes.MonthsRequired },
                "The pinned cycle period covers no months, so there is nothing to estimate.",
                country.CountryCode);
        }

        var resolved = new List<CycleCapacityCalculator.ResolvedMonth>(windows.Count);

        // One call per month. The working calendar has no bulk range operation and its service is outside this
        // domain's write scope, so it cannot be given one here (F-WC-BULK). A period is two to four months in
        // practice and twelve at most, which the per-call budget absorbs.
        foreach (var window in windows)
        {
            var count = await _workingDays.CountAsync(
                country.CountryCode, country.LegalEntityId, window.FromDate(), window.ToDate(), cancellationToken);

            if (!string.Equals(count.Resolution, CycleCapacityResolutions.Resolved, StringComparison.Ordinal)
                || count.WorkingDays is not { } workingDays)
            {
                // Fail-closed: the FIRST unresolved month ends the whole calculation. Continuing would produce a table
                // that is silently missing a month.
                return Unresolved(
                    capacity,
                    country.LegalEntityId,
                    count.Resolution,
                    count.ReasonCodes,
                    $"{window.Year}-{window.MonthNumber:00} could not be resolved: {count.Reason} "
                    + "No partial estimate is produced.",
                    country.CountryCode);
            }

            resolved.Add(new CycleCapacityCalculator.ResolvedMonth(window, workingDays));
        }

        return new Result(
            CycleCapacityCalculator.Calculate(capacity, resolved), country.LegalEntityId, country.CountryCode);
    }

    private static Result Unresolved(
        CapacityEntity capacity,
        Guid? legalEntityId,
        string resolution,
        IReadOnlyList<string> reasonCodes,
        string reason,
        string? countryCode = null)
        => new(
            CycleCapacityCalculator.Unresolved(capacity, resolution, reasonCodes, reason),
            legalEntityId,
            countryCode);
}
