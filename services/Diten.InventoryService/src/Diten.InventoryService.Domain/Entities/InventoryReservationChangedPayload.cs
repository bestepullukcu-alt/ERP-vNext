using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Internal payload of <c>InventoryReservationChanged</c> (not part of the frozen bundle — like ASSUMPTION-0173-08,
/// consumers must not depend on it until an additive contract publishes it).
/// </summary>
public sealed class InventoryReservationChangedPayload
{
    public Guid ReservationId { get; set; }

    /// <summary>Created · Released · Expired · Consumed · PartiallyConsumed · Restored (MOD-0173-FU03 §8.3.1; internal payload, not contract).</summary>
    public string Change { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    /// <summary>MOD-0173-FU03: the reservation's warehouse scope (null at SKU level).</summary>
    public string? WarehouseId { get; set; }

    /// <summary>MOD-0173-FU03: the reservation's lot scope (null unless lot level).</summary>
    public Guid? LotId { get; set; }

    /// <summary>MOD-0173-FU03: <see cref="Enums.ReservationClosedReasons"/> once closed.</summary>
    public string? ClosedReason { get; set; }

    /// <summary>The quantity this change added to (Created) or removed from (others) the reserved total.</summary>
    public decimal ChangedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public ReservationStatus Status { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public string? SourceDocumentId { get; set; }

    public string? SourceLineId { get; set; }

    /// <summary>The consuming ledger transaction, for Consumed / PartiallyConsumed.</summary>
    public Guid? TransactionId { get; set; }
}
