using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>Who / what / when / correlation of one reservation change, written to <c>inventory_audit</c> atomically with it.</summary>
public sealed class InventoryReservationAuditRecord : EntityBase
{
    /// <summary>InventoryReservationCreated · …Released · …Expired · …Consumed · …PartiallyConsumed.</summary>
    public string Action { get; set; } = string.Empty;

    public Guid ReservationId { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public decimal ChangedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public ReservationStatus Status { get; set; }

    /// <summary>JWT <c>sub</c>; <see cref="Guid.Empty"/> for the expiry sweeper (system actor).</summary>
    public Guid ActorUserId { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? TransactionId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
