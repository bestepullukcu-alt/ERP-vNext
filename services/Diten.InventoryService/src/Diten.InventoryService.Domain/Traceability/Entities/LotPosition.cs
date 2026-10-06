using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// K-A per-lot stock (collection <c>inventory_lot_positions</c>, pack §8): Tenant + LegalEntity + <see cref="LotId"/> →
/// <see cref="OnHand"/> in the base UoM over ALL stock statuses. A projection of the same ledger transactions as the balance
/// rows, written ONLY by the ledger posting code in the posting transaction (never computed per request, never edited).
/// It is not a second stock truth: Σ balance OnHand of the lot in the company equals it at every commit (pack §16 P2).
/// Kept for registered lots only — a typed lot text has no position until the P4 migration re-keys it.
/// </summary>
public sealed class LotPosition : EntityBase
{
    public Guid LotId { get; set; }

    public decimal OnHand { get; set; }

    public string BaseUomId { get; set; } = string.Empty;
}
