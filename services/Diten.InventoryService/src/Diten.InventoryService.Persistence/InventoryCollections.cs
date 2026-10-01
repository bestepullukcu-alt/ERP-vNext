namespace Diten.InventoryService.Persistence;

/// <summary>
/// Every collection MOD-0173 owns. Stock quantities live only in the append-only ledger and its projections
/// (balance rows; S2 SKU positions); reservations hold quantities of that same stock, they are not stock
/// (single stock SoR — pack §2, AC14). An architecture test pins this list.
/// </summary>
public static class InventoryCollections
{
    /// <summary>Append-only movement facts (pack §4).</summary>
    public const string Transactions = "inventory_transactions";

    /// <summary>Balance projection — the ONLY balance in ERP-vNext (DEC-INV-02/07/15).</summary>
    public const string Balances = "inventory_balances";

    public const string Audit = "inventory_audit";

    public const string Outbox = "inventory_outbox";

    /// <summary>Atomic per Tenant + LE + year counter behind <c>INV-{yyyy}-{0000000}</c>; holds no quantity.</summary>
    public const string Sequences = "inventory_sequences";

    /// <summary>Reservations (S2): stateful physical holds at SKU level (pack ASSUMPTION-0173-12); quantities are holds, not stock.</summary>
    public const string Reservations = "inventory_reservations";

    /// <summary>Per-SKU projection of the ledger + reservations (pack ASSUMPTION-0173-13); rebuildable, not a second truth.</summary>
    public const string SkuPositions = "inventory_sku_positions";

    /// <summary>MOD-0173-FU02: the place hierarchy (Site → … → Bin); holds no quantity — stock presence is read from the ledger.</summary>
    public const string Locations = "inventory_locations";

    /// <summary>MOD-0173-FU02 K-11: append-only location change history.</summary>
    public const string LocationHistory = "inventory_location_history";

    public static readonly IReadOnlyList<string> All =
        [Transactions, Balances, Audit, Outbox, Sequences, Reservations, SkuPositions, Locations, LocationHistory];
}
