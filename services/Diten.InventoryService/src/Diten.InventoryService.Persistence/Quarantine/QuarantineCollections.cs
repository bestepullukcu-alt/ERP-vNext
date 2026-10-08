namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// MOD-0175 collections in the InventoryService database (pack §3, prefix <c>quarantine_</c>). None is stock: a hold /
/// blocked-stock quantity is the quantity the record is about; the stock itself is only the MOD-0173 ledger. Slice Q2 adds
/// the workflow event inbox (<c>quarantine_event_inbox</c>, insert-only, <c>_id</c> = EventId).
/// </summary>
public static class QuarantineCollections
{
    public const string Holds = "quarantine_holds";

    /// <summary>Insert-only (pack §3).</summary>
    public const string Decisions = "quarantine_decisions";

    /// <summary>Insert-only.</summary>
    public const string DecisionEvents = "quarantine_decision_events";

    public const string BlockedStock = "quarantine_blocked_stock";

    /// <summary>Insert-only.</summary>
    public const string History = "quarantine_history";

    public const string Settings = "quarantine_settings";

    public const string BlockReasons = "quarantine_block_reasons";

    /// <summary>Atomic per Tenant + prefix + year counter behind <c>KRN-</c> / <c>QKR-</c> / <c>BLK-{yyyy}-{nnnn}</c>.</summary>
    public const string Sequences = "quarantine_sequences";

    /// <summary>MOD-0023 completion events already processed (Q2); insert-only.</summary>
    public const string EventInbox = "quarantine_event_inbox";

    public static readonly IReadOnlyList<string> All =
        [Holds, Decisions, DecisionEvents, BlockedStock, History, Settings, BlockReasons, Sequences, EventInbox];
}
