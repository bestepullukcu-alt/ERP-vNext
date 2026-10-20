namespace Diten.Web.Views.CRM.VisitNotifications;

/// <summary>
/// Localization marker for the WP-VW-W5 (W5-WEB) sent visit e-mails page (tenant module: 7 languages). The script keys
/// travel in the standard L10n payload (_IndexL10n.cshtml → index.l10n.js → window.L10n). <see cref="ScriptKeys"/> are the
/// strings the page scripts read — ONE list, so the view serializes exactly these and the tests check exactly these in
/// every language; <see cref="ViewKeys"/> the strings the views write themselves.
/// </summary>
public sealed class VisitNotificationsIndex
{
    public static readonly string[] ScriptKeys =
    [
        "PeriodLast30", "PeriodLast7", "PeriodCustom", "EventAll", "StatusAll", "RepAll",
        "Event_submitted", "Event_adverseevent", "Event_offlabelrequest", "Event_overdue",
        "St_sent", "St_queued", "St_failed", "St_failed_final", "St_no_recipient", "St_route_pending", "St_route_pending_pv", "St_route_pending_msl", "St_unknown",
        "Rcp_manager", "Rcp_upper_manager", "Rcp_pv", "Rcp_msl", "Rcp_user", "ManagerOf", "RcpNone", "RcpPending", "CcSuffix",
        "VisitDayLine", "Attempts",
        "MailFrom", "MailFromValue", "MailTo", "MailCc", "MailDate", "PreviewNote", "FooterKvkk",
        "Kind_submitted", "Kind_adverseevent", "Kind_offlabelrequest", "Kind_overdue",
        "Subj_submitted", "Subj_adverseevent", "Subj_offlabelrequest", "Subj_overdue",
        "Band_submitted", "Band_adverseevent", "Band_offlabelrequest", "Band_overdue",
        "Title_submitted", "Title_adverseevent", "Title_offlabelrequest", "Title_overdue",
        "Intro_submitted", "Intro_adverseevent", "Intro_offlabelrequest", "Intro_overdue",
        "Row_rep", "Row_doctor", "Row_institution", "Row_visit", "Row_products", "Row_reporter", "Row_sourceDoctor", "Row_suspectProduct",
        "Row_product", "Row_deadline",
        "Sec_note", "Sec_safety", "Sec_followUp", "Sec_repNote",
        "Safety_adverse_yes", "Safety_adverse_no", "Safety_offlabel_yes", "Safety_offlabel_no", "Note_adverseevent",
        "CtaOpenReport", "CtaReviewRecord",
        "Explain_queued", "Explain_failed", "Explain_failed_final", "Explain_no_recipient", "Explain_route_pending_pv", "Explain_route_pending_msl",
        "EmptyTitle", "EmptyText", "QuickView",
        "SettingsSaved", "SettingsDefault", "SettingsUpdated", "ErrEmailField", "ErrRangeMissing", "ErrRangeOrder",
        "Err_conflict", "Err_visit_notification_email_invalid", "Err_visit_notification_invalid", "Err_visit_notification_not_found",
        "Err_permission_denied", "Err_unavailable", "Err_generic"
    ];

    /// <summary>The strings the views themselves write (headings, labels, buttons).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageDescription", "TabList", "TabSettings", "PeriodLabel", "CustomFrom", "CustomTo", "EventLabel", "StatusLabel",
        "RepLabel", "ColDate", "ColEvent", "ColTarget", "ColRep", "ColRecipients", "ColStatus", "ColActions", "DrawerTitle", "CloseButton",
        "SettingsTitle", "SettingsDescription", "PvMailboxLabel", "PvMailboxHelp", "MslMailboxLabel", "MslMailboxHelp", "MailboxEmptyNote",
        "ReportSummaryLabel", "ReportSummaryHelp", "OverdueRemindersLabel", "OverdueRemindersHelp", "SafetyAlwaysNote", "SaveSettings"
    ];
}
