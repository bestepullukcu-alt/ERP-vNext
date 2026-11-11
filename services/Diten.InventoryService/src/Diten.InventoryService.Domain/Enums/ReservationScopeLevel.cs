namespace Diten.InventoryService.Domain.Enums;

/// <summary>
/// MOD-0173-FU03 §8.2 — the three reservation scope levels: SKU ⊇ SKU + warehouse ⊇ SKU + warehouse + lot. A reservation
/// holds its own level and every level that contains it; <see cref="Entities.InventoryReservationPosition"/> carries only
/// <see cref="Warehouse"/> and <see cref="Lot"/> (the SKU level stays in <see cref="Entities.InventorySkuPosition"/>).
/// </summary>
public enum ReservationScopeLevel
{
    Sku,
    Warehouse,
    Lot
}
