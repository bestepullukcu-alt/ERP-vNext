namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU06 request bodies. <c>TenantId</c> appears in none of them — it is resolved server-side from the claim.
/// WP-CAP-MODEL: the month FTE is authorable per row (<see cref="CycleCapacityMonthRequest.Fte"/>), and the typical
/// visit (<c>TypicalPromoCount</c> / <c>TypicalNonPromoCount</c> / <c>ReportMinutesPerVisit</c>) travels as a triple —
/// all three or none. There is no status field, because this aggregate has no lifecycle of its own.
/// </summary>
public sealed class CreateCycleCapacityRequest
{
    /// <summary>The period this capacity belongs to. Set once and never moved.</summary>
    public Guid CyclePeriodId { get; set; }

    /// <summary>
    /// ISO alpha-2 country whose working calendar answers "how many working days?".
    /// <para>It is a CALENDAR QUERY PARAMETER, not a scope: it never changes where the cycle period lives. When the
    /// period is country-scoped the server derives it and IGNORES whatever arrives here, so the two cannot
    /// disagree.</para>
    /// </summary>
    public string? CalendarCountryCode { get; set; }

    /// <summary>Minutes in a field working day. 480 (8 h × 60) unless the tenant works differently.</summary>
    public int DailyWorkMinutes { get; set; }

    /// <summary>Minutes of promoted-product conversation in ONE visit.</summary>
    public int PromoProductTime { get; set; }

    /// <summary>Minutes of non-promoted-product conversation in ONE visit. Together with
    /// <see cref="PromoProductTime"/> this must be greater than zero.</summary>
    public int NonPromoProductTime { get; set; }

    /// <summary>Minutes spent travelling on a field DAY.</summary>
    public int TravelingTime { get; set; }

    /// <summary>Minutes spent reporting on a field DAY.</summary>
    public int ReportDuration { get; set; }

    /// <summary>Minutes spent on quizzes on a field DAY.</summary>
    public int QuizDuration { get; set; }

    /// <summary>MOD-0155 FU06B — buffer minutes left between two consecutive visits. Nullable so an omitted field takes
    /// the server's configured default rather than posting a silent 0; not part of a single visit's duration.</summary>
    public int? BetweenVisitTimeMinutes { get; set; }

    /// <summary>WP-SB-3a — max promo / non-promo products per visit (1..10); omitted = 3.</summary>
    public int? MaxPromoProducts { get; set; }

    public int? MaxNonPromoProducts { get; set; }

    /// <summary>WP-CAP-MODEL — promo products in a typical visit (0..MaxPromoProducts). With the two below: all or none;
    /// none = legacy model (report per day).</summary>
    public int? TypicalPromoCount { get; set; }

    /// <summary>WP-CAP-MODEL — non-promo products in a typical visit (0..MaxNonPromoProducts).</summary>
    public int? TypicalNonPromoCount { get; set; }

    /// <summary>WP-CAP-MODEL — reporting minutes charged once per visit (0..480). On the typical model the per-day
    /// <see cref="ReportDuration"/> is stored as 0.</summary>
    public int? ReportMinutesPerVisit { get; set; }

    public string? Description { get; set; }

    /// <summary>One row per calendar month the period touches, each addressed by (Year, MonthNumber). There is no
    /// positional array: a period crossing new year's eve has to be expressible.</summary>
    public List<CycleCapacityMonthRequest> Months { get; set; } = new();
}

/// <summary>An edit. <c>CyclePeriodId</c> is absent on purpose: the pin is set once and the API offers no way to move
/// it, which is stronger than rejecting the attempt.</summary>
public sealed class UpdateCycleCapacityRequest
{
    public string? CalendarCountryCode { get; set; }
    public int DailyWorkMinutes { get; set; }
    public int PromoProductTime { get; set; }
    public int NonPromoProductTime { get; set; }
    public int TravelingTime { get; set; }
    public int ReportDuration { get; set; }
    public int QuizDuration { get; set; }

    /// <summary>MOD-0155 FU06B — buffer minutes between two consecutive visits. Nullable: an omitted field takes the
    /// configured default.</summary>
    public int? BetweenVisitTimeMinutes { get; set; }

    /// <summary>WP-SB-3a — max promo / non-promo products per visit (1..10); omitted = keep the stored value.</summary>
    public int? MaxPromoProducts { get; set; }

    public int? MaxNonPromoProducts { get; set; }

    /// <summary>WP-CAP-MODEL — the typical visit: all three or none (none = keep the stored model; mixed → 400
    /// <c>typical_visit_incomplete</c>).</summary>
    public int? TypicalPromoCount { get; set; }

    public int? TypicalNonPromoCount { get; set; }

    public int? ReportMinutesPerVisit { get; set; }

    public string? Description { get; set; }
    public List<CycleCapacityMonthRequest> Months { get; set; } = new();
    public int? ExpectedVersion { get; set; }
}

/// <summary>
/// The LIVE estimate request — the same numbers the create/edit form is holding, sent while the author is still
/// typing.
/// <para>It carries <b>no <c>Description</c></b> (a description changes no figure) and no <c>CycleCapacityId</c> — a
/// preview is not about a record, saved or otherwise. WP-CAP-MODEL: the month FTE and the typical visit travel exactly
/// as on a save, so the live figure uses the model the save will store.</para>
/// </summary>
public sealed class PreviewCycleCapacityRequest
{
    /// <summary>The period whose window decides which months exist. A preview cannot invent one.</summary>
    public Guid CyclePeriodId { get; set; }

    /// <summary>Ignored when the period is country-scoped: the server derives the code, exactly as it does on a save.</summary>
    public string? CalendarCountryCode { get; set; }

    public int DailyWorkMinutes { get; set; }
    public int PromoProductTime { get; set; }
    public int NonPromoProductTime { get; set; }
    public int TravelingTime { get; set; }
    public int ReportDuration { get; set; }
    public int QuizDuration { get; set; }

    /// <summary>WP-CAP-MODEL — the typical visit (all three → typical model).</summary>
    public int? TypicalPromoCount { get; set; }

    public int? TypicalNonPromoCount { get; set; }

    public int? ReportMinutesPerVisit { get; set; }

    public List<CycleCapacityMonthRequest> Months { get; set; } = new();
}

/// <summary>One month row on the wire.</summary>
public sealed class CycleCapacityMonthRequest
{
    public int Year { get; set; }

    /// <summary>1–12.</summary>
    public int MonthNumber { get; set; }

    public int MeetingDays { get; set; }
    public int TrainingDays { get; set; }
    public int VacationDays { get; set; }

    /// <summary>How many days of this month carry a micro-targeting charge.</summary>
    public int MicroTargetingDayCount { get; set; }

    /// <summary>Minutes that charge costs on one such day. Together these form a MONTHLY minute pool, not a per-day
    /// rate.</summary>
    public int MicroTargetingDuration { get; set; }

    /// <summary>WP-CAP-MODEL (K-5) — the month's FTE (0..9999, 0 = vacant position). Sent → stored as
    /// <c>authored</c>; omitted → the stored authored value is kept, else the configured interim average.</summary>
    public decimal? Fte { get; set; }
}
