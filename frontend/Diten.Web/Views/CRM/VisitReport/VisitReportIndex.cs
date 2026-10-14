namespace Diten.Web.Views.CRM.VisitReport;

/// <summary>
/// Localization marker for the WP-VW-W4 visit report wizard v2 (tenant module: 7 languages). <see cref="ScriptKeys"/> are
/// the strings the page scripts read (window.VisitReportL10n) — ONE list, so the view serializes exactly these and the
/// tests check exactly these in every language. WEB-b (the pharmacy report) appends its own keys here.
/// </summary>
public sealed class VisitReportIndex
{
    public static readonly string[] ScriptKeys =
    [
        "TitleDoctor", "DeadlineChip", "MissingChip", "ReadyChip", "AutoDraft", "AutoSaved", "Saving", "NoVisitTitle",
        "NoVisitText", "NotFoundTitle", "NotFoundText", "LoadFailed", "PharmacyTitle", "PharmacyText", "BackToWorkspace",
        "UnknownProduct", "UnknownStep", "SentTitle", "SentText", "BackToCalendar", "ViewReport", "SubmittedTitle",
        "EditableFor", "WindowClosed", "UnlockButton", "FixButton", "FixTitle", "FixText", "FixReasonPlaceholder",
        "WarnProposalPending", "WarnSideEffect", "StepGeneral", "StepSafety", "StepDoctor", "StepSummary", "StepMissing",
        "StepReady", "StepDone", "StepProduct", "StepOf", "BackButton", "NextTo", "NextShort", "SaveDraftShort", "SaveDraft",
        "Submit", "SubmitFix", "OutcomeLabel", "OutcomeDone", "OutcomeHint", "ActualTime", "ServerTime", "NoExecution",
        "MinutesShort", "SecondsShort", "VisitKind", "Kind_in_person", "Kind_remote", "JointVisit", "CoVisitorCount",
        "NoCoVisitor", "MetPerson", "PlannedDoctor", "GeneralNote", "FromPresentation", "AddUnplanned", "UnplannedTag",
        "UnplannedAdded", "Role_promo", "Role_non_promo", "SwipeHint", "GroupA", "GroupB", "GroupC", "GroupDone",
        "PresentedSteps", "MatchedPlan", "Deviation", "NoStepsShown", "Minutes", "MinutesHint", "VisitSuccess",
        "VisitSuccessHint", "Commitment", "Interest", "AdoptionStage", "PreviousVisit", "SameButton", "IndicationPatients",
        "IndicationHint", "ThreeMonthHint", "NoIndicationList", "PatientsPerMonth", "PreviousValue", "NoPatients",
        "LastPotential", "UpdateNumbers", "DoctorTotalLine", "TotalExceeded", "ShareBucket", "ComputedLabel", "PotentialLabel",
        "PatientsCount", "ThresholdLine", "PreviewNote", "Class_low", "Class_medium", "Class_high", "Opp_opportunity",
        "Opp_grow", "Opp_keep", "Opp_low_priority", "Competitors", "CompetitorsUnavailable", "NoCompetitor", "ActiveIngredient",
        "FormStrength", "BoxPrice", "ReimbursementLabel", "ProposalPending", "ProposalOnSubmit", "NotInList", "CompetitorName",
        "CompetitorCompany", "AddButton", "DidYouMean", "ProposalNeedsBoth", "ProposalLater", "AdvantageHint", "ObjectionsLabel",
        "AddObjection", "NoObjection", "SelectCategory", "LibraryAnswer", "NoLibraryAnswer", "NoteLabel", "Resolved",
        "Resolved_yes", "Resolved_partly", "Resolved_no", "RemoveLabel", "Requests", "AddRequest", "NoRequest", "SelectRequest",
        "SampleLabel", "Choose", "NoSample", "SampleRecorded", "NoGskus", "QuantityLabel", "Lot", "SampleSigned",
        "SampleNotSigned", "NextMessage", "NextMessagePlaceholder", "Suggestion", "UseSuggestion", "SuggestStage",
        "Suggest_commitment_will_try", "Suggest_commitment_will_prescribe", "Suggest_commitment_wants_info", "FollowUpRequired",
        "FollowUpDate", "FollowUpNote", "AdverseEventQ", "AdverseEventYes", "AdverseEventNote", "OffLabelQ", "OffLabelYes",
        "YesOption", "NoOption", "ProfileHint", "Typology", "CommunicationStyle", "KolLevel", "OnRecord", "ChangedTag",
        "ChangeCount", "NoChange", "TotalPatients", "LastTotal", "TotalHint", "TotalBelowProducts", "BestTime",
        "PreferredChannel", "ReadOnlyHere", "ShowHistory", "HideHistory", "SourceReport", "SourceManual",
        "Hist_adoptionTypologyCode", "Hist_communicationStyleCode", "Hist_kolLevelCode", "Hist_totalMonthlyPatients",
        "Hist_shareBucketCode", "Hist_noPatientsInIndications", "Day_monday", "Day_tuesday", "Day_wednesday", "Day_thursday",
        "Day_friday", "Day_saturday", "Day_sunday", "SummaryTitle", "JointWith", "CompetitorTag", "SampleTag", "EditButton",
        "AdverseEventShort", "OffLabelShort", "NoAnswer", "MissingTitle", "ReadyText", "ServerMissing", "MissingToast",
        "AmendmentsTitle", "SubmitSaved", "FixSaved", "DraftConflict", "ListUnavailable", "ActionFailed", "Field_visitKindCode",
        "Field_metContactId", "Field_adverseEvent", "Field_adverseEventNote", "Field_offLabelRequest",
        "Field_totalMonthlyPatients", "Field_section", "Field_minutes", "Field_visitSuccessCode", "Field_commitmentCode",
        "Field_interestCode", "Field_adoptionStageCode", "Field_shareBucketCode", "Field_potential",
        "Field_potentialExceedsTotal", "Field_totalBelowProducts", "Field_competitors", "Field_objections",
        "Field_objections_categoryCode", "Field_objections_resolved", "Field_requests", "Field_requests_typeCode",
        "Field_sample", "Field_nextMessage", "Field_amendReason", "Err_report_incomplete", "Err_report_code_invalid",
        "Err_report_v2_invalid", "Err_report_not_v2", "Err_reference_data_unavailable", "Err_visit_report_concurrency_conflict",
        "Err_visit_report_edit_window_closed", "Err_visit_report_deadline_passed", "Err_visit_execution_not_finished",
        "Err_visit_report_plan_cancelled", "Err_visit_not_yet_due", "Err_potential_exceeds_total",
        "Err_visit_report_amendment_reason_required", "Err_visit_report_not_finalised", "Err_resource_not_caller",
        "Err_visit_report_planned_visit_not_found", "Form_tablet", "Form_capsule", "Form_syrup", "Form_injection",
        "Form_ointment", "Form_cream", "Form_drops", "Form_inhaler", "Form_patch", "Form_other", "Form_film_coated_tablet",
        "Form_modified_release_tablet", "Form_extended_release_tablet", "Form_modified_release_capsule", "Form_ampoule"
    ];

    /// <summary>The strings the view itself writes.</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "BackLabel", "Loading", "StepsLabel"
    ];
}
