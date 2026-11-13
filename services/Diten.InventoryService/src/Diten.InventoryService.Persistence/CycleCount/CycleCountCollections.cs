namespace Diten.InventoryService.Persistence.CycleCounting;

/// <summary>
/// MOD-0181 collections in the InventoryService database (pack §3, §4.6; prefix <c>wms_count_</c>). A class of its own — never MOD-0178's
/// <c>WarehouseCollections</c> (Q-22: the count is 0181's own record, <c>wms_tasks</c> is not used). None is stock: no document carries an
/// OnHand / Available / Reserved total (pack §2.3 rule 1) — the stock is the MOD-0173 ledger's. DL-1 collections; DL-3 adds the settings,
/// DL-5 the evidence exports.
/// </summary>
public static class CycleCountCollections
{
    /// <summary>The counts (mutable head, Version CAS; a Draft cancel is a soft delete).</summary>
    public const string Counts = "wms_count_counts";

    /// <summary>The count lines (the snapshot's expected quantity is evidence written once, never a balance).</summary>
    public const string Lines = "wms_count_lines";

    /// <summary>The insert-only count timeline (the local GxP trail, pack §8.11).</summary>
    public const string Events = "wms_count_events";

    /// <summary>The bin claims of the open counts (F-6; a partial unique index, never deleted).</summary>
    public const string BinClaims = "wms_count_bin_claims";

    /// <summary>DL-3 (pack §4.4, Q-3): one settings record per company — tolerance, recount limit, line limit. Not stock.</summary>
    public const string Settings = "wms_count_settings";

    public static readonly IReadOnlyList<string> All = [Counts, Lines, Events, BinClaims, Settings];

    /// <summary>DL-5 (pack §4.5): the insert-only Evidence Pack export log (the MOD-0176 expiry_evidence_exports pattern). Not stock.</summary>
    public const string EvidenceExports = "wms_count_evidence_exports";

    /// <summary>DL-5: the stored bytes of each export (insert-only; what was hashed is what is downloaded).</summary>
    public const string EvidenceFiles = "wms_count_evidence_files";

    /// <summary>DL-5's own list (pack §24 DL-5 seam: "kanıt koleksiyonları ayrı liste") — beside <see cref="All"/>, never in it.</summary>
    public static readonly IReadOnlyList<string> Evidence = [EvidenceExports, EvidenceFiles];
}
