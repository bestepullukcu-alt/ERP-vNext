using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// A typed-lot ledger transaction → its registered lot (collection <c>trace_movement_lot_links</c>, pack §4). Written
/// only by the typed-lot migration (slice P4); P1 declares the record and its indexes, nothing writes it yet.
/// </summary>
public sealed class MovementLotLink : EntityBase
{
    public string TransactionId { get; set; } = string.Empty;

    public string TypedText { get; set; } = string.Empty;

    public Guid LotId { get; set; }

    public Guid MigrationId { get; set; }
}
