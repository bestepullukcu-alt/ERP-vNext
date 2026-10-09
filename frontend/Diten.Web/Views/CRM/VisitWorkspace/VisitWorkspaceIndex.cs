namespace Diten.Web.Views.CRM.VisitWorkspace;

/// <summary>
/// Localization marker for the WP-VW-W2 Visit Workspace (tenant module: 7 languages). <see cref="ScriptKeys"/> are the
/// strings the page script reads (window.VisitWorkspaceL10n) — ONE list, so the view serializes exactly these and the
/// tests check exactly these in every language.
/// </summary>
public sealed class VisitWorkspaceIndex
{
    public static readonly string[] ScriptKeys =
    [
        "WeekLabel", "WeekStateDraft", "WeekStateApproved", "WeekStatePast", "WeekStateNone", "CapacityLabel", "HoursShort",
        "Unplaced", "UnplacedDetail", "HolidayLabel", "DayLoad", "NoVisitDay", "FilterAllAccounts", "FilterAllProducts",
        "Status_cancelled", "Status_not_done", "Status_rescheduled", "Status_reported", "Status_expired",
        "Status_report_missing", "Status_missed", "Status_today", "Status_planned", "Status_draft",
        "Icon_Pinned", "Icon_Unplanned", "Icon_Rescheduled", "CountdownLeft", "CountdownPassed",
        "BandMissed", "BandExpired", "BandReportMissing", "BandDraft", "BandCancelled",
        "RolePromo", "RoleReminder", "NoContent", "Frequency",
        "Outcome_completed", "Outcome_missed", "Outcome_rescheduled",
        "Action_cancel", "Action_result", "Action_notDone", "Action_reschedule", "Action_sendReport", "Action_plan", "LockedInfo",
        "DlgCancelTitle", "DlgNotDoneTitle", "DlgRescheduleTitle", "RescheduleDayLoad", "RescheduleNoDays",
        "ReasonsUnavailable", "Loading", "Saved", "ActionFailed", "CalendarLoadFailed", "ApproveDone", "ReopenDone", "ReopenTitle",
        "NoMatches",
        "Err_visit_report_deadline_passed", "Err_visit_report_plan_cancelled", "Err_visit_reason_invalid",
        "Err_visit_reason_note_required", "Err_visit_reason_note_too_long", "Err_visit_reschedule_date_invalid",
        "Err_visit_report_reschedule_date_invalid", "Err_visit_reschedule_already_applied", "Err_visit_cancel_past_day",
        "Err_unplanned_visit_today_only", "Err_reference_data_unavailable", "Err_visit_report_edit_window_closed",
        "Err_visit_report_invalid_transition", "Err_resource_not_caller", "Err_visit_reason_applies_to_invalid",
        "Err_visit_not_yet_due", "Err_planned_visit_invalid_transition", "Err_planned_visit_overlap",
        // WP-VW-W2 (WEB-b) — Plan mode
        "TargetsTitle", "TargetsNoPlan", "PlanReadOnlyWeek", "PlanReadOnlyPast", "QuickDue", "QuickNever", "QuickAll", "FrequencyPerPeriod", "FrequencyDefaultWeekly", "DueThisWeek", "PlanDoctorsHeading", "OtherDoctorsHeading", "NoDoctors", "SelectAll", "ApplyProducts", "SelectionSummary", "TargetsSaved", "PinSaved", "ProductsApplied", "NoProductPicked", "RemoveProduct", "UnplacedReasonOther", "Pin_pin_time_invalid", "Pin_pin_time_outside_hours", "Pin_pin_time_conflict", "Pin_pin_time_past_day_end", "Pin_pin_overflow", "Pin_pin_day_full", "Pin_week_already_approved", "Reason_capacity_full", "Reason_no_near_day", "Reason_period_exhausted", "Reason_missing_location", "Reason_consent_blocked", "Reason_week_full_skipped", "Reason_extra_no_room", "Reason_no_feasible_availability_window"
    ];

    /// <summary>The strings the view itself writes (headings, buttons, labels).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageSubtitle", "TodayButton", "PreviousWeek", "NextWeek", "ApproveWeek", "ReopenWeek", "UnplannedVisit",
        "FilterStatus", "FilterAccount", "FilterProduct", "NoPlanWeek", "DetailTitle", "WhatToPresent", "PreviousVisit",
        "FrequencyTitle", "CloseButton", "CancelButton", "SaveButton", "ReasonLabel", "NoteLabel", "NoteRequired", "NewDate", "ReopenReason",
        "ReopenHint", "UnplacedTitle", "UnplacedHint", "UnplannedTitle", "UnplannedHint", "DoctorLabel", "DoctorSearch", "TimeLabel", "AddButton",
        // WP-VW-W2 (WEB-b) — Plan mode
        "ModeLabel", "ModeExecute", "ModePlan", "DragHint", "AccountLabel", "QuickFiltersLabel", "ProductsTitle", "ProductsHint", "PickedProducts", "ProductSearch", "ApplyButton"
    ];
}
