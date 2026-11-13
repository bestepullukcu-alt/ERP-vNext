namespace Diten.Web.Views.CRM.VisitEvidenceFlags;

/// <summary>
/// Localization marker for the WP-VW-W3E (W3E-WEB-b) visit evidence flags page (tenant module: 7 languages).
/// W3E-FLAGS-STD — the script keys travel in the standard L10n payload (_IndexL10n.cshtml → index.l10n.js → window.L10n).
/// <see cref="ScriptKeys"/> are the strings the page script reads (window.VisitEvidenceFlagsL10n) — ONE list, so the view
/// serializes exactly these and the tests check exactly these in every language; <see cref="ViewKeys"/> the strings the
/// view writes itself.
/// </summary>
public sealed class VisitEvidenceFlagsIndex
{
    public static readonly string[] ScriptKeys =
    [
        "PeriodLast7", "PeriodLast7Short", "PeriodLast30", "PeriodCustom", "RepAllTeam", "RepAllTenant", "FlagAll", "FlagSome",
        "AccountAll", "SumOther", "SumNoFlags", "Percent", "UnitMeters", "UnitKm", "Accuracy", "Minutes", "MinSec", "Seconds",
        "ExceptionLink", "EmptyTitle", "EmptyAllTitle", "EmptyText", "PageFlagged", "PageAll", "RepsEmpty", "Kind_in_person",
        "Kind_remote", "Flag_location_far", "Flag_location_missing", "Flag_location_inaccurate", "Flag_account_location_unknown",
        "Flag_short_visit", "Flag_started_with_exception", "Flag_qr_not_issued", "Flag_qr_invalid", "Flag_qr_wrong_account",
        "Flag_qr_revoked", "Flag_qr_unverifiable", "Flag_mock_location", "Flag_device_integrity_failed",
        "Flag_device_unverified", "Chip_qr_missing", "SecLocation", "LocDistance", "LocAccuracy", "LocRadius", "LocNotMeasured",
        "SecQr", "QrResult", "QrInstitution", "QrVersion", "QrVersionValue", "QrTime", "QrParent", "QrSt_ok", "QrSt_missing",
        "QrSt_missing_exception", "QrSt_invalid", "QrSt_wrong_account", "QrSt_revoked", "QrSt_not_issued", "QrSt_not_applicable",
        "QrSt_remote", "QrSt_unverifiable", "SecDevice", "DevPlatform", "DevApp", "DevVerdict", "Plat_web", "Plat_android",
        "Plat_ios", "AppWeb", "Verdict_passed", "Verdict_failed", "Verdict_web", "Verdict_unavailable", "Verdict_mock",
        "Verdict_remote", "Verdict_none", "SecDuration", "DurVisit", "DurSlides", "SlidesValue", "SecPhoto", "PhotoAlt",
        "PhotoCaption", "PhotoNoPermission", "PhotoNone", "SecException", "ExRule", "ExRange", "ExApproved", "ExOpen",
        "XRule_location_info", "XRule_photo_optional", "XRule_account_without_location_allow", "XRule_qr_optional",
        "ErrRangeMissing", "ErrRangeOrder", "ErrRangeLong", "Err_visit_evidence_flags_range_invalid",
        "Err_visit_evidence_flags_filter_invalid", "Err_visit_evidence_flags_not_found", "Err_permission_denied",
        "Err_unavailable", "Err_generic", "QuickView", "OpenVisit", "TabVisits"
    ];

    /// <summary>The strings the view itself writes (headings, buttons, labels).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageDescription", "SumStarted", "SumFlagged", "SumReview", "SumByFlag", "SumByFlagNote", "PeriodLabel",
        "CustomFrom", "CustomTo", "RepLabel", "AccountLabel", "OnlyFlagged", "TabVisits", "TabReps", "ColStart", "ColRep",
        "ColTarget", "ColKind", "ColFlags", "ColDuration", "ColException", "ColStarted", "ColFlagged", "ColRate", "ColTopFlag",
        "RepsHint", "DrawerTitle", "CloseButton", "OpenVisit", "FlagTypeLabel", "ColActions"
    ];
}
