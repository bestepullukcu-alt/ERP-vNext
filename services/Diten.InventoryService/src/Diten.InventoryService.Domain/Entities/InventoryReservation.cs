using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// A physical hold of AVAILABLE quantity at SKU level, or (MOD-0173-FU03) at SKU + warehouse or SKU + warehouse + lot level (collection <c>inventory_reservations</c>, pack ASSUMPTION-0173-12..17).
/// Unlike ledger transactions it is a stateful document: ACTIVE → RELEASED (explicit release or full consumption) or
/// EXPIRED. <see cref="EntityBase.Version"/> is the optimistic-concurrency counter behind the ETag <c>W/"&lt;version&gt;"</c>.
/// Quantities are in the SKU's base UoM (the frozen request carries no UoM — ASSUMPTION-S2-01).
/// </summary>
public sealed class InventoryReservation : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    /// <summary>
    /// MOD-0173-FU03 §4.1: the LOCATION warehouse node the hold is limited to (Active, Warehouse type, the caller's company);
    /// null = SKU level. Never changes after creation.
    /// </summary>
    public string? WarehouseId { get; set; }

    /// <summary>MOD-0173-FU03 §4.1: the registered lot (<c>Lot.Id</c>) the hold is limited to; needs <see cref="WarehouseId"/>. Never changes.</summary>
    public Guid? LotId { get; set; }

    /// <summary>The quantity reserved at creation.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The quantity still held; counts as reserved only while <see cref="Status"/> is ACTIVE.</summary>
    public decimal RemainingQuantity { get; set; }

    public ReservationStatus Status { get; set; }

    public string ReservationType { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    public string? SourceDocumentId { get; set; }

    public string? SourceLineId { get; set; }

    public int? Priority { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>MOD-0173-FU03 §4.1: one of <see cref="Enums.ReservationClosedReasons"/> once the reservation is no longer ACTIVE; null while ACTIVE.</summary>
    public string? ClosedReason { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>SKU <c>AvailableOnHand − Reserved</c> right after creation — replayed verbatim on an idempotent retry.</summary>
    public decimal CreatedAvailableAfter { get; set; }
}
