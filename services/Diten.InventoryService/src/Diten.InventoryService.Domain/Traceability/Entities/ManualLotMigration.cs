using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// The one typed-lot migration of a tenant (collection <c>trace_lot_migrations</c>, pack §4) and its
/// <see cref="ManualLotEntryClosed"/> flag. P1 declared the record and its unique index (one per tenant); slice P4 writes it.
/// <para>
/// WP-0174-P4 (CT K4-2) — the same document is also the tenant's typed-lot GATE: a ledger posting that carries a typed
/// (unregistered) lot text increments <see cref="GateSeq"/> inside its posting transaction (upsert — the document may exist
/// before any migration, then with <see cref="ManualLotEntryClosed"/> = false and no completion fields), and the migration
/// commit writes the same document in its own transaction. The two conflict in Mongo and the loser re-runs on the committed
/// state (the FU02 / P2 <c>StockTouchSeq</c> technique): a typed inbound posting can never land after the commit closed
/// typing, and a commit can never miss a typed posting that committed before it (it re-reads the groups → stale).
/// </para>
/// </summary>
public sealed class ManualLotMigration : EntityBase
{
    /// <summary>Set by the commit; null while the document is only the gate.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CompletedBy { get; set; }

    public string? Reason { get; set; }

    public int TransactionsLinked { get; set; }

    public int LotsLinked { get; set; }

    public int LotsCreated { get; set; }

    public bool ManualLotEntryClosed { get; set; }

    /// <summary>Written by every typed-lot posting and by the commit (the conflict that serialises them).</summary>
    public long GateSeq { get; set; }

    /// <summary>The commit's Idempotency-Key (replay 200 / reuse 400, MOD-0173 rules).</summary>
    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }
}
