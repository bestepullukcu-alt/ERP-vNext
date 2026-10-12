namespace Diten.Web.Views.CRM.Claims;

public sealed class ClaimsIndex;

/// <summary>WP-CL-FE-1 — the keys the list page hands to index.js. One list, used by the view AND the L10n guard test, so
/// a key the page needs cannot silently miss a language.</summary>
public static class ClaimsIndexL10nKeys
{
    /// <summary>Keys added by WP-CL-FE-1 (no key echo allowed in any language).</summary>
    public static readonly IReadOnlyList<string> NewKeys =
    [
        "ListDescription", "ApprovalViaWorkflowNote", "NewCoreClaim", "NewLocalClaim", "ReadOnlyNote",
        "ColClaim", "ColProduct", "ColKind", "ColAudience", "ColCountryStatus", "ColEvidence", "ColApproved", "ColUsage",
        "KindCore", "KindLocal", "EvidenceMissing", "ApprovedOfCountries", "CoreVersionLine", "LocalLine",
        "State_approved", "State_in-review", "State_draft", "State_review-required", "State_expiring", "State_closed",
        "State_not-opened", "State_not-applicable", "State_inactive", "State_archived",
        "ClosedTooltip", "NotOpenedTooltip",
        "FilterProduct", "FilterCountry", "FilterLanguage", "FilterStatus", "FilterAudience", "FilterKind",
        "FlagEvidenceMissing", "FlagReviewRequired", "FlagExpiring",
        "NewVersion", "NewVersionConfirm", "RecordNewVersion",
        "QvCoreText", "QvLocalText", "QvResponsibleTeam", "QvCountryVersions", "QvNoCountryVersions", "QvLanguages",
        "QvAudienceCount", "QvOpenDetails", "QvClose", "QvLoadFailed",
        "EmptyLibraryTitle", "EmptyLibraryText", "NoMatchTitle", "NoMatchText", "ClearFilters", "CounterUnavailable",
        "ShowInactive", "LiveOnly", "BreadcrumbCrm"
    ];

    /// <summary>Keys added by WP-CL-FE-2 (coverage matrix, closure / reopen, reason labels by value_code, error codes);
    /// no key echo allowed in any language.</summary>
    public static readonly IReadOnlyList<string> CoverageKeys =
    [
        "TabList", "TabCoverage", "CoverageDescription", "ColumnSummary", "RowSubline", "CellExpiringNote",
        "CellCoreChangedNote", "CellEvidenceChangedNote", "ActionOpenCountryVersion", "ActionMarkNotOpened",
        "ActionReopen", "ActionEditVersion", "CoreNotApprovedTooltip", "CellActionsLabel", "CloseModalTitle",
        "CloseReasonLabel", "CloseConfirm", "ReopenModalTitle", "ReopenNoteLabel", "ToastClosed", "ToastReopened",
        "CoverageEmptyTitle", "CoverageEmptyText", "LegendTitle", "SearchPlaceholder",
        "Reason_no-license", "Reason_regulation-disallows", "Reason_business-decision",
        "Err_country_has_version", "Err_not_applicable", "Err_reference_set_missing", "Err_country_already_closed",
        "Err_country_not_closed", "Err_invalid_reference_value", "Err_required"
    ];

    /// <summary>Keys added by WP-KP-4: the quick-view usage list (contents · knowledge paths · journeys by country). The
    /// retired content set is no longer a usage type. No key echo allowed in any language.</summary>
    public static readonly IReadOnlyList<string> UsageKeys =
    [
        "QvUsage", "QvUsageNone", "QvUsageFailed", "UsageGlobal", "UsageVia", "UsageNeedsReview",
        "UsageType_content", "UsageType_knowledge-path", "UsageType_journey"
    ];

    /// <summary>Everything the list / coverage bridge serializes (pre-existing shared keys + WP-CL-FE-1 + WP-CL-FE-2 +
    /// WP-KP-4).</summary>
    public static readonly IReadOnlyList<string> Bridge =
    [
        "Actions", "Apply", "Cancel", "Status", "EditClaim", "ArchiveClaim", "ArchiveClaimConfirm", "RecordArchived",
        "ErrorState", "Filter", "Loading", "No", "Yes", "View", "ViewDetails", "Edit", "AreYouSure", "Search", "Export",
        "Reset", "ShowAll", "SaveView", "ColumnVisibility", "QuickView", "Active", "Passive", "Unknown", "BulkDelete",
        "BulkDeleteConfirm", "Qualifiers",
        .. NewKeys,
        .. CoverageKeys,
        .. UsageKeys
    ];
}
