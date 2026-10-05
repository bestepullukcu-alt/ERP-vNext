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
/// <para>WP-CAP-MODEL / WP-CYC-UI-2 — the month <c>Fte</c> is authorable now (K-5), but posted only for the months the
/// author touched; the typical-visit triple is posted all-or-none.</para>
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

    /// <summary>WP-SB-3a default of both per-visit product ceilings.</summary>
    public const int DefaultMaxProductsPerVisit = 3;

    /// <summary>
    /// WP-SB-3-UIa — the most promo products a single visit tells (SB-3a, 1..10; a new record starts at 3).
    /// <para>Optional on purpose: an EMPTY field is not posted, and the runtime then keeps the stored value — so it is
    /// nullable and carries no [Required].</para>
    /// </summary>
    [Range(1, 10)]
    public int? MaxPromoProducts { get; set; }

    /// <summary>WP-SB-3-UIa — the most non-promo products a single visit tells (same rule as
    /// <see cref="MaxPromoProducts"/>).</summary>
    [Range(1, 10)]
    public int? MaxNonPromoProducts { get; set; }

    /// <summary>
    /// WP-CAP-MODEL (K-1) — promo products told in a TYPICAL visit (0..MaxPromoProducts).
    /// <para>The three typical fields travel as a TRIPLE: all three are posted or none is. None keeps the stored model
    /// (a legacy row stays legacy); a mixed set is never sent — the CRM would refuse it (<c>typical_visit_incomplete</c>)
    /// and the author would lose the save for a reason the form could have prevented.</para>
    /// </summary>
    [Range(0, 10)]
    public int? TypicalPromoCount { get; set; }

    /// <summary>WP-CAP-MODEL — non-promo products told in a typical visit (0..MaxNonPromoProducts).</summary>
    [Range(0, 10)]
    public int? TypicalNonPromoCount { get; set; }

    /// <summary>WP-CAP-MODEL — reporting minutes charged once per VISIT (the typical model's report; the legacy per-day
    /// <see cref="ReportDuration"/> is stored as 0 once the triple is saved).</summary>
    [Range(0, 480)]
    public int? ReportMinutesPerVisit { get; set; }

    /// <summary>WP-CAP-MODEL — <c>typical</c> / <c>legacy</c>, as the CRM read it. Display-only: decides whether the
    /// legacy band is shown; never posted as authority.</summary>
    public string? VisitModel { get; set; }

    /// <summary>WP-CAP-MODEL — the stored typical-visit divisor, as the CRM computed it. Display-only.</summary>
    public int? TypicalVisitMinutes { get; set; }

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

    /// <summary>WP-CYC-UI-2 (E9) — the input ceilings, read from the CRM contract so the form enforces the SAME numbers
    /// the runtime does. Display-only.</summary>
    public CycleCapacityFormLimits Limits { get; set; } = new();

    /// <summary>True when the row is still on the pre-WP-CAP-MODEL arithmetic (report charged per DAY).</summary>
    public bool IsLegacyModel => string.Equals(VisitModel, "legacy", StringComparison.OrdinalIgnoreCase);
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
    /// WP-CAP-MODEL (K-5) — the month's FTE as the CRM read it (display). The AUTHORED value travels in
    /// <see cref="FteText"/>.
    /// </summary>
    public decimal? Fte { get; set; }

    /// <summary>
    /// The month's FTE as the form posts it — an INVARIANT-culture string ("0.75"). A decimal would be bound with the
    /// request culture, and in Turkish "0.75" reads as 75. It is sent to the CRM ONLY for a month whose FTE the author
    /// changed (<see cref="FteTouched"/>): an untouched month sends nothing and the CRM keeps the stored value — so
    /// opening and saving a record never silently turns an interim default into an "authored" one.
    /// </summary>
    public string? FteText { get; set; }

    /// <summary><c>authored</c> / <c>interim-default</c>, as the CRM read it. Shown as the row's source badge.</summary>
    public string? FteSource { get; set; }

    /// <summary>Set by the page when the author edits this month's FTE (directly or through "apply to all months").</summary>
    public bool FteTouched { get; set; }

    /// <summary>Display-only label ("March 2026"), rendered server-side so the grid does not have to build one in a
    /// locale the page did not choose.</summary>
    public string MonthLabel { get; set; } = string.Empty;
}
