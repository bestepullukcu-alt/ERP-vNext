using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// <c>getStockPresence</c> over the balance projection (WP-0173-PM2). Why this projection and not another:
/// <list type="bullet">
/// <item><c>inventory_balances</c> has ONE row per Tenant + LE + SKU + level + warehouse + location + lot + STATUS, written in
/// the posting transaction itself (<see cref="InventoryLedgerRepository"/>), and its <c>OnHand</c> is the full quantity of that
/// row in EVERY status — AVAILABLE, QUALITY_INSPECTION, QUARANTINE, BLOCKED, IN_TRANSIT, DAMAGED, EXPIRED (the status buckets
/// are derived from it, pack §4). Reserved stock is part of <c>OnHand</c> (a reservation holds stock, it does not move it).</item>
/// <item><c>inventory_sku_positions</c> is NOT enough: it carries only the AVAILABLE on-hand (minus held lots).</item>
/// <item>The ledger (<c>inventory_transactions</c>) would give the same answer as a sum, at the cost of reading every movement.</item>
/// </list>
/// The filter is the tenant, the SKU and its level, live rows only (<c>IsDeleted = false</c>: a typed-lot migration closes
/// its rows to zero), and <c>OnHand ≠ 0</c> — a negative balance is non-zero. Deliberately NO legal-entity filter: the
/// question is "any company of this tenant". One document at most is read (index <c>ix_tenant_sku_level</c>).
/// </summary>
public sealed class StockPresenceReader : IStockPresenceReader
{
    private readonly IMongoCollection<InventoryBalance> _balances;

    public StockPresenceReader(IMongoDatabase database)
        => _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);

    public async Task<bool> HasNonZeroStockAsync(Guid tenantId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryBalance>.Filter;
        var filter = f.Eq(x => x.TenantId, (Guid?)tenantId)
                     & f.Eq(x => x.SkuId, skuId)
                     & f.Eq(x => x.SkuLevel, skuLevel)
                     & f.Eq(x => x.IsDeleted, false)
                     & f.Ne(x => x.OnHand, 0m);
        var hit = await _balances.Find(filter)
            .Project(x => x.Id)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        return hit != Guid.Empty;
    }
}
