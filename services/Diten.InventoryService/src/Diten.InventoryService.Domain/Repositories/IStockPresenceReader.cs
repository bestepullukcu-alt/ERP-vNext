using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// INVENTORY-BUNDLE 1.1.0 <c>getStockPresence</c> (WP-0173-PM2): does ANY legal entity of ONE tenant hold a non-zero
/// balance of this SKU in ANY stock status? Reads the balance projection (<c>inventory_balances</c>), the one row set that
/// carries every status — AVAILABLE, QUALITY_INSPECTION, QUARANTINE, BLOCKED, IN_TRANSIT, DAMAGED, EXPIRED — with reserved
/// stock inside its on-hand. Tenant-scoped, never company-scoped, never quantity-returning.
/// </summary>
public interface IStockPresenceReader
{
    Task<bool> HasNonZeroStockAsync(Guid tenantId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);
}
