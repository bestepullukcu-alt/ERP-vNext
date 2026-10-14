using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Per-SKU projection (collection <c>inventory_sku_positions</c>, pack ASSUMPTION-0173-13): <see cref="AvailableOnHand"/>
/// = Σ OnHand of the SKU's AVAILABLE balance rows, <see cref="Reserved"/> = Σ remaining quantity of its ACTIVE
/// reservations. Maintained inside the same Mongo transaction as every ledger leg and reservation change, rebuildable
/// from the ledger + reservations at any time. It is NOT a second stock truth: it only lets the two guards
/// (reserve ≤ AvailableOnHand − Reserved; issue keeps AvailableOnHand − Reserved ≥ 0) run atomically.
/// </summary>
public sealed class InventorySkuPosition : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public decimal AvailableOnHand { get; set; }

    public decimal Reserved { get; set; }

    public string BaseUomId { get; set; } = string.Empty;
}
