using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// MOD-0173-FU03 §4.2 — the warehouse and warehouse + lot levels of the reservation projection (collection
/// <c>inventory_reservation_positions</c>), beside the SKU level of <see cref="InventorySkuPosition"/>:
/// <see cref="AvailableOnHand"/> = Σ OnHand of the scope's AVAILABLE balance rows (held lots excluded — the SKU rule),
/// <see cref="Reserved"/> = Σ remaining quantity of the ACTIVE reservations that contain the scope (warehouse level = the
/// warehouse's warehouse and lot reservations; lot level = that lot's only). Written in the same Mongo transaction as every
/// ledger leg and reservation change, rebuilt at startup; a projection, never a second stock truth.
/// </summary>
public sealed class InventoryReservationPosition : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    /// <summary><see cref="ReservationScopeLevel.Warehouse"/> or <see cref="ReservationScopeLevel.Lot"/>.</summary>
    public ReservationScopeLevel Level { get; set; }

    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>The lot at <see cref="ReservationScopeLevel.Lot"/>; <see cref="Guid.Empty"/> at the warehouse level (never null — the unique index compares it).</summary>
    public Guid LotId { get; set; }

    public decimal AvailableOnHand { get; set; }

    public decimal Reserved { get; set; }

    public string BaseUomId { get; set; } = string.Empty;
}
