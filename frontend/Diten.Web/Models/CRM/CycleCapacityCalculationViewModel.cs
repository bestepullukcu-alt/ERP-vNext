namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0155 FU06 — the estimate, as the Details page renders it.
///
/// <para><b>Why this lives in its own file.</b> The DataTable verifier resolves a form field's type from the LAST
/// same-named property it finds in a file. This projection carries <c>Year</c> and <c>MonthNumber</c> — the same names
/// the form's month rows use — with non-nullable types, and it would shadow the form's nullable ones, making the
/// "optional numeric/date fields use nullable ViewModel types" check report a defect that does not exist. Splitting the
/// file is the documented fix for that trap (MOD-0165 FU08 S2, MOD-0167 FU02).</para>
///
/// <para><b>Nothing here is ever posted or persisted.</b> It is a read-time projection: the server recomputes it from
/// the published working calendar on every request, and no field of it exists on the stored aggregate.</para>
/// </summary>
public sealed class CycleCapacityCalculationViewModel
{
    public Guid CycleCapacityId { get; set; }
    public Guid CyclePeriodId { get; set; }
    public string CalendarCountryCode { get; set; } = string.Empty;
    public Guid? CalendarLegalEntityId { get; set; }

    /// <summary><c>resolved</c> | <c>calendar_unresolved</c> | <c>calendar_forbidden</c>.</summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>Always true, and part of the runtime contract rather than a decoration of this page.</summary>
    public bool IsEstimate { get; set; } = true;

    /// <summary>
    /// <c>null</c> — never <c>0</c> — when <see cref="Resolution"/> is not <c>resolved</c>. Zero means "no time is
    /// left for visits"; null means "the calendar did not answer and we do not know". The page must render the two
    /// differently.
    /// </summary>
    public int? TotalVisitNumber { get; set; }

    public int MinutesPerVisit { get; set; }

    /// <summary>EMPTY when unresolved: a partial table looks authoritative and is wrong.</summary>
    public List<CycleCapacityMonthCalculationViewModel> Months { get; set; } = [];

    public List<string> ReasonCodes { get; set; } = [];
    public string Reason { get; set; } = string.Empty;

    public bool IsResolved => string.Equals(Resolution, "resolved", StringComparison.OrdinalIgnoreCase);

    public bool IsForbidden => string.Equals(Resolution, "calendar_forbidden", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One month of the estimate, with every intermediate step. The steps are not decoration: a capacity figure nobody can
/// trace is a figure nobody trusts, so the page shows working days → deductions → field days → minutes → visits.
/// </summary>
public sealed class CycleCapacityMonthCalculationViewModel
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public DateTimeOffset RangeStart { get; set; }
    public DateTimeOffset RangeEnd { get; set; }

    /// <summary>Days of this month the period actually covers (the clipped range, inclusive).</summary>
    public int CalendarDays { get; set; }

    /// <summary>From the working calendar. Weekends, public holidays and company closures are ALREADY excluded — the
    /// page must not subtract them again.</summary>
    public int WorkingDays { get; set; }

    /// <summary>
    /// FU07 — <c>CalendarDays − WorkingDays</c>. Weekends, public holidays and closures TOGETHER, which is why the
    /// column is labelled "non-working days" and never "holidays": separating them needs dates the working calendar
    /// has no range operation for (F-WC-HOLIDAY-RANGE).
    /// </summary>
    public int NonWorkingDays { get; set; }

    public int MeetingDays { get; set; }
    public int TrainingDays { get; set; }
    public int VacationDays { get; set; }

    /// <summary>FU07 — the inputs travel with the result now that Details shows one merged table: a reader must be
    /// able to see WHY a month produced what it produced without lining up two tables by eye.</summary>
    public int MicroTargetingDayCount { get; set; }

    public int MicroTargetingDuration { get; set; }

    public int DeductedDays { get; set; }

    /// <summary>Clamped at zero. A month fully consumed by leave estimates zero visits, which is a real answer.</summary>
    public int FieldDays { get; set; }

    public int AvailableMinutes { get; set; }
    public int MicroTargetingMinutes { get; set; }
    public int SpendMinutes { get; set; }
    public int VisitMinutes { get; set; }

    /// <summary>FU07 — the month's own FTE, the multiplier this row's figure was built on.</summary>
    public decimal Fte { get; set; }

    public int TotalVisitNumber { get; set; }

    /// <summary>True when the month's deductions consumed every working day. The row is flagged rather than hidden.</summary>
    public bool IsFullyDeducted => FieldDays == 0;
}
