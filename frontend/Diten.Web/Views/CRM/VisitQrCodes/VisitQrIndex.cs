namespace Diten.Web.Views.CRM.VisitQrCodes;

/// <summary>
/// Localization marker for the WP-VW-W3E (W3E-WEB-a) institution QR screens (tenant module: 7 languages): the account
/// card + dialogs + history (Q1 / Q3 / Q4), the bulk page (Q5) and the print page (Q2). <see cref="ScriptKeys"/> are the
/// strings the scripts read (window.VisitQrL10n) — ONE list, so the views serialize exactly these and the tests check
/// exactly these in every language (the views write their own headings with the same keys).
/// </summary>
public sealed class VisitQrIndex
{
    public static readonly string[] ScriptKeys =
    [
        "CardTitle", "Status_active", "Status_revoked", "Status_none", "HistoryLink", "NoneText", "InheritedInfo",
        "Generate", "PreviewLocked", "ActiveLine", "ScanLine", "NeverScanned", "ValidNote", "PrintButton",
        "RegenerateButton", "RevokeButton", "ReadOnly", "RevokedLine", "ReasonLine", "RevokedNote", "NewCode",
        "ToastIssued", "ToastRegenerated", "ToastRevoked", "RegenTitle", "RegenWarn", "VersionArrow", "ReasonLabel",
        "Optional", "ReasonPlaceholderRegen", "CancelButton", "CloseButton", "RevokeTitle", "RevokeText", "RevokeInfo",
        "ReasonPlaceholderRevoke", "ReasonCount", "ReasonRequired", "HistoryTitle", "Ev_issued", "Ev_regenerated",
        "Ev_revoked", "Ev_printed", "Ev_bulk_issued", "PrintedDetail", "Paper_a4", "Paper_a5", "Paper_labels4",
        "Paper_labels8", "NoHistory", "Today", "Yesterday", "Err_visit_execution_concurrency_conflict",
        "Err_visit_qr_signing_unavailable", "Err_visit_qr_not_active", "Err_visit_qr_account_not_found",
        "Err_visit_qr_request_invalid", "Err_permission", "Err_generic", "LoadFailed", "Retry", "PageTitle", "Crumb",
        "Intro", "FiltersTitle", "AllRegions", "AllTypes", "AllCities", "AllStatuses", "StatusOpt_none", "StatusOpt_active",
        "StatusOpt_revoked", "SearchPlaceholder", "CountLabel", "SelectedLabel", "BulkGenerate", "BulkPrint", "BulkLimit",
        "ColInstitution", "ColType", "ColCity", "ColStatus", "ColVersion", "ColLastScan", "ColRecent", "ColActions",
        "Chip_active", "Chip_revoked", "Chip_none", "InheritsParent", "VersionShort", "BulkResult", "PrintLabelsLink",
        "EmptyTitle", "ClearFilters", "RowPrint", "RowOpen", "SelectAll", "SelectRow", "PageInfo", "PrevPage", "NextPage",
        "PrintTitle", "PrintCrumb", "ModeSingle", "ModeLabels", "PaperLabel", "PerA4", "Labels4", "Labels8", "CopiesLabel",
        "HintSingle", "HintLabels", "StickerText", "StickerFooter", "LabelFooter", "CutHint", "PreviewNote",
        "NothingToPrint", "NoSelection", "SkippedNote", "BackToList"
    ];
}
