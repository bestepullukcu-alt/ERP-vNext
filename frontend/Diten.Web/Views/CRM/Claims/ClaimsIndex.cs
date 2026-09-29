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

    /// <summary>Everything the list bridge serializes (pre-existing shared keys + the WP-CL-FE-1 keys).</summary>
    public static readonly IReadOnlyList<string> Bridge =
    [
        "Actions", "Apply", "Cancel", "Status", "EditClaim", "ArchiveClaim", "ArchiveClaimConfirm", "RecordArchived",
        "ErrorState", "Filter", "Loading", "No", "Yes", "View", "ViewDetails", "Edit", "AreYouSure", "Search", "Export",
        "Reset", "ShowAll", "SaveView", "ColumnVisibility", "QuickView", "Active", "Passive", "Unknown", "BulkDelete",
        "BulkDeleteConfirm", "Qualifiers",
        .. NewKeys
    ];
}
