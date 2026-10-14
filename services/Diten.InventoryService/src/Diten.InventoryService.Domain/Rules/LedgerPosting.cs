using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Everything one posting writes, committed all-or-nothing (pack §8 consistency): the transaction, its balance
/// legs, the audit record and the Pending outbox event. The repository assigns the transaction number and stamps
/// tenant / legal entity on every document. <see cref="Lot"/> (WP-0174-P2) carries the serial decisions of the lot / serial
/// pre-check; null = no serial lifecycle (every caller before P2).
/// </summary>
public sealed record LedgerPosting(
    InventoryTransaction Transaction,
    IReadOnlyList<LedgerLeg> Legs,
    InventoryAuditRecord Audit,
    InventoryOutboxMessage Outbox,
    LotPostingContext? Lot = null);
