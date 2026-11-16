using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// CT-D1 (a), WP-0174-P2 — a lot status change moves the lot's AVAILABLE stock out of / back into availability. Runs in the
/// SAME transaction as the lot change (status change, expiry sweeper): when the lot starts holding its stock (Draft /
/// Released → Blocked / Expired) every company's <c>inventory_sku_positions.AvailableOnHand</c> of the lot's SKU drops by
/// Σ OnHand of the lot's AVAILABLE balance rows in that company; when it stops (Blocked → Released) it rises by the same sum.
/// Blocked → Expired changes nothing (held both ways).
/// <list type="bullet">
/// <item>The balance rows are NOT touched — their stock status stays ("bloke lot ≠ bloke stok").</item>
/// <item>The balance read is the transaction's snapshot; a posting of the same lot writes the lot document
/// (<c>StockTouchSeq</c>) and the change replaces it, so the two conflict and the loser re-runs — the sum always matches
/// the committed rows.</item>
/// <item>Never refused: blocking is a quality action. A SKU whose reservations exceed what stays available is
/// over-reserved until stock arrives or a reservation is released; new reservations and other issues are refused by the
/// unchanged S2 guards meanwhile.</item>
/// </list>
/// </summary>
internal static class LotHeldStock
{
    public static async Task ApplyAsync(
        IMongoDatabase database, IClientSessionHandle session, Lot lot, LotStatus from, LotStatus to, DateTimeOffset now, CancellationToken ct)
    {
        // CT-Q16: the status change keeps the lot's stored retest hold — the one hold question asked for both ends
        await MoveAsync(database, session, lot, LotPostingRules.HoldsStock(from, lot.RetestHeldAtUtc), LotPostingRules.HoldsStock(to, lot.RetestHeldAtUtc), now, ct);
    }

    /// <summary>
    /// MOD-0175 Q4 (KQ-H, CT-Q16): the retest hold set (<paramref name="heldBefore"/> = null → <paramref name="heldAfter"/> = now) or
    /// cleared, on the lot's stored status — called in the SAME transaction that writes <see cref="Lot.RetestHeldAtUtc"/>.
    /// </summary>
    public static Task ApplyRetestAsync(
        IMongoDatabase database, IClientSessionHandle session, Lot lot, DateTimeOffset? heldBefore, DateTimeOffset? heldAfter, DateTimeOffset now, CancellationToken ct)
        => MoveAsync(database, session, lot, LotPostingRules.HoldsStock(lot.Status, heldBefore), LotPostingRules.HoldsStock(lot.Status, heldAfter), now, ct);

    private static async Task MoveAsync(
        IMongoDatabase database, IClientSessionHandle session, Lot lot, bool wasHeld, bool isHeld, DateTimeOffset now, CancellationToken ct)
    {
        if (wasHeld == isHeld)
        {
            return;
        }

        var bf = Builders<InventoryBalance>.Filter;
        var rows = await database.GetCollection<InventoryBalance>(InventoryCollections.Balances)
            .Find(session, bf.Eq(x => x.TenantId, lot.TenantId) & bf.Eq(x => x.IsDeleted, false)
                           & bf.Eq(x => x.LotId, lot.Id.ToString("D")) & bf.Eq(x => x.StockStatus, StockStatus.Available))
            .ToListAsync(ct);

        var positions = database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions);
        var pf = Builders<InventorySkuPosition>.Filter;
        var u = Builders<InventorySkuPosition>.Update;
        // MOD-0173-FU03 §8.2 yol 3–4 (CT-07, XMC-001 — owner permission Beste 2026-11-02): grouped per warehouse too, so every group
        // moves the lot's warehouse (W:) and warehouse + lot (L:) scope positions in the same direction as the SKU position.
        var scopePositions = database.GetCollection<InventoryReservationPosition>(InventoryCollections.ReservationPositions);
        var sf = Builders<InventoryReservationPosition>.Filter;
        var su = Builders<InventoryReservationPosition>.Update;
        foreach (var company in rows.GroupBy(r => (r.LegalEntityId, r.ItemId, r.SkuId, r.SkuLevel, r.WarehouseId)))
        {
            var quantity = company.Sum(r => r.OnHand);
            if (quantity == 0m)
            {
                continue;
            }

            await positions.UpdateOneAsync(
                session,
                pf.Eq(x => x.TenantId, lot.TenantId) & pf.Eq(x => x.LegalEntityId, company.Key.LegalEntityId) & pf.Eq(x => x.IsDeleted, false)
                & pf.Eq(x => x.ItemId, company.Key.ItemId) & pf.Eq(x => x.SkuId, company.Key.SkuId) & pf.Eq(x => x.SkuLevel, company.Key.SkuLevel),
                u.Combine(
                    u.Inc(x => x.AvailableOnHand, isHeld ? -quantity : quantity),
                    u.Inc(x => x.Version, 1),
                    u.Set(x => x.UpdatedAt, now),
                    u.SetOnInsert(x => x.Id, Guid.NewGuid()),
                    u.SetOnInsert(x => x.Reserved, 0m),
                    u.SetOnInsert(x => x.BaseUomId, company.First().BaseUomId),
                    u.SetOnInsert(x => x.CreatedAt, now),
                    u.SetOnInsert(x => x.DeletedAt, null)),
                new UpdateOptions { IsUpsert = true },
                ct);

            if (company.Key.WarehouseId is not { } warehouseId)
            {
                continue;
            }

            foreach (var (level, lotId) in new[] { (ReservationScopeLevel.Warehouse, Guid.Empty), (ReservationScopeLevel.Lot, lot.Id) })
            {
                await scopePositions.UpdateOneAsync(
                    session,
                    sf.Eq(x => x.TenantId, lot.TenantId) & sf.Eq(x => x.LegalEntityId, company.Key.LegalEntityId) & sf.Eq(x => x.IsDeleted, false)
                    & sf.Eq(x => x.ItemId, company.Key.ItemId) & sf.Eq(x => x.SkuId, company.Key.SkuId) & sf.Eq(x => x.SkuLevel, company.Key.SkuLevel)
                    & sf.Eq(x => x.Level, level) & sf.Eq(x => x.WarehouseId, warehouseId) & sf.Eq(x => x.LotId, lotId),
                    su.Combine(
                        su.Inc(x => x.AvailableOnHand, isHeld ? -quantity : quantity),
                        su.Inc(x => x.Version, 1),
                        su.Set(x => x.UpdatedAt, now),
                        su.SetOnInsert(x => x.Id, Guid.NewGuid()),
                        su.SetOnInsert(x => x.Reserved, 0m),
                        su.SetOnInsert(x => x.BaseUomId, company.First().BaseUomId),
                        su.SetOnInsert(x => x.CreatedAt, now),
                        su.SetOnInsert(x => x.DeletedAt, null)),
                    new UpdateOptions { IsUpsert = true },
                    ct);
            }
        }
    }
}
