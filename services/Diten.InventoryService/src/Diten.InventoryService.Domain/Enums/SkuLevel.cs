namespace Diten.InventoryService.Domain.Enums;

/// <summary>Model A SKU level (DEC-INV-17): all three levels can hold stock.</summary>
public enum SkuLevel
{
    Gsku,
    Lsku,
    FinishedGood,

    /// <summary>
    /// INVENTORY-BUNDLE 1.1.0 (WP-0173-PM2): a material item that is its own SKU (<c>skuId == itemId</c>, PRODUCT-MASTER 1.1).
    /// Known to the domain so <c>getStockPresence</c> can ask for it; <see cref="Rules.WireNames"/> deliberately does NOT map
    /// it yet, so every other operation (postMovement, reservations, reads, lots) keeps refusing <c>Item</c> exactly as in
    /// 1.0.2 until WP-0173-PM1 brings the PRODUCT-MASTER 1.1 consumer change.
    /// </summary>
    Item
}
