namespace Diten.Web.Views.CRM.PotentialThresholds;

/// <summary>
/// Localization marker for the WP-VW-W4 (W4-WEB-b) potential thresholds page (tenant module: 7 languages).
/// <see cref="ScriptKeys"/> are the strings the page scripts read (window.L10n, through the standard payload
/// _IndexL10n.cshtml — W3E-E4R2-FIX-c) — ONE list, so the payload serializes exactly these and the tests check exactly these
/// in every language; <see cref="ViewKeys"/> the strings the views write themselves.
/// </summary>
public sealed class PotentialThresholdsIndex
{
    public static readonly string[] ScriptKeys =
    [
        "TenantDefault", "Source_product", "Source_default", "Source_built_in", "BandLow", "BandMedium", "BandHigh",
        "Field_lowBelow", "Field_highAbove", "Field_own", "OwnRow", "Never", "EditButton", "EditTitle", "NewTitle",
        "ProductPlaceholder", "SaveText", "SaveTextDefault", "Saved", "NothingChanged", "NoProducts", "NoHistory",
        "HistoryDefault", "YouLabel", "Class_low", "Class_medium", "Class_high", "ErrRequired", "ErrRange", "ErrOrder", "ErrProduct",
        "Err_potential_threshold_invalid", "Err_version_conflict", "Err_doctor_insights_product_invalid", "Err_doctor_insights_invalid",
        "Err_reference_data_unavailable", "Err_dependency_unavailable", "Err_permission_denied", "Err_unavailable", "Err_generic",
        "AddProduct"
    ];

    /// <summary>The strings the views themselves write (header, tabs, columns, filter, drawer, confirm).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "PageDescription", "ReadOnly", "RetryButton", "ThresholdsTitle", "ThresholdsNote", "ColProduct",
        "ColLow", "ColMedium", "ColHigh", "ColSource", "ColLastChange", "RowActions", "HistoryTitle", "AuditReadOnly", "ColDate",
        "ColPerson", "ColTarget", "ColField", "ColChange", "ProductLabel", "LowLabel", "LowHint", "HighLabel", "HighHint", "PreviewTitle",
        "PreviewPatients", "CloseButton", "CancelButton", "SaveButton", "SaveTitle", "SaveConfirm", "QuickView", "EditButton"
    ];
}
