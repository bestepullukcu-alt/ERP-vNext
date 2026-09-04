using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU06 — the FORM view models, deliberately in a file of their OWN.
//
// The DataTable verifier resolves a form field's type and its required metadata from the LAST same-named property it
// finds in the model file. Several read-side shapes in CycleCapacityViewModels.cs carry CyclePeriodId, Fte and
// CalendarCountryCode as plain, non-required members — and they shadowed the form's nullable, [Required] ones, so the
// verifier reported a missing [Required] and a non-nullable optional field that do not exist.
//
// Splitting the form models out is the documented fix for that trap (MOD-0165 FU08 S2, MOD-0167 FU02) and is a
// genuine separation besides: what an author fills in is a different contract from what an API hands back.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// MOD-0155 FU06 — the create/edit form. Ten user fields, so this module follows the Golden <b>Compact</b> reference:
/// separate Create / Edit / Details pages rather than an offcanvas.
/// <para><c>Fte</c> is rendered but <b>disabled</b> and is never posted as authority: the interim configured average is
/// stamped server-side, and the request model carries no FTE at all, so re-enabling the input in a browser changes
/// nothing.</para>
/// <para>Optional numeric/date fields are nullable so the generated client validation does not demand a value the
/// runtime treats as optional; required numerics are nullable-with-[Required] for the same reason the sibling module
/// uses that shape — a non-nullable <c>int</c> would post 0 and look "filled in".</para>
/// </summary>
public sealed class CycleCapacityEditViewModel
{
    public Guid? CycleCapacityId { get; set; }

    /// <summary>The pinned period. Chosen once on create and <b>read-only afterwards</b>: the API offers no way to
    /// move a capacity to another period.</summary>
    [Required]
    public Guid? CyclePeriodId { get; set; }

    /// <summary>
    /// The country whose working calendar answers "how many working days?".
    /// <para><b>A calendar query parameter, not a scope.</b> It never changes where the cycle period lives. When the
    /// period is country-scoped it is derived server-side and this control renders read-only.</para>
    /// </summary>
    [Required]
    public string? CalendarCountryCode { get; set; }

    /// <summary>True when the country came from the period's own country scope. Display-only: the server derives it
    /// again on every write, so a tampered value changes nothing.</summary>
    public bool CalendarCountryIsDerived { get; set; }

    [Required]
    public int? DailyWorkMinutes { get; set; }

    [Required]
    public int? PromoProductTime { get; set; }

    [Required]
    public int? NonPromoProductTime { get; set; }

    [Required]
    public int? TravelingTime { get; set; }

    [Required]
    public int? ReportDuration { get; set; }

    [Required]
    public int? QuizDuration { get; set; }

    /// <summary>
    /// MOD-0155 FU06B — the buffer left between two consecutive visits when a field day is packed.
    /// <para>Editable operator config (0–240). It is NOT part of a single visit's duration — the packing engine
    /// (MOD-0155 FU05) applies it BETWEEN visits. Nullable-with-[Required] like the sibling minute fields so an empty
    /// input is caught rather than silently posting 0.</para>
    /// </summary>
    [Required]
    public int? BetweenVisitTimeMinutes { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Where the author came from, so Save and Cancel can both return them there.
    /// <para>It is a NAVIGATION HINT, never a URL: the controller compares it against one known constant and uses it
    /// only to choose between two fixed local actions. A value that is not that constant simply falls back to the
    /// capacity list, so a tampered field can redirect nobody anywhere.</para>
    /// <para>It rides a hidden field rather than the query string alone so a REJECTED save — which redisplays the form
    /// — does not quietly lose the origin and strand the author on the wrong list.</para>
    /// </summary>
    public string? ReturnTo { get; set; }

    public int? ExpectedVersion { get; set; }

    /// <summary>One row per calendar month the period touches, each addressed by (Year, MonthNumber). The set is
    /// derived from the period's window: an author edits the deductions, never which months exist.</summary>
    public List<CycleCapacityMonthViewModel> Months { get; set; } = [];

    // ── server-rendered context (never posted back as authority) ───────────────────────────────────────────────────

    /// <summary>The pinned period, for the header of the form and the Details page.</summary>
    public CycleCapacityPeriodViewModel? CyclePeriod { get; set; }

    /// <summary>Periods the author may pin to, for the create page when it was not reached through a row action.</summary>
    public List<CycleCapacityPeriodOptionViewModel> PeriodOptions { get; set; } = [];

    /// <summary>Governed country values. Empty and NOT-READY rather than substituted: a hardcoded list would let an
    /// author pick a value the platform does not know, and the save would then be refused for a reason the form never
    /// showed them.</summary>
    public List<CycleCapacityCountryOptionViewModel> CountryOptions { get; set; } = [];

    public bool CountryReady { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>True while the pinned period is not closed. This aggregate has no status of its own — editability is
    /// DERIVED, which is why there is no status field anywhere on this model.</summary>
    public bool IsEditable { get; set; } = true;
}

/// <summary>One month row of the capacity form.</summary>
public sealed class CycleCapacityMonthViewModel
{
    [Required]
    public int? Year { get; set; }

    /// <summary>1–12.</summary>
    [Required]
    public int? MonthNumber { get; set; }

    [Required]
    public int? MeetingDays { get; set; }

    [Required]
    public int? TrainingDays { get; set; }

    [Required]
    public int? VacationDays { get; set; }

    [Required]
    public int? MicroTargetingDayCount { get; set; }

    [Required]
    public int? MicroTargetingDuration { get; set; }

    /// <summary>
    /// FU07 — the interim configured average for THIS month. <b>Never posted as authority</b>: rendered disabled, and
    /// the server stamps its own value on every write. It is shown because the row's estimate is built on it.
    /// </summary>
    public decimal? Fte { get; set; }

    public string? FteSource { get; set; }

    /// <summary>Display-only label ("March 2026"), rendered server-side so the grid does not have to build one in a
    /// locale the page did not choose.</summary>
    public string MonthLabel { get; set; } = string.Empty;
}
