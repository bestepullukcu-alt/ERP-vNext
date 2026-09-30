using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Everything one posting writes, committed all-or-nothing (pack §8 consistency): the transaction, its balance
/// legs, the audit record and the Pending outbox event. The repository assigns the transaction number and stamps
/// tenant / legal entity on every document.
/// </summary>
public sealed record LedgerPosting(
    InventoryTransaction Transaction,
    IReadOnlyList<LedgerLeg> Legs,
    InventoryAuditRecord Audit,
    InventoryOutboxMessage Outbox);
