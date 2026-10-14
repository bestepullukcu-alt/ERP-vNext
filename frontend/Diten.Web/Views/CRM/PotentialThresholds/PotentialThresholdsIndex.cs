namespace Diten.Web.Views.CRM.PotentialThresholds;

/// <summary>
/// Localization marker for the WP-VW-W4 (W4-WEB-b) potential thresholds page (tenant module: 7 languages).
/// <see cref="ScriptKeys"/> are the strings the page script reads (window.PotentialThresholdsL10n) — ONE list, so the view
/// serializes exactly these and the tests check exactly these in every language; <see cref="ViewKeys"/> the strings the
/// view writes itself.
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
        "Err_reference_data_unavailable", "Err_dependency_unavailable", "Err_permission_denied", "Err_unavailable", "Err_generic"
    ];

    /// <summary>The strings the view itself writes (headings, buttons, labels).</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "Crumb", "Intro", "AddProduct", "ReadOnly", "RetryButton", "ThresholdsTitle", "ThresholdsNote", "ColProduct",
        "ColLow", "ColMedium", "ColHigh", "ColSource", "ColLastChange", "RowActions", "HistoryTitle", "AuditReadOnly", "ColDate",
        "ColPerson", "ColTarget", "ColField", "ColChange", "ProductLabel", "LowLabel", "LowHint", "HighLabel", "HighHint", "PreviewTitle",
        "PreviewPatients", "CloseButton", "CancelButton", "SaveButton", "SaveTitle", "SaveConfirm"
    ];
}
