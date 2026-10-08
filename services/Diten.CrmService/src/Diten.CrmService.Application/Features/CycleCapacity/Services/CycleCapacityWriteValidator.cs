using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Services;

/// <summary>
/// MOD-0155 FU06 — the write path's gate, in ONE place so create and update can never drift apart. It runs a fixed
/// order and stops at the first refusal:
/// <list type="number">
/// <item><description>the PIN — the cycle period must exist in the caller's tenant (404 otherwise);</description></item>
/// <item><description>the closed-period lock — a capacity whose period is closed is frozen (409);</description></item>
/// <item><description>the shape — minute budget, the non-zero divisor, the description;</description></item>
/// <item><description>the calendar country — derived from a country-scoped period, or validated against the governed
/// reference set when authored;</description></item>
/// <item><description>the month rows — addressed, unique, and intersecting the period's window.</description></item>
/// </list>
/// <para><b>WP-CAP-MODEL.</b> The gate also resolves the per-visit product ceilings, the typical visit (K-1) and each
/// month's FTE (K-5) against the stored row on an update, so create and update cannot drift on them either. The
/// typical model is decided BEFORE the shape check, because it decides which per-day charges the day budget holds.</para>
/// <para><b>Everything here happens BEFORE any insert or replace</b>, so a dependency outage can never leave a
/// half-authored capacity behind.</para>
/// <para><b>It reads the period; it never writes one.</b> <c>ICyclePeriodReader</c> is the read-only seam MOD-0165
/// published, and nothing in this class can reach a CyclePeriod repository.</para>
/// </summary>
public sealed class CycleCapacityWriteValidator
{
    private readonly ICyclePeriodReader _periods;
    private readonly ICycleCapacityCountryResolver _countries;
    private readonly IReferenceDataValidator _references;
    private readonly ICycleCapacityDefaultsProvider _defaults;

    public CycleCapacityWriteValidator(
        ICyclePeriodReader periods,
        ICycleCapacityCountryResolver countries,
        IReferenceDataValidator references,
        ICycleCapacityDefaultsProvider defaults)
    {
        _periods = periods;
        _countries = countries;
        _references = references;
        _defaults = defaults;
    }

    /// <summary>The accepted write, or the failure the handler answers with.</summary>
    public sealed record Result(
        CyclePeriodSnapshot? Period,
        string? CalendarCountryCode,
        bool CalendarCountryIsDerived,
        IReadOnlyList<CycleCapacityMonth>? Months,
        int BetweenVisitTimeMinutes,
        CycleCapacityValidation.Failure? Failure)
    {
        /// <summary>WP-SB-3a — the resolved per-visit ceilings (request, else stored, else 3).</summary>
        public int MaxPromoProducts { get; init; }

        public int MaxNonPromoProducts { get; init; }

        /// <summary>WP-CAP-MODEL — the typical visit to store (all null = legacy model).</summary>
        public CycleCapacityValidation.TypicalVisit TypicalVisit { get; init; } = CycleCapacityValidation.TypicalVisit.None;

        /// <summary>WP-CAP-MODEL — the per-DAY report charge to store: the request's on a legacy row, 0 on a typical
        /// row (its report is charged per visit).</summary>
        public int ReportDuration { get; init; }
    }

    public async Task<Result> ValidateAsync(
        Guid cyclePeriodId,
        string? calendarCountryCode,
        int dailyWorkMinutes,
        int promoProductTime,
        int nonPromoProductTime,
        int travelingTime,
        int reportDuration,
        int quizDuration,
        string? description,
        IReadOnlyList<CycleCapacityMonthInput> months,
        int? betweenVisitTimeMinutes,
        CancellationToken cancellationToken,
        int? maxPromoProducts = null,
        int? maxNonPromoProducts = null,
        CycleCapacityValidation.TypicalVisit? typicalVisit = null,
        CapacityEntity? existing = null)
    {
        // 1 — the pin. A period that does not exist in this tenant answers 404 rather than 403, so the endpoint never
        // confirms that a row exists somewhere else.
        var period = await _periods.GetByIdAsync(cyclePeriodId, cancellationToken);
        if (period is null)
        {
            return Fail(new CycleCapacityValidation.Failure(
                "The cycle period does not exist.", CycleCapacityReasonCodes.PeriodNotFound, 404));
        }

        // 2 — the closed-period lock. This aggregate has no lifecycle of its own; a closed period freezes its capacity,
        // because the estimate belongs to a plan that has already ended.
        if (string.Equals(period.CycleStatus, CyclePeriodStatuses.Closed, StringComparison.Ordinal))
        {
            return Fail(new CycleCapacityValidation.Failure(
                $"Cycle period '{period.CycleCode}' is closed, so its capacity can no longer be edited.",
                CycleCapacityReasonCodes.PeriodClosed,
                409));
        }

        // 3a — WP-CAP-MODEL: which visit model the written row will use. An update that sends no typical field keeps
        // the stored one (a caller that does not know the fields never silently reverts a row to legacy); a partial
        // triple is refused in 3d. On the typical model the per-DAY report charge is 0 — the report is charged per
        // visit — so the day budget below is judged with it.
        var requested = typicalVisit ?? CycleCapacityValidation.TypicalVisit.None;
        var typical = requested.IsEmpty && existing is not null
            ? new CycleCapacityValidation.TypicalVisit(
                existing.TypicalPromoCount, existing.TypicalNonPromoCount, existing.ReportMinutesPerVisit)
            : requested;
        var effectiveReportDuration = typical.IsComplete ? 0 : reportDuration;

        // 3 — the shape.
        if (CycleCapacityValidation.ValidateShape(
                dailyWorkMinutes, promoProductTime, nonPromoProductTime,
                travelingTime, effectiveReportDuration, quizDuration, description) is { } shapeFailure)
        {
            return Fail(shapeFailure);
        }

        // 3b — MOD-0155 FU06B: the between-visit buffer. A missing payload takes the configured default (fail-closed),
        // so an author who never touches the field still gets a sane value; an authored value is range-checked.
        var betweenVisit = betweenVisitTimeMinutes ?? _defaults.Current.BetweenVisitTimeMinutes;
        if (CycleCapacityValidation.ValidateBetweenVisitTime(betweenVisit) is { } bufferFailure)
        {
            return Fail(bufferFailure);
        }

        // 3c — WP-SB-3a: products per visit by role. Omitted = the stored value on an update, 3 on a new row.
        var maxPromo = maxPromoProducts
                       ?? existing?.EffectiveMaxPromoProducts()
                       ?? CycleCapacityLimits.DefaultMaxProductsPerVisit;
        var maxNonPromo = maxNonPromoProducts
                          ?? existing?.EffectiveMaxNonPromoProducts()
                          ?? CycleCapacityLimits.DefaultMaxProductsPerVisit;
        if (CycleCapacityValidation.ValidateMaxProducts(maxPromo, maxNonPromo) is { } maxFailure)
        {
            return Fail(maxFailure);
        }

        // 3d — WP-CAP-MODEL: the typical visit, judged against the ceilings it may not exceed.
        if (CycleCapacityValidation.ValidateTypicalVisit(
                typical, promoProductTime, nonPromoProductTime, maxPromo, maxNonPromo) is { } typicalFailure)
        {
            return Fail(typicalFailure);
        }

        // 4 — the calendar country (D-COUNTRY = B). Derived from a country-scoped period, otherwise authored.
        var country = _countries.Resolve(period, calendarCountryCode);
        if (country.Failure is not null || country.CountryCode is null)
        {
            return Fail(country.Failure ?? new CycleCapacityValidation.Failure(
                "A calendar country is required.", CycleCapacityReasonCodes.CountryRequired));
        }

        // A derived code came from a period that already passed this very check when it was written; re-validating it
        // would only add a dependency call that can fail for a value we did not accept from the caller.
        if (!country.IsDerived
            && await ValidateCountryAsync(country.CountryCode, cancellationToken) is { } referenceFailure)
        {
            return Fail(referenceFailure);
        }

        // 5 — the month rows, judged against the period's own window.
        if (CycleCapacityValidation.ValidateMonths(months, period.StartDate, period.EndDate) is { } monthFailure)
        {
            return Fail(monthFailure);
        }

        // FU07 / WP-CAP-MODEL (K-5) - the FTE is resolved HERE, per month, so create and update cannot drift on it:
        // an FTE the author sent is stored as `authored` (0 allowed); an omitted one keeps the stored AUTHORED value of
        // the same month on an update; otherwise the configured interim average is stamped, as before.
        var stored = existing?.Months.ToDictionary(m => (m.Year, m.MonthNumber));
        var stamped = months.Select(m => ToMonth(m, _defaults.Current.Fte, stored)).ToList();

        foreach (var month in stamped)
        {
            if (!CycleCapacityFteSources.IsAuthored(month.FteSource)
                && CycleCapacityValidation.ValidateStampedMonthFte(month) is { } fteFailure)
            {
                return Fail(fteFailure);
            }
        }

        return new Result(
            period,
            country.CountryCode,
            country.IsDerived,
            stamped,
            betweenVisit,
            Failure: null)
        {
            MaxPromoProducts = maxPromo,
            MaxNonPromoProducts = maxNonPromo,
            TypicalVisit = typical.IsComplete ? typical : CycleCapacityValidation.TypicalVisit.None,
            ReportDuration = effectiveReportDuration
        };
    }

    private static CycleCapacityMonth ToMonth(
        CycleCapacityMonthInput input,
        decimal configuredFte,
        IReadOnlyDictionary<(int Year, int MonthNumber), CycleCapacityMonth>? stored)
    {
        var (fte, source) = input.Fte is { } authored
            ? (authored, CycleCapacityFteSources.Authored)
            : stored is not null
              && stored.TryGetValue((input.Year, input.MonthNumber), out var previous)
              && CycleCapacityFteSources.IsAuthored(previous.FteSource)
                ? (previous.Fte, CycleCapacityFteSources.Authored)
                : (configuredFte, CycleCapacityFteSources.InterimDefault);

        return new CycleCapacityMonth
        {
            Year = input.Year,
            MonthNumber = input.MonthNumber,
            MeetingDays = input.MeetingDays,
            TrainingDays = input.TrainingDays,
            VacationDays = input.VacationDays,
            MicroTargetingDayCount = input.MicroTargetingDayCount,
            MicroTargetingDuration = input.MicroTargetingDuration,
            Fte = fte,
            FteSource = source
        };
    }

    /// <summary>
    /// The authored country must be a published value of the governed set. <b>The set is deliberately the same one
    /// <c>CyclePeriod</c> validates its country scope against</b> (<c>CyclePeriodReferenceSets.CountrySet</c>) rather
    /// than a second constant: a code derived from a country-scoped period and a code typed by an author must live in
    /// ONE vocabulary, or the same capacity would mean different things depending on how its country arrived.
    /// <para>No hardcoded fallback list exists. An unpublished SET and an unknown VALUE are reported as different
    /// failures, because one is fixed by an operator and the other by retyping.</para>
    /// </summary>
    private async Task<CycleCapacityValidation.Failure?> ValidateCountryAsync(
        string countryCode, CancellationToken cancellationToken)
    {
        var result = await _references.ValidateAsync(
            CyclePeriodReferenceSets.CountrySet, countryCode, cancellationToken);

        return result.Status switch
        {
            ReferenceValidationStatus.Valid => null,
            ReferenceValidationStatus.SetMissing => new CycleCapacityValidation.Failure(
                $"The governed reference set '{CyclePeriodReferenceSets.CountrySet}' is not published yet, so a "
                + "calendar country cannot be validated. An operator must publish it first.",
                CycleCapacityReasonCodes.ReferenceSetUnpublished),
            _ => new CycleCapacityValidation.Failure(
                $"'{countryCode}' is not a published value of '{CyclePeriodReferenceSets.CountrySet}'.",
                CycleCapacityReasonCodes.CountryUnknown)
        };
    }

    private static Result Fail(CycleCapacityValidation.Failure failure)
        => new(null, null, false, null, 0, failure);
}
