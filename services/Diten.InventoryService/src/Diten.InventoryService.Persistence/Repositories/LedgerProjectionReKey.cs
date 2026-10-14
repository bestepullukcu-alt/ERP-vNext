using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>One typed group of the commit: its rows move from the typed text to the registered lot.</summary>
/// <param name="LotHoldsStock">The registered lot holds its stock (<c>LotPostingRules.HoldsStock(Lot)</c>: Blocked / Expired / stored
/// retest hold, CT-D1 (a), CT-Q16) — its AVAILABLE stock leaves availability.</param>
public sealed record LedgerReKeyGroup(Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string TypedText, Guid LotId, bool LotHoldsStock);

/// <summary>What one re-key moved: rows closed, rows written, Σ OnHand per company (base unit).</summary>
public sealed record LedgerReKeyResult(int RowsClosed, int RowsWritten, IReadOnlyDictionary<Guid, decimal> OnHandByCompany);

/// <summary>
/// MOD-0174 WP-0174-P4 — the ledger side of the typed-lot migration (pack §8 "Migration re-key", CT-D6, DEC-INV-02): the
/// projections of a typed lot text are rebuilt under the registered lot id, INSIDE the migration commit's transaction
/// (the caller's session). Owned by the ledger (same folder as <see cref="InventoryLedgerRepository"/> and
/// <see cref="SkuPositionRebuilder"/>):
/// <list type="bullet">
/// <item><c>inventory_balances</c>: every row of the tenant (every company) whose dimension carries the typed text moves to
/// the same dimension with the registered <c>LotId</c> — upsert + <c>$inc</c> of every quantity bucket, so several typed
/// texts linked to one lot (or a row that already exists under it) MERGE and the totals stay equal; the typed row is closed
/// (quantities 0, <c>IsDeleted</c>) and is never read again by a scoped query.</item>
/// <item><c>inventory_lot_positions</c> (K-A): + Σ moved OnHand (all statuses) per company and lot.</item>
/// <item><c>inventory_sku_positions</c> (CT-D1 (a)): a typed text was never held, so its AVAILABLE stock is in the SKU
/// position; linked to a Blocked / Expired lot it leaves it (− Σ moved AVAILABLE OnHand per company). A Draft / Released
/// target changes nothing there.</item>
/// </list>
/// <b>No <c>inventory_transactions</c> document is read for writing, updated, replaced or deleted here</b> — the ledger
/// stays append-only; the link from a transaction to its lot is the migration's <c>trace_movement_lot_links</c>.
/// A posting that touches one of these rows concurrently writes the same documents → WriteConflict → the loser re-runs.
/// </summary>
public static class LedgerProjectionReKey
{
    public static async Task<LedgerReKeyResult> ApplyAsync(
        IMongoDatabase database, IClientSessionHandle session, Guid tenantId, IReadOnlyList<LedgerReKeyGroup> groups, DateTimeOffset now, CancellationToken ct)
    {
        var balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        var lotPositions = database.GetCollection<LotPosition>(TraceCollections.LotPositions);
        var skuPositions = database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions);
        var bf = Builders<InventoryBalance>.Filter;
        var bu = Builders<InventoryBalance>.Update;

        var closed = 0;
        var written = 0;
        var onHandByCompany = new Dictionary<Guid, decimal>();
        foreach (var group in groups)
        {
            var rows = await balances.Find(session,
                    bf.Eq(x => x.TenantId, (Guid?)tenantId) & bf.Eq(x => x.IsDeleted, false)
                    & bf.Eq(x => x.ItemId, group.ItemId) & bf.Eq(x => x.SkuId, group.SkuId) & bf.Eq(x => x.SkuLevel, group.SkuLevel)
                    & bf.Eq(x => x.LotId, group.TypedText))
                .ToListAsync(ct);
            var lotId = group.LotId.ToString("D");

            foreach (var row in rows)
            {
                var target = bf.Eq(x => x.TenantId, row.TenantId) & bf.Eq(x => x.LegalEntityId, row.LegalEntityId) & bf.Eq(x => x.IsDeleted, false)
                             & bf.Eq(x => x.ItemId, row.ItemId) & bf.Eq(x => x.SkuId, row.SkuId) & bf.Eq(x => x.SkuLevel, row.SkuLevel)
                             & bf.Eq(x => x.WarehouseId, row.WarehouseId) & bf.Eq(x => x.LocationId, row.LocationId) & bf.Eq(x => x.LotId, lotId)
                             & bf.Eq(x => x.StockStatus, row.StockStatus) & bf.Eq(x => x.OwnerId, row.OwnerId) & bf.Eq(x => x.ProjectId, row.ProjectId);
                await balances.UpdateOneAsync(session, target,
                    bu.Combine(
                        bu.Inc(x => x.OnHand, row.OnHand),
                        bu.Inc(x => x.Reserved, row.Reserved),
                        bu.Inc(x => x.Available, row.Available),
                        bu.Inc(x => x.InTransit, row.InTransit),
                        bu.Inc(x => x.Blocked, row.Blocked),
                        bu.Inc(x => x.QualityInspection, row.QualityInspection),
                        bu.Inc(x => x.Version, 1),
                        bu.Max(x => x.LastPostingDate, row.LastPostingDate),
                        bu.Set(x => x.UpdatedAt, now),
                        bu.SetOnInsert(x => x.Id, Guid.NewGuid()),
                        bu.SetOnInsert(x => x.BaseUomId, row.BaseUomId),
                        bu.SetOnInsert(x => x.LastMovementId, row.LastMovementId),
                        bu.SetOnInsert(x => x.CreatedAt, now),
                        bu.SetOnInsert(x => x.DeletedAt, null)),
                    new UpdateOptions { IsUpsert = true }, ct);
                written++;

                var close = await balances.UpdateOneAsync(session,
                    bf.Eq(x => x.Id, row.Id) & bf.Eq(x => x.Version, row.Version) & bf.Eq(x => x.IsDeleted, false),
                    bu.Combine(
                        bu.Set(x => x.OnHand, 0m), bu.Set(x => x.Reserved, 0m), bu.Set(x => x.Available, 0m),
                        bu.Set(x => x.InTransit, 0m), bu.Set(x => x.Blocked, 0m), bu.Set(x => x.QualityInspection, 0m),
                        bu.Set(x => x.IsDeleted, true), bu.Set(x => x.DeletedAt, now), bu.Set(x => x.UpdatedAt, now),
                        bu.Inc(x => x.Version, 1)),
                    cancellationToken: ct);
                if (close.ModifiedCount != 1)
                {
                    throw new InvalidOperationException("A typed-lot balance row changed inside the migration transaction.");
                }

                closed++;
            }

            foreach (var company in rows.GroupBy(r => r.LegalEntityId!.Value))
            {
                var onHand = company.Sum(r => r.OnHand);
                onHandByCompany[company.Key] = onHandByCompany.GetValueOrDefault(company.Key) + onHand;
                if (onHand != 0m)
                {
                    var lf = Builders<LotPosition>.Filter;
                    var lu = Builders<LotPosition>.Update;
                    await lotPositions.UpdateOneAsync(session,
                        lf.Eq(x => x.TenantId, (Guid?)tenantId) & lf.Eq(x => x.LegalEntityId, (Guid?)company.Key)
                        & lf.Eq(x => x.IsDeleted, false) & lf.Eq(x => x.LotId, group.LotId),
                        lu.Combine(
                            lu.Inc(x => x.OnHand, onHand),
                            lu.Inc(x => x.Version, 1),
                            lu.Set(x => x.UpdatedAt, now),
                            lu.SetOnInsert(x => x.Id, Guid.NewGuid()),
                            lu.SetOnInsert(x => x.BaseUomId, company.First().BaseUomId),
                            lu.SetOnInsert(x => x.CreatedAt, now),
                            lu.SetOnInsert(x => x.DeletedAt, null)),
                        new UpdateOptions { IsUpsert = true }, ct);
                }

                var available = company.Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand);
                if (group.LotHoldsStock && available != 0m)
                {
                    var pf = Builders<InventorySkuPosition>.Filter;
                    var pu = Builders<InventorySkuPosition>.Update;
                    await skuPositions.UpdateOneAsync(session,
                        pf.Eq(x => x.TenantId, (Guid?)tenantId) & pf.Eq(x => x.LegalEntityId, (Guid?)company.Key) & pf.Eq(x => x.IsDeleted, false)
                        & pf.Eq(x => x.ItemId, group.ItemId) & pf.Eq(x => x.SkuId, group.SkuId) & pf.Eq(x => x.SkuLevel, group.SkuLevel),
                        pu.Combine(
                            pu.Inc(x => x.AvailableOnHand, -available),
                            pu.Inc(x => x.Version, 1),
                            pu.Set(x => x.UpdatedAt, now),
                            pu.SetOnInsert(x => x.Id, Guid.NewGuid()),
                            pu.SetOnInsert(x => x.Reserved, 0m),
                            pu.SetOnInsert(x => x.BaseUomId, company.First().BaseUomId),
                            pu.SetOnInsert(x => x.CreatedAt, now),
                            pu.SetOnInsert(x => x.DeletedAt, null)),
                        new UpdateOptions { IsUpsert = true }, ct);
                }
            }
        }

        return new LedgerReKeyResult(closed, written, onHandByCompany);
    }
}
