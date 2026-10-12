namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0176 permission keys — namespace <c>expiry.*</c>, owned by MOD-0176 (pack §14.1; PKS-001 §4 one owner per namespace). Tier-3
/// verbs registered by the pack (PKS-001 §5): <c>comment</c>, <c>review</c>, <c>resolve</c>, <c>close</c>, <c>manage</c>. All Tenant scope.
/// <see cref="F1"/> are the keys the F1 slice enforces; the rest are enforced from F2–F5.
/// </summary>
public static class ExpiryPermissions
{
    public const string DashboardRead = "expiry.dashboard.read";

    public const string FefoOrderRead = "expiry.fefo-order.read";

    public const string ExceptionsRead = "expiry.exceptions.read";

    public const string ExceptionsCreate = "expiry.exceptions.create";

    public const string ExceptionsComment = "expiry.exceptions.comment";

    public const string ExceptionsReview = "expiry.exceptions.review";

    public const string ExceptionsResolve = "expiry.exceptions.resolve";

    public const string ExceptionsClose = "expiry.exceptions.close";

    public const string PoliciesRead = "expiry.policies.read";

    public const string PoliciesManage = "expiry.policies.manage";

    public const string EvidenceExport = "expiry.evidence.export";

    public const string DateOverridesCreate = "expiry.date-overrides.create";

    /// <summary>Pack §14.2 — every key of the namespace.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        DashboardRead, FefoOrderRead, ExceptionsRead, ExceptionsCreate, ExceptionsComment, ExceptionsReview, ExceptionsResolve, ExceptionsClose,
        PoliciesRead, PoliciesManage, EvidenceExport, DateOverridesCreate
    ];

    /// <summary>WP-0176-F1 — the keys the F1 controller enforces (and the only ones its manifest declares).</summary>
    public static readonly IReadOnlyList<string> F1 =
    [
        PoliciesRead, PoliciesManage, ExceptionsRead, ExceptionsCreate, ExceptionsComment, ExceptionsReview, ExceptionsResolve, ExceptionsClose
    ];

    /// <summary>WP-0176-F2 — F1 + the API-only key of createDateOverride: the keys the controller enforces and the manifest declares.</summary>
    public static readonly IReadOnlyList<string> Implemented = [.. F1, DateOverridesCreate];
}
