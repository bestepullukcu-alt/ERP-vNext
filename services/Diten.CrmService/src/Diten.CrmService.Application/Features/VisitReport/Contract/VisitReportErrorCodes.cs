namespace Diten.CrmService.Application.Features.VisitReport.Contract;

/// <summary>Machine-readable refusal codes, so a UI and a smoke script can branch without parsing prose.</summary>
public static class VisitReportErrorCodes
{
    public const string UnsupportedVocabularyValue = "unsupported_vocabulary_value";

    public const string PlannedVisitRequired = "visit_report_planned_visit_required";
    public const string PlannedVisitNotFound = "visit_report_planned_visit_not_found";

    public const string OutcomeRequired = "visit_report_outcome_required";
    public const string ReasonCodeRequired = "visit_report_reason_code_required";
    public const string RescheduleDateInvalid = "visit_report_reschedule_date_invalid";

    public const string ResourceRequired = "visit_report_resource_required";
    public const string OutcomeCodeRequired = "visit_report_outcome_code_required";
    public const string SampleInvalid = "visit_report_sample_invalid";
    public const string ContentActualsInvalid = "visit_report_content_actuals_invalid";
    public const string FreeTextTooLong = "visit_report_free_text_too_long";

    public const string ReportNotFound = "visit_report_not_found";
    public const string ReportAlreadyExists = "visit_report_already_exists";
    public const string NotCompleted = "visit_report_not_completed";
    public const string EditWindowClosed = "visit_report_edit_window_closed";
    public const string NotFinalised = "visit_report_not_finalised";
    public const string AmendmentReasonRequired = "visit_report_amendment_reason_required";
    public const string InvalidTransition = "visit_report_invalid_transition";
    public const string ConcurrencyConflict = "visit_report_concurrency_conflict";

    /// <summary>WP-E2E-FIX-1 (E9-B5) — a completed/missed outcome or a report submit on a visit whose planned day is still
    /// in the future (409). Rescheduling stays free.</summary>
    public const string NotYetDue = "visit_not_yet_due";

    /// <summary>WP-VW-W1 — an outcome or a report submit on a visit whose plan is CANCELLED (409). An amendment of a
    /// report submitted before the cancellation stays allowed.</summary>
    public const string PlanCancelled = "visit_report_plan_cancelled";

    /// <summary>WP-VW-W1 — an outcome or a first submit after the report deadline (end of the planned day UTC +
    /// <c>ReportDeadlineHours</c>), 409. The read-all holder is exempt; an amendment is unlimited.</summary>
    public const string DeadlinePassed = "visit_report_deadline_passed";

    /// <summary>WP-VW-W1 — the calendar's from / to window is missing or not a date (400). Replaces the misleading
    /// <c>visit_report_reschedule_date_invalid</c> on that endpoint.</summary>
    public const string CalendarRangeInvalid = "visit_report_calendar_range_invalid";

    /// <summary>WP-VW-W1 — the calendar's optional <c>workStatus</c> filter carries an unknown code (400).</summary>
    public const string WorkStatusInvalid = "visit_report_work_status_invalid";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UnsupportedVocabularyValue,
        PlannedVisitRequired, PlannedVisitNotFound,
        OutcomeRequired, ReasonCodeRequired, RescheduleDateInvalid,
        ResourceRequired, OutcomeCodeRequired, SampleInvalid, ContentActualsInvalid, FreeTextTooLong,
        ReportNotFound, ReportAlreadyExists, NotCompleted, EditWindowClosed, NotFinalised,
        AmendmentReasonRequired, InvalidTransition, ConcurrencyConflict, NotYetDue,
        PlanCancelled, DeadlinePassed, CalendarRangeInvalid, WorkStatusInvalid
    };
}
