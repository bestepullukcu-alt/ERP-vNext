namespace Diten.Web.Views.CRM.KnowledgePaths;

/// <summary>WP-KP-UI-1 — resource marker of the Knowledge Path Studio (list additions, new path, workspace, composition):
/// Resources/Views/CRM/KnowledgePaths/KnowledgePathStudio.{en,tr,fr,es,zh,ar,ru}.resx. The legacy editor keeps
/// KnowledgePathsIndex.</summary>
public sealed class KnowledgePathStudio
{
    /// <summary>WP-VW-W3 (W3-WEB-c) — the "Sunum notları" tab's strings (presentation-notes.js + _PresentationNotes.cshtml).
    /// The studio bridge serializes the whole family; this list is what the tests check in seven languages.</summary>
    public static readonly string[] PresentationKeys =
    [
        "PnButton", "PnBar", "PnMissing", "PnFilledRule", "PnFilled", "PnEmpty", "PnStepOf", "PnTabContent",
        "PnTabNotes", "PnRequired", "PnEstimatedDuration", "PnPrerequisite", "PnContentNote", "PnReadOnly",
        "PnKeyMessage", "PnTalkTrack", "PnTarget", "PnSeconds", "PnTargetHint", "PnTargetHintNone", "PnEstimate",
        "PnTransition", "PnObjections", "PnObjectionsHint", "PnReasonObjectionReadMissing",
        "PnReasonObjectionNoProduct", "PnCreateInLibrary", "PnCreateUnsaved", "PnRefresh", "PnAddObjection",
        "PnPickerSearch", "PnPickerEmpty", "PnPickerMax", "PnClaimCount", "PnRemove", "PnUnusable", "PnPreview",
        "PnDoctorScreen", "PnNotHere", "PnRemote", "PnThisSlide", "PnTargetShort", "PnApprovedAnswer", "PnSave",
        "PnCancel", "PnSaved", "PnInvalid", "PnUnsavedClose", "PnDuration", "PnSecondsValue",
        "Err_path_step_presentation_invalid", "Err_path_step_objection_invalid", "Err_path_step_objection_not_found"
    ];
}
