namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU06 request bodies. <c>TenantId</c> appears in none of them — it is resolved server-side from the claim —
/// and neither does <c>Fte</c>: the interim average is stamped by the server, so a caller who re-enables the disabled
/// field in a browser has nothing to send. There is no status field either, because this aggregate has no lifecycle of
/// its own.
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
    public string? Description { get; set; }
    public List<CycleCapacityMonthRequest> Months { get; set; } = new();
    public int? ExpectedVersion { get; set; }
}

/// <summary>
/// The LIVE estimate request — the same numbers the create/edit form is holding, sent while the author is still
/// typing.
/// <para>It deliberately carries <b>no <c>Fte</c> and no <c>Description</c></b>: the FTE is stamped server-side from
/// configuration (so the preview matches what a save would store), and a description changes no figure. There is no
/// <c>CycleCapacityId</c> either — a preview is not about a record, saved or otherwise.</para>
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
}
