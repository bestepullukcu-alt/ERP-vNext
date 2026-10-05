using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// The one typed-lot migration of a tenant (collection <c>trace_lot_migrations</c>, pack §4) and its
/// <see cref="ManualLotEntryClosed"/> flag. Written only by slice P4; P1 declares the record and its index.
/// </summary>
public sealed class ManualLotMigration : EntityBase
{
    public DateTimeOffset CompletedAt { get; set; }

    public Guid CompletedBy { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int TransactionsLinked { get; set; }

    public int LotsLinked { get; set; }

    public int LotsCreated { get; set; }

    public bool ManualLotEntryClosed { get; set; }
}
