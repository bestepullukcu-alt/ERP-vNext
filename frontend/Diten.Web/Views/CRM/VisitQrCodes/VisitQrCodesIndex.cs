namespace Diten.Web.Views.CRM.VisitQrCodes;

/// <summary>
/// WP-VW-W3E (W3E-FIX, U-1) — the localization marker of the institution QR codes LIST, a Golden Compact server-mode page
/// (frontend-datatable-template.md: class = <c>{Module}Index</c>, resx <c>VisitQrCodesIndex.{lang}.resx</c>, 7 languages):
/// the title, the description, the column headers, the filter chips, the quick view and the bulk confirm. The QR words of
/// a row (chips, "parent code applies", the bulk result, the errors) stay <see cref="VisitQrIndex"/>'s, shared with the card.
/// </summary>
public sealed class VisitQrCodesIndex
{
    /// <summary>Every key of this page's own resx (the view, the L10n payload and the export headers read them).</summary>
    public static readonly string[] Keys =
    [
        "VisitQrCodesTitle", "PageDescription", "ColInstitution", "ColType", "ColCity", "ColStatus", "ColVersion", "ColLastScan",
        "ColRecent", "Actions", "StatusOpt_none", "StatusOpt_active", "StatusOpt_revoked", "FilterRegion", "FilterType",
        "QuickView", "BulkGenerateConfirm"
    ];
}
