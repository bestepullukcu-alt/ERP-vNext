namespace Diten.Web.Views.CRM.PharmacyReport;

/// <summary>
/// Localization marker for the WP-VW-W4 (W4-WEB-b) pharmacy visit report page (tenant module: 7 languages).
/// <see cref="ScriptKeys"/> are the strings the page script reads (window.PharmacyReportL10n) — ONE list, so the view
/// serializes exactly these and the tests check exactly these in every language; <see cref="ViewKeys"/> the strings the
/// view writes itself. BRD option labels (met role, stock level, shelf, trend, recommendation, request, advantage) come
/// from the pre-fill's optionSets, never from here.
/// </summary>
public sealed class PharmacyReportIndex
{
    public static readonly string[] ScriptKeys =
    [
        // the steps
        "Step_general", "Step_stock", "Step_order", "Step_market", "Step_safety", "Step_summary", "StepMissing", "StepOk",
        "StepOf", "NextStep",
        // the header / state
        "MissingCount", "ReadyToSend", "SavedAt", "Unsaved", "DraftStored", "NotSaved", "SentText", "SentAmended", "NotPharmacy",
        // general
        "Outcome", "OutcomeDone", "OutcomeHint", "ActualTimes", "ServerTime", "Minutes", "VisitKind", "Kind_in_person",
        "Kind_remote", "MetRole", "GeneralNote", "OptionsUnavailable",
        // stock and shelf
        "StockTitle", "NearExpiry", "ReturnIssue", "RemoveRow", "NoSkus", "GskuSearch", "GskusNoPermission", "ShelfTitle",
        "MaterialStand", "ShelfPhoto", "PhotoTake", "PhotoReplace", "PhotoHint", "PhotoSaved",
        // order and sales
        "OrderTitle", "NoOrder", "CreateOrder", "Soon", "OrderSoonHint", "SalesTitle", "SalesHint", "BoxesPerMonth",
        // competitor and recommendation
        "CompetitorTitle", "CompetitorsUnavailable", "NoCompetitor", "ProposedBadge", "AdvantageHint", "ProposalOpen",
        "ProposalName", "ProposalCompany", "ProposalAdd", "ProposalHint", "RecommendationTitle", "LinkedDoctors", "NoLinked",
        "DoctorSearch", "TrainingTitle", "TrainingGiven", "RequestType", "RequestDate", "RequestAdd",
        // safety
        "AeQuestion", "AeWarning", "AeNote", "AnswerYes", "AnswerNo",
        // summary
        "SumStock", "SumSales", "SumCompetitors", "SumAeYes", "SumAeNo", "SumAeOpen",
        "Miss_visitKindCode", "Miss_adverseEvent", "Miss_adverseEventNote", "Miss_metRoleCode", "Miss_stock_levelCode",
        "Miss_sales_trendCode", "Miss_competitors", "Miss_generic",
        // the actions
        "SubmitButton", "SendFix", "DraftSaved", "Submitted", "SubmittedWithWarnings", "Amended", "WindowClosed",
        // the field checks
        "ErrStockLevel", "ErrTrend", "ErrCompetitors", "ErrAe", "ErrAeNote", "ErrProposalName", "ErrProposalCompany",
        "ErrTooLong", "ErrBoxes", "ErrReason", "ErrReasonLong",
        // the refusals
        "Err_report_incomplete", "Err_report_code_invalid", "Err_report_v2_invalid", "Err_reference_data_unavailable",
        "Err_visit_report_edit_window_closed", "Err_visit_report_concurrency_conflict", "Err_visit_report_amendment_reason_required",
        "Err_visit_report_not_finalised", "Err_visit_report_plan_cancelled", "Err_visit_report_deadline_passed",
        "Err_visit_not_yet_due", "Err_visit_photo_invalid", "Err_shelf_photo_pharmacy_only", "Err_shelf_photo_report_submitted",
        "Err_shelf_photo_not_found", "Err_visit_evidence_store_unavailable", "Err_dependency_unavailable",
        "Err_conflict", "Err_permission_denied", "Err_unavailable", "Err_generic"
    ];

    /// <summary>The strings the view itself writes (headings, buttons, labels).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "BackButton", "ReadOnly", "SentTitle", "Unlock", "FixTitle", "FixText", "FixPlaceholder", "StepsLabel",
        "Step_general", "Step_safety", "Step_summary", "ShelfPhoto", "PrevButton", "SaveDraft"
    ];
}
