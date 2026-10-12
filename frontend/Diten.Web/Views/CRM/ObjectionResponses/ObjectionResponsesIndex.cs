namespace Diten.Web.Views.CRM.ObjectionResponses;

/// <summary>
/// Localization marker for the WP-VW-W3 (W3-WEB-c) objection library (tenant module: 7 languages; resx
/// <c>Resources/Views/CRM/ObjectionResponses/ObjectionResponsesIndex.*.resx</c>). <see cref="ScriptKeys"/> are the strings the
/// page scripts read (<c>window.ObjectionL10n</c>) — ONE list, so the views serialize exactly these and the tests check exactly
/// these in every language. <see cref="ViewKeys"/> are the strings the Razor views print themselves.
/// <para>The knowledge path studio's "Sunum notları" tab reads its own strings from the studio resx
/// (<c>KnowledgePathStudio</c>, <see cref="Diten.Web.Views.CRM.KnowledgePaths.KnowledgePathStudio.PresentationKeys"/>).</para>
/// </summary>
public sealed class ObjectionResponsesIndex
{
    public static readonly string[] ScriptKeys =
    [
        // list
        "ColProduct", "ColCategory", "ColQuestion", "ColClaims", "ColUsage", "ColStatus", "ColUpdated", "ColActions",
        "NewResponse", "ViewRecord", "EditRecord", "AllProducts", "AllCategories", "AllStatuses", "SearchPlaceholder",
        "Status_draft", "Status_in_review", "Status_approved", "Status_retired",
        // editor
        "EditTitle", "NewTitle", "SelectProduct", "SelectCategory", "ClaimsSelected", "ClaimsMax", "ClaimsNone",
        "ClaimsPickProduct", "ClaimsUnavailable", "ProductsUnavailable", "CategoriesUnavailable", "PreviewEmptyQuestion",
        "PreviewEmptyAnswer", "SaveDraft", "Saved", "Submitted", "SubmitForApproval", "CancelButton", "UnsavedChanges",
        "CreatedBy", "LastSaved", "ProductRequired",
        // record page
        "PendingLine", "ApprovedNote", "RetiredLine", "DraftReturnedLine", "DraftReturnedReason", "SodHint", "WithdrawButton",
        "ApproveButton", "ReturnToDraft", "RetireButton", "Copy", "EditButton", "ReturnTitle", "ReturnNoteLabel", "ReturnNoteRequired",
        "ReturnConfirm", "RetireTitle", "RetireText", "RetireUsed", "RetireConfirm", "WithdrawConfirm", "SubmitConfirm",
        "ApproveConfirm", "Approved", "Returned", "Withdrawn", "Retired", "Copied", "UsagesNone", "UsagePath",
        "UsageVersion", "UsageStep", "UsageStepLine", "UsageCount", "PathStatus_draft", "PathStatus_review", "PathStatus_approved",
        "PathStatus_published", "PathStatus_inactive", "PathStatus_archived", "HistoryTitle", "Event_created",
        "Event_submitted", "Event_approved", "Event_returned", "Event_withdrawn", "Event_timeout", "Event_retired",
        "EventLine", "UnknownUser", "ClaimNotApprovedAnymore", "NotFound", "LoadFailed",
        // shared
        "ActionFailed", "Err_objection_response_incomplete", "Err_objection_claim_not_approved",
        "Err_objection_claim_product_mismatch", "Err_objection_response_sod", "Err_objection_response_in_review",
        "Err_objection_response_not_editable", "Err_objection_response_not_draft", "Err_approval_via_workflow_only",
        "Err_reference_data_unavailable", "Err_objection_category_invalid", "Err_objection_product_invalid",
        "Err_objection_response_invalid", "Err_objection_response_not_found", "Err_decision_comment_required",
        "Err_dependency_unavailable", "Err_version_conflict", "Err_service_unavailable"
    ];

    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageGroup", "PageDescription", "EmptyTitle", "EmptyText", "SectionContent",
        "FieldProduct", "FieldCategory", "FieldQuestion", "FieldAnswer", "FieldClaims", "ClaimsHint", "PreviewTitle",
        "PreviewRemote", "PreviewObjections", "MetaNote", "RecordTitle", "UsagesTitle", "CloseButton"
    ];
}
