namespace Diten.Web.Views.CRM.VisitEvidenceSettings;

/// <summary>
/// Localization marker for the WP-VW-W3 (W3-WEB-d) visit evidence settings page (tenant module: 7 languages).
/// <see cref="ScriptKeys"/> are the strings the page script reads (window.VisitEvidenceSettingsL10n) — ONE list, so the
/// view serializes exactly these and the tests check exactly these in every language; <see cref="ViewKeys"/> the strings
/// the view writes itself.
/// </summary>
public sealed class VisitEvidenceSettingsIndex
{
    public static readonly string[] ScriptKeys =
    [
        // the form
        "F_locationMode", "F_radiusMeters", "F_maxAccuracyMeters", "F_accountWithoutLocation", "F_photoMode",
        "F_doctorConfirmation", "F_durationConsistency", "F_shortVisitSeconds",
        "D_locationMode_required", "D_locationMode_info", "D_accountWithoutLocation", "D_photoMode", "D_doctorConfirmation",
        "Opt_locationMode_required", "Opt_locationMode_info", "Opt_photoMode_required", "Opt_photoMode_optional",
        "Opt_accountWithoutLocation_allow_flagged", "Opt_accountWithoutLocation_block",
        "R_radiusMeters", "R_maxAccuracyMeters", "R_shortVisitSeconds", "G_location", "G_photo", "G_approval",
        "ValueOn", "ValueOff", "UnitMeters", "UnitSeconds", "UnitM", "UnitS", "RangeError",
        // the preview
        "PvRequired", "PvOptional", "PvInfo", "PvLocationRequired", "PvLocationInfo", "PvOpenBoth", "PvOpenLocation",
        "PvOpenPhoto", "PvOpenNone", "PvAccuracy", "PvNoLocationFlag", "PvNoLocationBlock", "PvShort",
        // the scope, the save, the lookup, the history
        "Reps", "AllFields", "DefaultPolicy", "YouLabel", "WorkflowActor", "SaveText", "Saved", "LookPlaceholder",
        "ActiveException", "SourceExceptionUntil", "SourceTenant", "SourceException", "HistCreated", "NoHistory",
        // the exceptions
        "XFilter_all", "XFilter_active", "XFilter_planned", "XFilter_in_review", "XFilter_draft", "XFilter_expired",
        "XFilter_cancelled", "XFilter_ended_early",
        "XRule_location_info", "XRule_photo_optional", "XRule_account_without_location_allow",
        "XSt_draft", "XSt_in_review", "XSt_active", "XSt_planned", "XSt_expired", "XSt_cancelled", "XSt_ended_early",
        "XDays", "XCreatedBy", "XApprovedBy", "XEmpty", "OpenRow", "XDetailRules", "XDetailRange", "XDetailReason",
        "XInReviewNote", "XSod", "XReturnComment", "XCloseReason",
        "TlCreated", "TlSubmitted", "TlApproved", "TlReturned", "TlWithdrawn", "TlTimedOut", "TlCancelled", "TlEndedEarly",
        "TlRefused_visit_evidence_exception_sod", "TlRefused_visit_evidence_exception_overlap",
        "CloseButton", "CancelButton", "XBtnSubmit", "XBtnWithdraw", "XBtnApprove", "XBtnReturn", "XBtnCancel", "XBtnEndEarly",
        "XBtnConfirmReturn", "XBtnConfirmCancel", "XBtnConfirmEndEarly",
        "XSubmitted", "XWithdrawn", "XApprovedToast", "XReturnedToast", "XCancelledToast", "XEndedToast", "XCreatedNotSent",
        "XRuleNoRep", "XRuleAlready", "XRuleNow", "XDayCounter", "XDayCounterEmpty",
        "XErrRep", "XErrRules", "XErrFrom", "XErrPast", "XErrToRequired", "XErrToBefore", "XErrTooLong", "XErrWhy", "XErrWhyLong",
        // the refusals
        "Err_visit_evidence_exception_invalid", "Err_visit_evidence_exception_overlap", "Err_visit_evidence_exception_rep_unknown",
        "Err_visit_evidence_exception_not_editable", "Err_visit_evidence_exception_sod", "Err_visit_evidence_exception_not_found",
        "Err_visit_evidence_policy_invalid", "Err_visit_execution_concurrency_conflict", "Err_workflow_template_missing",
        "Err_workflow_unavailable", "Err_approval_forbidden", "Err_no_open_review", "Err_withdraw_not_possible",
        "Err_rejection_comment_required", "Err_workflow_request_rejected", "Err_permission_denied",
        "Err_conflict", "Err_unavailable", "Err_generic",
        // WP-VW-W3E (W3E-WEB-b) — additional evidence (E1), qr_optional (E2), the reps pane and the new fields (E3)
        "G_extra", "QrCaption", "QrIntro", "DeviceCaption", "DeviceIntro", "ManageQrLink", "F_qrMode", "F_antiFraudMode", "Opt_qrMode_off", "Opt_qrMode_optional", "Opt_qrMode_required", "Opt_antiFraudMode_off", "Opt_antiFraudMode_flag", "Opt_antiFraudMode_block", "D_qrMode_off", "D_qrMode_optional", "D_qrMode_required", "NewBadge", "SoonBadge", "XRule_qr_optional", "XRuleDesc_qr_optional", "XRuleQrNotRequired", "SourceExceptionShort", "RepsEmpty"
    ];

    /// <summary>The strings the view itself writes (headings, buttons, labels).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "Crumb", "Intro", "NewException", "TabSettings", "TabExceptions", "Retry", "ReadOnly", "ScopesTitle",
        "ScopesNote", "TenantDefault", "Locked", "AddLegalEntity", "AddLegalEntityHint", "LookTitle", "LookSearch",
        "EffectiveTitle", "CloseButton", "ColField", "ColValue", "ColSource", "Priority", "SourceException", "SourceTenant",
        "ScopeUnresolved", "TypeTenant", "PaneForm", "PaneHistory", "PreviewTitle",
        "PvStartTitle", "PvSample", "PvPhoto", "PvLocation", "PvSign", "PvStart", "LastChange", "Unsaved", "CancelButton", "SaveButton",
        "HistoryTitle", "AuditReadOnly", "ColDate", "ColPerson", "ColChange", "ExceptionsInfo", "ExceptionsTitle",
        "XColRep", "XColRules", "XColRange", "XColReason", "XColPeople", "XColStatus", "OpenRow", "SaveTitle", "SaveConfirm",
        "XNewTitle", "XRep", "XRules", "XRulesHint", "XFrom", "XTo", "XReason", "XReasonPlaceholder", "XSodNew", "XSubmit",
        "XDetailTitle",
        "PaneReps", "RepsTitle", "RepsNote", "ColRep", "ColLocation", "ColPhoto", "ColQr", "ColDevice"
    ];
}
