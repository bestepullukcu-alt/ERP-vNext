namespace Diten.InventoryService.Persistence;

/// <summary>
/// Every collection MOD-0173 owns. Exactly two hold quantities: the append-only ledger and its projection
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

    public static readonly IReadOnlyList<string> All = [Transactions, Balances, Audit, Outbox, Sequences];
}
