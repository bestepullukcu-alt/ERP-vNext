namespace Diten.Web.Views.Commercial.PreOrders;

/// <summary>
/// Localization marker for the WP-VW-W6 (W6-WEB-a) pharmacy pre-order screens — B the list, A create / edit, Details, C the
/// approval, C-link, E the document and the pre-order settings (tenant module: 7 languages, one resource for the whole
/// set). The script keys travel in the standard L10n payload (_IndexL10n.cshtml → index.l10n.js → window.L10n).
/// <see cref="ScriptKeys"/> are the strings the page scripts read — ONE list, so the view serializes exactly these and the
/// tests check exactly these in every language; <see cref="ViewKeys"/> the strings only the views (and the export's column
/// headers) write. The rule messages are NOT here: they are the shared <c>CommercialRules</c> source (Rule.{code}).
/// </summary>
public sealed class PreOrdersIndex
{
    /// <summary>UXH-001 — the form's help code (COM-PREORDER-FORM; the list and the settings use their menu codes
    /// PREORDERS and PREORDERSETTINGS, which are COM-PREORDER-LIST and COM-PREORDER-SETTINGS).</summary>
    public const string FormHelpCode = "COMPREORDERFORM";

    /// <summary>UXH-001 — the approval's help code (COM-PREORDER-APPROVE): screen C and the e-mail link.</summary>
    public const string ApproveHelpCode = "COMPREORDERAPPROVE";

    public static readonly string[] ScriptKeys =
    [
        "PeriodAll", "PeriodLast7", "PeriodLast30", "PeriodMonth", "PeriodCustom", "StatusAll", "RepAll", "AllOption", "CountryLabel",
        "CountryAll", "AwaitingAll", "AwaitingOption", "AwaitingYou", "SearchHint", "Searching", "NoMatch", "ColTotal", "ColRule",
        "QuickView", "NewPreOrder", "EmptyTitle", "EmptyText", "QuantityModeShort", "OpenEdit", "OpenDetails", "DR_delivered", "DR_partial",
        "DepotResponseSaved", "OfQuantity", "St_draft", "St_pending_approval", "St_approved", "St_transmitted", "St_transmission_failed",
        "St_rejected", "St_cancelled", "St_unknown", "Ev_created", "Ev_updated", "Ev_discarded", "Ev_signature_captured",
        "Ev_signature_cleared", "Ev_submitted", "Ev_auto_approved", "Ev_approval_requested", "Ev_approver_unresolved", "Ev_approved",
        "Ev_rejected", "Ev_changes_requested", "Ev_transmission_attempted", "Ev_transmitted", "Ev_transmission_failed", "Ev_cancelled",
        "Ev_depot_response_recorded", "Ev_copied_from", "Ev_unknown", "TimelineTitle", "TimelineEmpty", "ReasonLine", "NoteLine",
        "FactCode", "FactDate", "FactPharmacy", "FactDepot", "FactRep", "FactDelivery", "FactSeller", "FactMode", "ModePriced",
        "ModeQuantity", "ColProduct", "ColQty", "ColMf", "ColDiscount", "ColUnitPrice", "ColVat", "ColLineTotal", "ColCombined",
        "GtinShort", "FreeBoxes", "CombinedLine", "MfRatioLine", "PerBox", "TotalNet", "TotalVat", "TotalEffective", "TotalGross", "FxLine",
        "QuantityModeNote", "FormTitleOrder", "DiscardConfirm", "DraftSaved", "ModePricedBadge", "ModeQuantityBadge", "PriceSource",
        "PriceSourceShort", "Tier_pharmacist_purchase", "Tier_depot", "Tier_public", "ChangeBannerTitle", "LinesNotePriced",
        "LinesNoteQuantity", "RemoveLine", "NoLinesYet", "ProductPlaceholder", "PreviousOrdersNone", "LinesCount", "SummaryPending",
        "Need_yes", "Need_no", "Need_unknown", "Need_tenant_setting", "Need_soft", "Approver_line", "Approver_chain", "Approver_none",
        "Approver_unavailable", "ApproverPosition", "SignatureSaved", "SignatureNone", "SignedBy", "SignedNote", "Send",
        "Why_no_permission", "Why_no_lines", "Why_hard", "Why_no_depot", "Why_no_signature", "Why_unsaved", "SubmittedPending",
        "SubmittedApproved", "Lock_pending", "Lock_sent", "Lock_rejected", "Lock_cancelled", "Lock_not_owner", "CancelAndNew", "CopyAndNew",
        "CancelTitle", "CancelText", "CancelConfirm", "CancelledToast", "ErrQuantity", "ErrMf", "ErrDiscount", "ErrRangeMissing",
        "ErrRangeOrder", "Err_generic", "Err_unavailable", "Err_permission_denied", "Err_not_found", "Err_conflict", "DetailsTitleOrder",
        "AutoApproved", "Decision_approve", "Decision_reject", "Decision_request_changes", "AwaitingApprover", "AwaitingApproverUnknown",
        "TransmittedAt", "TransmissionFailedLine", "CancelledLine", "PartialLine", "RetryToast", "ApproveTitleOrder", "WhyMeNone",
        "Why_me_tenant", "Why_me_soft", "YouAreReportsTo", "LimitSoft", "LimitHard", "Approve", "Reject", "RequestChanges", "RejectTitle",
        "RejectText", "ChangesTitle", "ChangesText", "Decided_approve", "Decided_reject", "Decided_request_changes", "NotYourDecision",
        "AlreadyDecided", "HistoryTitle", "HistoryTitleOf", "ColMonth", "ColOrders", "ColBoxes", "LinkOnce", "LinkUsedNow", "Link_consumed",
        "Link_consumed_text", "Link_expired", "Link_expired_text", "Link_superseded", "Link_superseded_text", "Link_notfound",
        "Link_notfound_text", "DocumentTitleOrder", "TrailNone", "TrailAttempt", "Trail_sent", "Trail_failed", "TierLabel", "TierNone",
        "PrefixNone", "CompanyLabel", "PrefixLabel", "RemoveRow", "SettingsSaved", "SettingsDefault", "SettingsUpdated", "ErrTtl",
        "ErrRetry", "ErrPrefix", "ErrTierDuplicate",
        // W6-FIX-b — the summary rows (V-3), the header (V-5), the send button (V-5), the confirmations (G-6 / G-7)
        "TotalListSubtotal", "TotalDiscount", "TotalVatRate", "FormSubtitleRep", "FormDescription", "SendForApproval", "DiscardDraft",
        "ApproveConfirmTitle", "ApproveConfirmText"
    ];

    /// <summary>The strings the views themselves write (headings, labels, buttons) and the export's column headers.</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageDescription", "PeriodLabel", "CustomFrom", "CustomTo", "StatusLabel", "RepLabel", "PharmacyLabel", "DepotLabel",
        "ProductLabel", "AwaitingLabel", "ColCode", "ColDate", "ColPharmacy", "ColDepot", "ColRep", "ColLines", "ColLocal", "ColEffective",
        "ColStatus", "ColActions", "DrawerTitle", "CloseButton", "OpenDocument", "OpenApproval", "DepotResponseTitle", "DepotResponseNote",
        "DepotResponseSave", "FormTitleCreate", "FormTitleEdit", "BackToList", "SaveDraft", "FromSystem",
        "PharmacySection", "PharmacyPlaceholder", "LinesSection", "AddLine", "PreviousOrders", "DeliverySection", "DepotPlaceholder",
        "DeliveryLabel", "CurrencyLabel", "NoteLabel", "NotePlaceholder", "SummarySection", "NeedTitle", "SignatureSection", "SignerLabel",
        "SignatureHint", "SignatureClear", "SignatureSave", "Resign", "CancelOrder", "DetailsTitle", "DetailsDescription", "StateSection",
        "RetryTransmission", "ApproveTitle", "ApproveDescription", "WhyMeTitle", "LimitsNote", "DecisionTitle", "DecisionNote",
        "ReasonLabel", "ReasonPlaceholder", "HistoryNote", "LinkTitle", "LinkDescription", "DocumentTitle", "DocumentDescription",
        "CopyGroupLabel", "CopyDepot", "CopyPharmacy", "DownloadPdf", "TrailTitle", "SettingsTitle", "SettingsDescription",
        "ApprovalSection", "Mode_always", "Mode_always_help", "Mode_auto", "Mode_auto_help", "TtlLabel", "TtlHelp", "RetryLabel",
        "RetryHelp", "TierSection", "TierHelp", "AddTier", "PrefixSection", "PrefixHelp", "AddPrefix", "SaveSettings"
    ];
}
