using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Provider;

/// <summary>
/// The read-only working-day seam — the actual product of this capability. Every consumer (field visit planning,
/// MicroTarget cadence, project and finance schedulers) asks THIS; nobody copies a weekend list, nobody re-implements
/// the arithmetic, and nobody in this process calls it over HTTP.
/// <para><b>It never writes and never throws into a consumer.</b> An internal failure comes back as an unresolved
/// result, because an exception would tempt a caller to fall back to "probably a working day".</para>
/// </summary>
public interface IWorkingCalendarProvider
{
    Task<WorkingDayResult> IsWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default);

    Task<HolidayLookupResult> GetHolidayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default);

    Task<WorkingDateResult> NextWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default);

    Task<WorkingDateResult> AddWorkingDaysAsync(DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct = default);

    Task<WorkingDayCountResult> WorkingDaysBetweenAsync(DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct = default);
}

public sealed class WorkingCalendarProvider : IWorkingCalendarProvider
{
    /// <summary>Bounds the date walk so a pathological request can never spin. Five years of days is far beyond any
    /// legitimate "next working day" or "add N working days" query.</summary>
    private const int MaxScanDays = 1830;

    private readonly IWorkingCalendarRepository _repository;
    private readonly IPlatformLookupProvider _lookups;
    private readonly ILogger<WorkingCalendarProvider>? _logger;

    public WorkingCalendarProvider(
        IWorkingCalendarRepository repository,
        IPlatformLookupProvider lookups,
        ILogger<WorkingCalendarProvider>? logger = null)
    {
        _repository = repository;
        _lookups = lookups;
        _logger = logger;
    }

    public async Task<WorkingDayResult> IsWorkingDayAsync(
        DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        var country = Normalize(scope.CountryCode);

        if (!await IsKnownCountryAsync(country, ct))
        {
            return new WorkingDayResult(
                WorkingCalendarResolution.CountryUnknown, null, date, country, null, null, null,
                $"'{country}' is not a published country in the reference data set.",
                new[] { WorkingCalendarReasonCodes.CountryUnknown });
        }

        try
        {
            var (countryCal, overrideCal) = await LoadAsync(
                country, date.Year, scope.OrganizationUnitId, scope.LegalEntityId, ct);
            return WorkingCalendarResolveEngine.ResolveWorkingDay(date, country, countryCal, overrideCal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Working-calendar resolution failed for {Country} {Date}; reporting unresolved.", country, date);
            return new WorkingDayResult(
                WorkingCalendarResolution.CalendarMissing, null, date, country, null, null, null,
                "The working calendar could not be read; the answer is reported as unresolved rather than guessed.",
                new[] { WorkingCalendarReasonCodes.CalendarMissing });
        }
    }

    public async Task<HolidayLookupResult> GetHolidayAsync(
        DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        var result = await IsWorkingDayAsync(date, scope, ct);

        return new HolidayLookupResult(
            result.Resolution,
            result.Holiday,
            result.Date,
            result.CountryCode,
            result.ResolvedCalendarId,
            result.ResolvedOverrideCalendarId,
            result.Holiday is null && result.Resolution == WorkingCalendarResolution.Resolved
                ? $"{date:yyyy-MM-dd} is not a holiday or closure."
                : result.SelectionReason,
            result.ReasonCodes);
    }

    public async Task<WorkingDateResult> NextWorkingDayAsync(
        DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        // "Next" is strictly after the input date — the input itself is never the answer.
        var cursor = date.AddDays(1);

        for (var scanned = 0; scanned < MaxScanDays; scanned++)
        {
            var probe = await IsWorkingDayAsync(cursor, scope, ct);
            if (probe.Resolution != WorkingCalendarResolution.Resolved)
            {
                return Unresolved(probe, date, scope);
            }

            if (probe.IsWorkingDay == true)
            {
                return new WorkingDateResult(
                    WorkingCalendarResolution.Resolved, cursor, date, probe.CountryCode,
                    $"The first working day after {date:yyyy-MM-dd} is {cursor:yyyy-MM-dd}.",
                    probe.ReasonCodes);
            }

            cursor = cursor.AddDays(1);
        }

        return new WorkingDateResult(
            WorkingCalendarResolution.CalendarMissing, null, date, Normalize(scope.CountryCode),
            $"No working day was found within {MaxScanDays} days of {date:yyyy-MM-dd}.",
            new[] { WorkingCalendarReasonCodes.CalendarMissing });
    }

    /// <summary>
    /// Adds N working days. <c>days = 0</c> returns the start date when it is itself a working day, otherwise the
    /// next working day — stated in the contract rather than left to chance. Negative N walks backwards.
    /// </summary>
    public async Task<WorkingDateResult> AddWorkingDaysAsync(
        DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        if (days == 0)
        {
            var probe = await IsWorkingDayAsync(start, scope, ct);
            if (probe.Resolution != WorkingCalendarResolution.Resolved)
            {
                return Unresolved(probe, start, scope);
            }

            if (probe.IsWorkingDay == true)
            {
                return new WorkingDateResult(
                    WorkingCalendarResolution.Resolved, start, start, probe.CountryCode,
                    $"{start:yyyy-MM-dd} is already a working day, so adding zero working days returns it unchanged.",
                    probe.ReasonCodes);
            }

            return await NextWorkingDayAsync(start, scope, ct);
        }

        var step = days > 0 ? 1 : -1;
        var remaining = Math.Abs(days);
        var cursor = start;

        for (var scanned = 0; scanned < MaxScanDays && remaining > 0; scanned++)
        {
            cursor = cursor.AddDays(step);

            var probe = await IsWorkingDayAsync(cursor, scope, ct);
            if (probe.Resolution != WorkingCalendarResolution.Resolved)
            {
                return Unresolved(probe, start, scope);
            }

            if (probe.IsWorkingDay == true)
            {
                remaining--;
            }
        }

        if (remaining > 0)
        {
            return new WorkingDateResult(
                WorkingCalendarResolution.CalendarMissing, null, start, Normalize(scope.CountryCode),
                $"Could not find {Math.Abs(days)} working days within {MaxScanDays} days of {start:yyyy-MM-dd}.",
                new[] { WorkingCalendarReasonCodes.CalendarMissing });
        }

        return new WorkingDateResult(
            WorkingCalendarResolution.Resolved, cursor, start, Normalize(scope.CountryCode),
            $"{days:+#;-#;0} working days from {start:yyyy-MM-dd} is {cursor:yyyy-MM-dd}.",
            new[] { WorkingCalendarReasonCodes.WorkingDay });
    }

    /// <summary>
    /// Counts working days in [from, to] inclusive. If ANY day in the range falls in a year with no active calendar
    /// the whole call comes back unresolved with a null count — a partial count would look authoritative and be wrong.
    /// </summary>
    public async Task<WorkingDayCountResult> WorkingDaysBetweenAsync(
        DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        var country = Normalize(scope.CountryCode);

        if (from > to)
        {
            return new WorkingDayCountResult(
                WorkingCalendarResolution.InvalidRange, null, from, to, country,
                "The start date is after the end date; no count is produced (a negative count is never invented).",
                new[] { WorkingCalendarReasonCodes.InvalidDateRange });
        }

        if ((to.DayNumber - from.DayNumber) > MaxScanDays)
        {
            return new WorkingDayCountResult(
                WorkingCalendarResolution.InvalidRange, null, from, to, country,
                $"The range exceeds the {MaxScanDays}-day limit for a single count.",
                new[] { WorkingCalendarReasonCodes.InvalidDateRange });
        }

        var count = 0;
        for (var cursor = from; cursor <= to; cursor = cursor.AddDays(1))
        {
            var probe = await IsWorkingDayAsync(cursor, scope, ct);
            if (probe.Resolution != WorkingCalendarResolution.Resolved)
            {
                return new WorkingDayCountResult(
                    probe.Resolution, null, from, to, probe.CountryCode,
                    $"{cursor:yyyy-MM-dd} could not be resolved ({probe.Resolution}); no partial count is returned.",
                    probe.ReasonCodes);
            }

            if (probe.IsWorkingDay == true)
            {
                count++;
            }
        }

        return new WorkingDayCountResult(
            WorkingCalendarResolution.Resolved, count, from, to, country,
            $"{count} working day(s) between {from:yyyy-MM-dd} and {to:yyyy-MM-dd} inclusive.",
            new[] { WorkingCalendarReasonCodes.WorkingDay });
    }

    // ── internals ────────────────────────────────────────────────────────────

    private static string Normalize(string? countryCode)
        => (countryCode ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<bool> IsKnownCountryAsync(string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var options = await _lookups.GetLookupOptionsAsync(PlatformLookupKeys.Countries, ct);

        // A missing/empty reference set means we cannot confirm the country. Fail closed rather than accepting
        // free text — the hardcoded fallback list is exactly what this rule forbids.
        if (options is null || options.Count == 0)
        {
            return false;
        }

        return options.Any(o =>
            string.Equals(o.Code, countryCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Value, countryCode, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(Wc? Country, Wc? Override)> LoadAsync(
        string countryCode, int year, Guid? organizationUnitId, Guid? legalEntityId, CancellationToken ct)
    {
        var countryRows = await _repository.GetCountryLayerAsync(countryCode, year, ct);
        var countryCal = countryRows.FirstOrDefault(c => c.IsActive());

        var overrideRows = await _repository.GetTenantOverridesAsync(
            countryCode, year, organizationUnitId, legalEntityId, ct);

        // Most specific wins: organization-unit > legal-entity > tenant. Exactly one row is passed to the engine;
        // rows are never merged with each other.
        var overrideCal = overrideRows
            .Where(c => c.IsActive())
            .OrderByDescending(c => c.ScopeType switch
            {
                WorkingCalendarScopeType.OrganizationUnit => 3,
                WorkingCalendarScopeType.LegalEntity => 2,
                WorkingCalendarScopeType.Tenant => 1,
                _ => 0
            })
            .ThenBy(c => c.CalendarCode, StringComparer.Ordinal)
            .FirstOrDefault();

        return (countryCal, overrideCal);
    }

    private static WorkingDateResult Unresolved(WorkingDayResult probe, DateOnly input, WorkingCalendarScope scope)
        => new(probe.Resolution, null, input, Normalize(scope.CountryCode), probe.SelectionReason, probe.ReasonCodes);
}
