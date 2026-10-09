namespace Diten.InventoryService.Domain.Enums;

/// <summary>Model A SKU level (DEC-INV-17): all three levels can hold stock.</summary>
public enum SkuLevel
{
    Gsku,
    Lsku,
    FinishedGood,

    /// <summary>
    /// INVENTORY-BUNDLE 1.1.0: a material item that is its own SKU (<c>skuId == itemId</c>, PRODUCT-MASTER 1.1) — WP-0173-PM2 for
    /// <c>getStockPresence</c>, WP-0173-PM1 for every ledger operation (postMovement, availability, balance, transactions,
    /// reservations). TRACE-BUNDLE 1.2.0 and QUARANTINE-BUNDLE 1.0.0 have no <c>Item</c> level yet and keep refusing it
    /// (<see cref="Rules.WireNames.TryParseLegacySkuLevel"/>).
    /// </summary>
    Item
}
