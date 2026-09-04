using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity;

/// <summary>
/// Entity → DTO projections. One place, so the grid, the detail and the calculation surface can never disagree about
/// what a capacity is.
/// <para><b>The period is projected, never copied.</b> Every shape that shows a cycle code or window takes a
/// <see cref="CyclePeriodSnapshot"/> read through <c>ICyclePeriodReader</c> at request time. A period the caller can no
/// longer see (deleted, or another tenant's) simply projects as <c>null</c> — the capacity row stays visible with its
/// own data rather than disappearing, because the inputs are still the tenant's.</para>
/// </summary>
public static class CycleCapacityMapper
{
    public static CycleCapacityListItemDto ToListItem(CapacityEntity c, CyclePeriodSnapshot? period) => new(
        c.Id,
        c.CyclePeriodId,
        period?.CycleCode,
        period?.CycleName,
        period?.Year,
        period?.SequenceInYear,
        period?.StartDate,
        period?.EndDate,
        period?.CycleStatus,
        period?.ScopeType,
        period?.ScopeRef,
        c.CalendarCountryCode,
        c.DailyWorkMinutes,
        c.MinutesPerVisit(),
        c.Months.Count,
        c.IsArchived,
        IsEditable(period),
        c.Version,
        c.CreatedAt,
        c.UpdatedAt);

    public static CycleCapacityDetailDto ToDetail(
        CapacityEntity c, CyclePeriodSnapshot? period, bool calendarCountryIsDerived) => new(
        c.Id,
        c.CyclePeriodId,
        ToPeriod(period),
        c.CalendarCountryCode,
        calendarCountryIsDerived,
        c.DailyWorkMinutes,
        c.PromoProductTime,
        c.NonPromoProductTime,
        c.TravelingTime,
        c.ReportDuration,
        c.QuizDuration,
        // Read-time normalised before it reaches here (repository EnsureBetweenVisitTime), so an old row shows the
        // configured default rather than 0.
        c.BetweenVisitTimeMinutes.GetValueOrDefault(),
        c.DailySpendMinutes(),
        c.MinutesPerVisit(),
        // The FTE is never editable in this FU. It is published as a flag so the form does not hardcode the rule, and
        // the server ignores the payload's value regardless — the flag is a hint, not the guard (F-FTE-HR).
        FteIsEditable: false,
        c.Description,
        c.OrderedMonths().Select(ToMonth).ToList(),
        c.IsArchived,
        IsEditable(period),
        IsEstimate: true,
        c.Version,
        c.CreatedAt,
        c.CreatedBy,
        c.UpdatedAt,
        c.UpdatedBy);

    public static CycleCapacityMonthDto ToMonth(CycleCapacityMonth m) => new(
        m.Year, m.MonthNumber, m.MeetingDays, m.TrainingDays, m.VacationDays,
        m.MicroTargetingDayCount, m.MicroTargetingDuration, m.DeductedDays(), m.MicroTargetingMinutes(),
        m.Fte, m.FteSource);

    public static CycleCapacityPeriodDto? ToPeriod(CyclePeriodSnapshot? p)
        => p is null
            ? null
            : new CycleCapacityPeriodDto(
                p.CyclePeriodId, p.CycleCode, p.CycleName, p.Year, p.SequenceInYear, p.StartDate, p.EndDate,
                p.CycleStatus, p.ScopeType, p.ScopeRef, p.CountryScope, p.LegalEntityId, p.BusinessUnitId,
                IsClosed(p));

    public static CycleCapacityCalculationDto ToCalculation(
        CapacityEntity c,
        Guid? calendarLegalEntityId,
        CycleCapacityCalculator.CapacityCalculation calculation) => new(
        c.Id,
        c.CyclePeriodId,
        c.CalendarCountryCode,
        calendarLegalEntityId,
        calculation.Resolution,
        calculation.IsEstimate,
        calculation.TotalVisitNumber,
        calculation.MinutesPerVisit,
        calculation.Months.Select(ToMonthCalculation).ToList(),
        calculation.ReasonCodes,
        calculation.Reason);

    private static CycleCapacityMonthCalculationDto ToMonthCalculation(
        CycleCapacityCalculator.MonthCalculation m) => new(
        m.Year, m.MonthNumber, m.RangeStart, m.RangeEnd, m.CalendarDays, m.WorkingDays, m.NonWorkingDays,
        m.MeetingDays, m.TrainingDays, m.VacationDays, m.DeductedDays, m.FieldDays,
        m.AvailableMinutes, m.MicroTargetingMinutes, m.SpendMinutes, m.VisitMinutes, m.Fte, m.TotalVisitNumber);

    /// <summary>
    /// A capacity is editable while its pinned period is not closed. This is the whole of D-LIFECYCLE: the aggregate
    /// has no status of its own, so editability is DERIVED rather than stored — one truth instead of two.
    /// <para>An unreadable period is treated as NOT editable. Refusing a write we cannot justify is the fail-closed
    /// direction; the alternative would let a capacity be edited precisely when its period could not be checked.</para>
    /// </summary>
    public static bool IsEditable(CyclePeriodSnapshot? period) => period is not null && !IsClosed(period);

    private static bool IsClosed(CyclePeriodSnapshot p)
        => string.Equals(p.CycleStatus, CyclePeriodStatuses.Closed, StringComparison.Ordinal);
}
