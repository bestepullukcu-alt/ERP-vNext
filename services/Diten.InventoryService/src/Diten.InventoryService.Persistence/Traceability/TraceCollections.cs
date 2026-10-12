namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// MOD-0174 collections in the InventoryService database (pack §3/§4, ASSUMPTION-0174-02: prefix <c>trace_</c>). None of
/// them carries a stock quantity — stock stays the MOD-0173 ledger's single truth. The one quantity-carrying collection of
/// MOD-0174, <see cref="LotPositions"/>, keeps the ledger prefix because the LEDGER posting code writes it (K-A, pack §8): a
/// projection of the ledger transactions like <c>inventory_sku_positions</c>, listed apart in <see cref="LedgerProjections"/>.
/// </summary>
public static class TraceCollections
{
    public const string Lots = "trace_lots";

    public const string LotHistory = "trace_lot_history";

    public const string Serials = "trace_serials";

    public const string GenealogyLinks = "trace_genealogy_links";

    /// <summary>Slice P4 (typed-lot migration) — declared with its indexes in P1, written only by P4.</summary>
    public const string MovementLotLinks = "trace_movement_lot_links";

    /// <summary>Slice P4 — declared with its index in P1, written only by P4.</summary>
    public const string LotMigrations = "trace_lot_migrations";

    public static readonly IReadOnlyList<string> All = [Lots, LotHistory, Serials, GenealogyLinks, MovementLotLinks, LotMigrations];

    /// <summary>WP-0174-P2, K-A — per-lot OnHand per company, written only inside the ledger posting transaction.</summary>
    public const string LotPositions = "inventory_lot_positions";

    public static readonly IReadOnlyList<string> LedgerProjections = [LotPositions];
}
