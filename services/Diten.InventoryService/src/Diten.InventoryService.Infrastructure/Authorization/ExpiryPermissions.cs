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

    /// <summary>
    /// The keys the manifest DECLARES, one entry per declaration (WP-0176-F4: a page key repeats per page, a button key per button) — the
    /// manifest namespace test compares the manifest with this list as a multiset; <c>Implemented.Distinct()</c> = the keys the
    /// controllers enforce. F1 keys and F2 <c>date-overrides.create</c> (API) + the F4 pages (3 × dashboard.read, fefo-order.read) and
    /// buttons (3 × exceptions.create "İstisna aç", 2 × fefo-order.read "FEFO sırasını gör", 2 × dashboard.read CSV, fefo-order.read
    /// "Sırayı hesapla"). WP-0176-F5: the pages İstisna Kutusu + Detayı (2 × exceptions.read), Ayarlar (policies.read), Kanıt Paketi
    /// (evidence.export) and their buttons ("İstisna aç", "İncelemeye al", "Yorum ekle", "Karar ver", "İstisnayı kapat", "Kaydet",
    /// "CSV / PDF indir") replace the F1 API permissions; only <c>date-overrides.create</c> stays API (pack §14.4).
    /// </summary>
    public static readonly IReadOnlyList<string> Implemented =
    [
        DashboardRead, DashboardRead, DashboardRead, FefoOrderRead,                // F4 pages
        ExceptionsCreate, ExceptionsCreate, ExceptionsCreate,                      // "İstisna aç"
        FefoOrderRead, FefoOrderRead, DashboardRead, DashboardRead, FefoOrderRead, // view order ×2, CSV ×2, calculate
        ExceptionsRead, ExceptionsRead, PoliciesRead, EvidenceExport,              // F5 pages (inbox, detail, settings, evidence)
        ExceptionsCreate,                                                          // inbox "İstisna aç"
        ExceptionsReview, ExceptionsComment, ExceptionsResolve, ExceptionsClose,   // detail buttons
        PoliciesManage,                                                            // settings "Kaydet"
        EvidenceExport,                                                            // "CSV / PDF indir"
        DateOverridesCreate                                                        // API by design (F2)
    ];
}
