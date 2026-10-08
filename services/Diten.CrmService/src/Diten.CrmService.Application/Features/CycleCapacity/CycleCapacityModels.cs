namespace Diten.CrmService.Application.Features.CycleCapacity;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU06 — every DTO / read model of the CycleCapacity feature, in ONE file (the single documented exception to
// the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the claim.
//
// Nothing here carries a MicroTarget row, a visit allocation, a route or a frequency. And nothing STORED here carries
// a TotalVisitNumber: that figure appears only in the calculation shapes at the bottom of this file, which are
// projections computed on read and never written back.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>One month row as it arrives on a write. Deliberately NOT the entity type: a command must not be able to
/// hand the domain a half-built object.</summary>
public sealed record CycleCapacityMonthInput(
    int Year,
    int MonthNumber,
    int MeetingDays,
    int TrainingDays,
    int VacationDays,
    int MicroTargetingDayCount,
    int MicroTargetingDuration,
    // WP-CAP-MODEL (K-5) — the author's FTE for this month (0 allowed: a vacant position). Present → stored as
    // `authored`; omitted → the stored authored value is kept, otherwise the configured interim default is stamped.
    decimal? Fte = null);

/// <summary>One month row as it is read back. Same shape, plus nothing — the deductions are the whole truth of a
/// month; its working days and visit number are computed elsewhere.</summary>
public sealed record CycleCapacityMonthDto(
    int Year,
    int MonthNumber,
    int MeetingDays,
    int TrainingDays,
    int VacationDays,
    int MicroTargetingDayCount,
    int MicroTargetingDuration,
    int DeductedDays,
    int MicroTargetingMinutes,

    /// <summary>FU07 — the month's own FTE. WP-CAP-MODEL (K-5): authorable; <see cref="FteSource"/> says whether the
    /// author supplied it (<c>authored</c>) or the configured interim average was stamped.</summary>
    decimal Fte,

    string FteSource);

/// <summary>
/// One row of the capacity grid. It carries the PINNED PERIOD's identifying fields as a read-time projection
/// (<see cref="CycleCode"/> / <see cref="CycleName"/> / the window / its status) so the grid is readable — these are
/// re-read from the period on every request and are never stored on the capacity.
/// </summary>
public sealed record CycleCapacityListItemDto(
    Guid CycleCapacityId,
    Guid CyclePeriodId,
    string? CycleCode,
    string? CycleName,
    int? CycleYear,
    int? CycleSequenceInYear,
    DateTimeOffset? CycleStartDate,
    DateTimeOffset? CycleEndDate,
    string? CycleStatus,
    string? CycleScopeType,
    string? CycleScopeRef,
    string CalendarCountryCode,
    int DailyWorkMinutes,
    int MinutesPerVisit,
    int MonthCount,
    bool IsArchived,
    bool IsEditable,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // WP-SB-3a — products per visit by role (3 / 3 on a pre-SB-3a row).
    int MaxPromoProducts = CycleCapacityDefaultsView.MaxProducts,
    int MaxNonPromoProducts = CycleCapacityDefaultsView.MaxProducts,
    // WP-CAP-MODEL — which visit-duration arithmetic the row uses (typical / legacy) and its per-visit divisor.
    string VisitModel = Diten.CrmService.Domain.Entities.CycleCapacityVisitModels.Legacy,
    int TypicalVisitMinutes = 0);

public sealed record CycleCapacityListDto(IReadOnlyList<CycleCapacityListItemDto> Items, int TotalCount);

/// <summary>Capacity detail: every authored input, plus the pinned period as a read-time projection.</summary>
public sealed record CycleCapacityDetailDto(
    Guid CycleCapacityId,
    Guid CyclePeriodId,
    CycleCapacityPeriodDto? CyclePeriod,
    string CalendarCountryCode,
    bool CalendarCountryIsDerived,
    int DailyWorkMinutes,
    int PromoProductTime,
    int NonPromoProductTime,
    int TravelingTime,
    int ReportDuration,
    int QuizDuration,

    /// <summary>MOD-0155 FU06B — the between-visit buffer. A config input, not part of a visit's duration.</summary>
    int BetweenVisitTimeMinutes,

    int DailySpendMinutes,
    int MinutesPerVisit,

    /// <summary>FU07 — the FTE is per MONTH now, so there is no capacity-wide value to publish. This flag stays
    /// because the FORM still needs to know the control is locked (F-FTE-HR).</summary>
    bool FteIsEditable,

    string? Description,
    IReadOnlyList<CycleCapacityMonthDto> Months,
    bool IsArchived,
    bool IsEditable,
    bool IsEstimate,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    // WP-SB-3a — products per visit by role (3 / 3 on a pre-SB-3a row).
    int MaxPromoProducts = CycleCapacityDefaultsView.MaxProducts,
    int MaxNonPromoProducts = CycleCapacityDefaultsView.MaxProducts,
    // WP-CAP-MODEL (K-1) — the typical visit (null on a legacy row), the model name, and the two derived figures the
    // single visit-duration model computes. On a typical row ReportDuration is 0 and the report is per visit.
    int? TypicalPromoCount = null,
    int? TypicalNonPromoCount = null,
    int? ReportMinutesPerVisit = null,
    string VisitModel = Diten.CrmService.Domain.Entities.CycleCapacityVisitModels.Legacy,
    int TypicalVisitMinutes = 0,
    int DailyFixedMinutes = 0);

/// <summary>WP-SB-3a — the default products-per-visit ceiling, usable as a DTO default value.</summary>
public static class CycleCapacityDefaultsView
{
    public const int MaxProducts = Diten.CrmService.Domain.Entities.CycleCapacityLimits.DefaultMaxProductsPerVisit;
}

/// <summary>
/// The pinned period, projected for display. A consumer may SHOW this and must never STORE it: copying a period's code
/// or window into another row would go stale the moment the period is renamed.
/// </summary>
public sealed record CycleCapacityPeriodDto(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string CycleStatus,
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    bool IsClosed);

// ── Calculation shapes — READ-TIME PROJECTIONS, never persisted ──────────────────────────────────────────────────

/// <summary>
/// One month of the estimate, with every intermediate step exposed. The steps are not decoration: a capacity figure
/// nobody can trace is a figure nobody trusts, so the UI shows working days → deductions → field days → minutes →
/// visits rather than a bare total.
/// </summary>
public sealed record CycleCapacityMonthCalculationDto(
    int Year,
    int MonthNumber,
    DateTimeOffset RangeStart,
    DateTimeOffset RangeEnd,

    /// <summary>Days of this month the period actually covers (the clipped range, inclusive).</summary>
    int CalendarDays,

    int WorkingDays,

    /// <summary>FU07 — <c>CalendarDays − WorkingDays</c>: weekends, public holidays and closures TOGETHER. Named
    /// "non-working" rather than "holidays" because telling those apart needs dates the calendar has no range
    /// operation for (F-WC-HOLIDAY-RANGE).</summary>
    int NonWorkingDays,

    int MeetingDays,
    int TrainingDays,
    int VacationDays,
    int DeductedDays,
    int FieldDays,
    int AvailableMinutes,
    int MicroTargetingMinutes,
    int SpendMinutes,
    int VisitMinutes,
    decimal Fte,
    int TotalVisitNumber,

    /// <summary>WP-CAP-MODEL — the month's fixed per-day charges (<c>DailyFixedMinutes × FieldDays</c>).</summary>
    int DailyFixedMinutes,

    /// <summary>WP-CAP-MODEL — minutes left for visits after the fixed charges and micro-targeting.</summary>
    int RemainingMinutes,

    /// <summary>WP-CAP-MODEL — the per-visit divisor.</summary>
    int TypicalVisitMinutes);

/// <summary>WP-CAP-MODEL — the waterfall's cycle totals; each is the sum of the month rows (visits rounded per month).
/// </summary>
public sealed record CycleCapacityCalculationTotalsDto(
    int WorkingDays,
    int DeductedDays,
    int FieldDays,
    int AvailableMinutes,
    int DailyFixedMinutes,
    int MicroTargetingMinutes,
    int RemainingMinutes,
    int Visits,
    decimal AverageFte);

/// <summary>
/// The estimate. <see cref="TotalVisitNumber"/> is <c>null</c> — never <c>0</c> — whenever
/// <see cref="Resolution"/> is not <c>resolved</c>: zero means "no time is left for visits", null means "the calendar
/// did not answer and we do not know". <see cref="Months"/> is EMPTY on an unresolved answer; a partial table looks
/// authoritative and is wrong.
/// <para><see cref="IsEstimate"/> is always true and is part of the contract, not only of the screen.</para>
/// </summary>
public sealed record CycleCapacityCalculationDto(
    Guid CycleCapacityId,
    Guid CyclePeriodId,
    string CalendarCountryCode,
    Guid? CalendarLegalEntityId,
    string Resolution,
    bool IsEstimate,
    int? TotalVisitNumber,
    int MinutesPerVisit,
    IReadOnlyList<CycleCapacityMonthCalculationDto> Months,
    IReadOnlyList<string> ReasonCodes,
    string Reason,

    /// <summary>WP-CAP-MODEL — <c>typical</c> or <c>legacy</c>.</summary>
    string VisitModel,

    /// <summary>WP-CAP-MODEL — the per-visit divisor (equals <see cref="MinutesPerVisit"/>).</summary>
    int TypicalVisitMinutes,

    /// <summary>WP-CAP-MODEL — the fixed minutes charged per field DAY.</summary>
    int DailyFixedMinutes,

    /// <summary>WP-CAP-MODEL — null whenever <see cref="TotalVisitNumber"/> is (calendar unresolved).</summary>
    CycleCapacityCalculationTotalsDto? Totals);
