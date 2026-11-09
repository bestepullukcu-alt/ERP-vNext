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

    public static readonly IReadOnlyList<string> All = [Counts, Lines, Events, BinClaims];
}
