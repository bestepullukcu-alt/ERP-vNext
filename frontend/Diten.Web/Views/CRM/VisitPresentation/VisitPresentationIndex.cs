namespace Diten.Web.Views.CRM.VisitPresentation;

/// <summary>
/// Localization marker for the WP-VW-W3 Visit Presentation (tenant module: 7 languages). <see cref="ScriptKeys"/> are the
/// strings the page script reads (window.VisitPresentationL10n) — ONE list, so the view serializes exactly these and the
/// tests check exactly these in every language.
/// </summary>
public sealed class VisitPresentationIndex
{
    public static readonly string[] ScriptKeys =
    [
        "ActionFailed", "LoadFailed", "NotStartedTitle", "NotStartedText", "BackToWorkspace", "NoSlides",
        "StartedLine", "LiveChip", "Kind_in_person", "Kind_remote", "DoctorScreenHint", "ExitHold", "RepTitle",
        "BackToDoctor", "DoctorMode", "TotalLabel", "DoctorSees", "PrevSlide", "NextSlide", "SkipProduct",
        "LastProduct", "TabNotes", "TabMarks", "TabNote", "KeyMessage", "ThisSlide", "TargetLabel", "TargetSeconds",
        "TargetMinutes", "TalkTrack", "ForThisDoctor", "BriefLastVisit", "BriefOutcome", "BriefFollowUp",
        "BriefSamples", "Objections", "MyNote", "MyNotePlaceholder", "TransitionHeading", "QuickMarks",
        "Interest_interested", "Interest_neutral", "Interest_not_interested", "Flag_question", "Flag_objection",
        "Flag_sample_request", "Flag_literature_request", "Flag_follow_up", "ObjectionCategory", "ObjectionWith",
        "VisitNote", "VisitNotePlaceholder", "VoiceNote", "Listening", "JointVisit", "CoVisitorsPlaceholder",
        "CoVisitorsMax", "NoPeople", "DurationConsistency", "FinishVisit", "FinishConfirmTitle", "FinishConfirmText",
        "FinishConfirmYes", "CancelButton", "PinSetTitle", "PinConfirmTitle", "PinUnlockTitle", "PinMismatch",
        "PinWrong", "SummaryTitle", "SummaryLine", "SummaryLineNoAccount", "DurationHeading", "RolePromo", "RoleReminder", "MinutesClock",
        "ShortVisitWarning", "NotesHeading", "CoVisitorsLine", "SignatureTitleApproval", "SignatureTitleSample",
        "SignatureTextApproval", "SignatureTextSample", "ClearSignature", "ConfirmVisit", "SignatureSave",
        "SignatureSaved", "Signed", "EnterReport", "Later", "LaterHint", "CaptureFailed", "ImagePlaceholder",
        "Err_visit_execution_finished", "Err_visit_execution_not_finished", "Err_visit_signature_exists",
        "Err_visit_report_deadline_passed", "Err_visit_capture_invalid", "Err_visit_co_visitor_invalid",
        "Err_visit_evidence_store_unavailable", "Err_visit_already_reported", "Err_visit_execution_not_found",
        "Err_visit_execution_concurrency_conflict", "Err_resource_not_caller"
    ];

    /// <summary>The strings the view itself writes.</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "BackLabel", "KindLabel", "Loading", "DoctorScreen", "RepPanel"
    ];
}
