using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// Rebuilds <c>inventory_sku_positions</c> from its sources (pack ASSUMPTION-0173-13): <c>AvailableOnHand</c> = Σ OnHand
/// of the SKU's AVAILABLE balance rows, <c>Reserved</c> = Σ remaining quantity of its ACTIVE reservations.
/// <list type="bullet">
/// <item>Idempotent: running it twice writes the same numbers. Never deletes a document.</item>
/// <item>Each SKU is recomputed and written in its OWN transaction (snapshot read + write of the position). A posting
/// that changes the SKU concurrently also writes the position, so one of the two gets a WriteConflict and is retried
/// on the committed state — a concurrent movement cannot be overwritten by a stale sum.</item>
/// <item>Groups by Tenant + LE + SKU; it never mixes tenants (a system job, not a tenant query).</item>
/// </list>
/// Runs at startup before the host serves requests (S1 ledgers that predate S2 get their positions), and is the
/// oracle of the AC-R9 invariant test.
/// <para>MOD-0174 CT-D1 (a), WP-0174-P2: the AVAILABLE rows of a Blocked / Expired registered lot are not counted — the same
/// rule the posting and the lot status change maintain, so a restart never brings a blocked lot's stock back into
/// availability. The tenant's lots are read in the same transaction (a status change writes the position too → conflict).</para>
/// <para>MOD-0173-FU03 §8.2 (yol 6): the same step rebuilds the SKU's warehouse and warehouse + lot positions
/// (<c>inventory_reservation_positions</c>) from the same rows and reservations — per warehouse Σ OnHand of its AVAILABLE rows and
/// Σ remaining of the ACTIVE reservations of that warehouse (warehouse and lot scope); per warehouse + registered lot the same for the
/// lot. A scope position no source names any more is set to 0, never deleted.</para>
/// </summary>
public sealed class SkuPositionRebuilder : ISkuPositionRebuilder
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly IMongoCollection<InventorySkuPosition> _positions;
    private readonly IMongoCollection<InventoryReservationPosition> _scopePositions;
    private readonly IMongoCollection<Lot> _lots;

    public SkuPositionRebuilder(IMongoClient client, IMongoDatabase database)
    {
        _client = client;
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
        _positions = database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions);
        _scopePositions = database.GetCollection<InventoryReservationPosition>(InventoryCollections.ReservationPositions);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
    }

    public async Task<int> RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var keys = new HashSet<(string Tenant, string Le, string Item, string Sku, string Level)>();
        var groupId = new BsonDocument("_id", new BsonDocument
        {
            { "t", "$TenantId" }, { "le", "$LegalEntityId" }, { "item", "$ItemId" }, { "sku", "$SkuId" }, { "level", "$SkuLevel" }
        });

        LastSkippedUnscoped = 0;
        var groups = new List<BsonDocument>();
        groups.AddRange(await _balances.Aggregate()
            .Match(Builders<InventoryBalance>.Filter.Eq(x => x.StockStatus, StockStatus.Available)
                   & Builders<InventoryBalance>.Filter.Eq(x => x.IsDeleted, false))
            .Group(groupId).ToListAsync(cancellationToken));
        groups.AddRange(await _reservations.Aggregate()
            .Match(Builders<InventoryReservation>.Filter.Eq(x => x.Status, ReservationStatus.Active))
            .Group(groupId).ToListAsync(cancellationToken));
        groups.AddRange(await _positions.Aggregate().Group(groupId).ToListAsync(cancellationToken));
        groups.AddRange(await _scopePositions.Aggregate().Group(groupId).ToListAsync(cancellationToken));
        foreach (var g in groups)
        {
            if (TryKey(g, out var key))
            {
                keys.Add(key);
            }
        }

        foreach (var key in keys)
        {
            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            await session.WithTransactionAsync(
                (s, ct) => RebuildOneAsync(s, key, ct),
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }

        return keys.Count;
    }

    private async Task<bool> RebuildOneAsync(IClientSessionHandle session, (string Tenant, string Le, string Item, string Sku, string Level) key, CancellationToken ct)
    {
        var tenant = (Guid?)Guid.Parse(key.Tenant);
        var le = (Guid?)Guid.Parse(key.Le);
        var item = Guid.Parse(key.Item);
        var skuId = Guid.Parse(key.Sku);
        var level = Enum.Parse<SkuLevel>(key.Level);

        var bf = Builders<InventoryBalance>.Filter;
        var rows = await _balances
            .Find(session, bf.Eq(x => x.TenantId, tenant) & bf.Eq(x => x.LegalEntityId, le) & bf.Eq(x => x.IsDeleted, false)
                           & bf.Eq(x => x.ItemId, item) & bf.Eq(x => x.SkuId, skuId) & bf.Eq(x => x.SkuLevel, level)
                           & bf.Eq(x => x.StockStatus, StockStatus.Available))
            .ToListAsync(ct);

        var lotIds = rows.Where(r => r.LotId is not null).Select(r => r.LotId!).Distinct(StringComparer.Ordinal)
            .Select(l => LotPostingRules.IsRegisteredLotForm(l, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).ToList();
        // CT-Q16: the one hold question (status or stored retest hold), asked of the stored lots
        var held = lotIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _lots.Find(session, Builders<Lot>.Filter.Eq(x => x.TenantId, tenant) & Builders<Lot>.Filter.In(x => x.Id, lotIds)).ToListAsync(ct))
              .Where(LotPostingRules.HoldsStock)
              .Select(l => l.Id.ToString("D")).ToHashSet(StringComparer.Ordinal);
        rows = rows.Where(r => r.LotId is null || !held.Contains(r.LotId)).ToList();

        var rf = Builders<InventoryReservation>.Filter;
        var active = await _reservations
            .Find(session, rf.Eq(x => x.TenantId, tenant) & rf.Eq(x => x.LegalEntityId, le) & rf.Eq(x => x.IsDeleted, false)
                           & rf.Eq(x => x.ItemId, item) & rf.Eq(x => x.SkuId, skuId) & rf.Eq(x => x.SkuLevel, level)
                           & rf.Eq(x => x.Status, ReservationStatus.Active))
            .ToListAsync(ct);

        var pf = Builders<InventorySkuPosition>.Filter;
        var u = Builders<InventorySkuPosition>.Update;
        var now = DateTimeOffset.UtcNow;
        await _positions.UpdateOneAsync(
            session,
            pf.Eq(x => x.TenantId, tenant) & pf.Eq(x => x.LegalEntityId, le) & pf.Eq(x => x.IsDeleted, false)
            & pf.Eq(x => x.ItemId, item) & pf.Eq(x => x.SkuId, skuId) & pf.Eq(x => x.SkuLevel, level),
            u.Combine(
                u.Set(x => x.AvailableOnHand, rows.Sum(r => r.OnHand)),
                u.Set(x => x.Reserved, active.Sum(r => r.RemainingQuantity)),
                u.SetOnInsert(x => x.Id, Guid.NewGuid()),
                u.SetOnInsert(x => x.BaseUomId, rows.FirstOrDefault()?.BaseUomId ?? string.Empty),
                u.SetOnInsert(x => x.CreatedAt, now),
                u.SetOnInsert(x => x.Version, 0),
                u.SetOnInsert(x => x.DeletedAt, null)),
            new UpdateOptions { IsUpsert = true },
            ct);

        await RebuildScopesAsync(session, tenant, le, item, skuId, level, rows, active, now, ct);
        return true;
    }

    /// <summary>
    /// MOD-0173-FU03 §4.2: the warehouse and warehouse + lot positions of one SKU from the rows (held lots already left out) and its ACTIVE
    /// reservations. Rows without a warehouse belong to no warehouse position; a typed lot text has no lot position (its stock counts in
    /// the warehouse only). Existing positions that no source names are set to 0 (a rebuild never deletes).
    /// </summary>
    private async Task RebuildScopesAsync(
        IClientSessionHandle session, Guid? tenant, Guid? le, Guid item, Guid skuId, SkuLevel level,
        IReadOnlyList<InventoryBalance> rows, IReadOnlyList<InventoryReservation> active, DateTimeOffset now, CancellationToken ct)
    {
        var target = new Dictionary<(ReservationScopeLevel Level, string Warehouse, Guid Lot), (decimal OnHand, decimal Reserved, string Uom)>();
        void Add((ReservationScopeLevel, string, Guid) key, decimal onHand, decimal reserved, string uom)
        {
            (decimal OnHand, decimal Reserved, string Uom) current = target.TryGetValue(key, out var found) ? found : (0m, 0m, uom);
            target[key] = (current.OnHand + onHand, current.Reserved + reserved, current.Uom.Length > 0 ? current.Uom : uom);
        }

        foreach (var row in rows.Where(r => r.WarehouseId is not null))
        {
            Add((ReservationScopeLevel.Warehouse, row.WarehouseId!, Guid.Empty), row.OnHand, 0m, row.BaseUomId);
            if (LotPostingRules.IsRegisteredLotForm(row.LotId, out var lotId))
            {
                Add((ReservationScopeLevel.Lot, row.WarehouseId!, lotId), row.OnHand, 0m, row.BaseUomId);
            }
        }

        foreach (var reservation in active.Where(r => r.WarehouseId is not null))
        {
            Add((ReservationScopeLevel.Warehouse, reservation.WarehouseId!, Guid.Empty), 0m, reservation.RemainingQuantity, string.Empty);
            if (reservation.LotId is { } lotId)
            {
                Add((ReservationScopeLevel.Lot, reservation.WarehouseId!, lotId), 0m, reservation.RemainingQuantity, string.Empty);
            }
        }

        var sf = Builders<InventoryReservationPosition>.Filter;
        var skuFilter = sf.Eq(x => x.TenantId, tenant) & sf.Eq(x => x.LegalEntityId, le) & sf.Eq(x => x.IsDeleted, false)
                        & sf.Eq(x => x.ItemId, item) & sf.Eq(x => x.SkuId, skuId) & sf.Eq(x => x.SkuLevel, level);
        foreach (var existing in await _scopePositions.Find(session, skuFilter).ToListAsync(ct))
        {
            target.TryAdd((existing.Level, existing.WarehouseId, existing.LotId), (0m, 0m, existing.BaseUomId));
        }

        var su = Builders<InventoryReservationPosition>.Update;
        foreach (var ((scope, warehouse, lot), (onHand, reserved, uom)) in target)
        {
            await _scopePositions.UpdateOneAsync(
                session,
                skuFilter & sf.Eq(x => x.Level, scope) & sf.Eq(x => x.WarehouseId, warehouse) & sf.Eq(x => x.LotId, lot),
                su.Combine(
                    su.Set(x => x.AvailableOnHand, onHand),
                    su.Set(x => x.Reserved, reserved),
                    su.SetOnInsert(x => x.Id, Guid.NewGuid()),
                    su.SetOnInsert(x => x.BaseUomId, uom),
                    su.SetOnInsert(x => x.CreatedAt, now),
                    su.SetOnInsert(x => x.Version, 0),
                    su.SetOnInsert(x => x.DeletedAt, null)),
                new UpdateOptions { IsUpsert = true },
                ct);
        }
    }

    /// <summary>
    /// A document without a complete scope (Tenant, LE, item, sku, level) cannot be attributed to any position; it is
    /// skipped — never guessed, never deleted. Its count is reported by <see cref="LastSkippedUnscoped"/>.
    /// </summary>
    private bool TryKey(BsonDocument group, out (string, string, string, string, string) key)
    {
        key = default;
        var id = group["_id"].AsBsonDocument;
        foreach (var name in new[] { "t", "le", "item", "sku", "level" })
        {
            if (!id.TryGetValue(name, out var value) || !value.IsString || !(name == "level" || Guid.TryParse(value.AsString, out _)))
            {
                LastSkippedUnscoped++;
                return false;
            }
        }

        key = (id["t"].AsString, id["le"].AsString, id["item"].AsString, id["sku"].AsString, id["level"].AsString);
        return true;
    }

    /// <summary>Groups skipped by the last <see cref="RebuildAllAsync"/> because their scope was incomplete.</summary>
    public int LastSkippedUnscoped { get; private set; }
}
