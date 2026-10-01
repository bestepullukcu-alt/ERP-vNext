using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.ValueObjects;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// The MOD-0173 ledger on MongoDB. Every query starts from <see cref="TransactionScope"/> /
/// <see cref="BalanceScope"/> (Tenant + LegalEntity + IsDeleted = false) taken from the resolved JWT scope — there is
/// no overload without it (tenant-audit: no unfiltered query).
///
/// <para><b>Posting</b> runs in ONE multi-document transaction (pack §8): per-year counter → balance legs
/// (decreases first, each under a coverage condition) → SKU position (S2) → transaction insert → audit insert → outbox insert. A leg
/// that is not covered aborts the whole transaction; nothing is partially written. Two concurrent decreases of the
/// same row conflict inside Mongo (WriteConflict → the driver retries the loser), and the retry re-evaluates the
/// coverage condition against the committed winner — so balance 100 with two parallel issues of 60 ends at 40,
/// never −20 (AC6).</para>
///
/// <para><b>Locations (MOD-0173-FU02 K-13, §8)</b>: inside the same transaction, before any leg, every leg location
/// must still be an Active stockable location of this company under the leg's warehouse; its <c>StockTouchSeq</c> is
/// incremented. A deactivation / archive / move / stockable change writes the same document in its own transaction,
/// so the two conflict in Mongo and the loser re-runs on the committed state: stock never lands on an Inactive
/// location. A location id that is not the canonical UUID string is not a location (ASSUMPTION-LOC1-03).</para>
///
/// <para><b>Append-only</b>: the transaction collection is only ever read or <c>InsertOne</c>-ed. There is no
/// update, replace or delete of a transaction anywhere in this class (AC17, architecture-tested).</para>
/// </summary>
public sealed class InventoryLedgerRepository : IInventoryLedgerRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<InventoryAuditRecord> _audit;
    private readonly IMongoCollection<InventoryOutboxMessage> _outbox;
    private readonly IMongoCollection<BsonDocument> _sequences;
    private readonly IMongoCollection<Location> _locations;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<InventoryLedgerRepository> _logger;
    private readonly ReservationPositionOperations _reservations;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public InventoryLedgerRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext,
        TransactionRetryCounter retries,
        ILogger<InventoryLedgerRepository> logger)
    {
        _client = client;
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _audit = database.GetCollection<InventoryAuditRecord>(InventoryCollections.Audit);
        _outbox = database.GetCollection<InventoryOutboxMessage>(InventoryCollections.Outbox);
        _sequences = database.GetCollection<BsonDocument>(InventoryCollections.Sequences);
        _locations = database.GetCollection<Location>(InventoryCollections.Locations);
        _retries = retries;
        _logger = logger;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
        _reservations = new ReservationPositionOperations(database, _tenantId, _legalEntityId, tenantContext.LegalEntitySource);
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<InventoryTransaction?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _transactions
            .Find(TransactionScope() & Builders<InventoryTransaction>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<InventoryTransaction?> FindTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => await _transactions
            .Find(TransactionScope() & Builders<InventoryTransaction>.Filter.Eq(x => x.Id, transactionId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> IsReversedAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => await _transactions
            .Find(TransactionScope() & Builders<InventoryTransaction>.Filter.Eq(x => x.ReversesTransactionId, transactionId))
            .AnyAsync(cancellationToken);

    public async Task<BalancePage> QueryBalancesAsync(BalanceQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryBalance>.Filter;
        var filter = BalanceFilter(query.ItemId, query.SkuId, query.SkuLevel, query.WarehouseId, query.LocationId);
        if (query.LotId is not null) filter &= f.Eq(x => x.LotId, query.LotId);
        if (query.StockStatus.HasValue) filter &= f.Eq(x => x.StockStatus, query.StockStatus.Value);

        var total = await _balances.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var rows = await _balances.Find(filter)
            .Sort(Builders<InventoryBalance>.Sort
                .Ascending(x => x.ItemId).Ascending(x => x.SkuId).Ascending(x => x.WarehouseId)
                .Ascending(x => x.LocationId).Ascending(x => x.LotId).Ascending(x => x.StockStatus))
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        return new BalancePage(rows, total);
    }

    public async Task<IReadOnlyList<InventoryBalance>> ListBalancesAsync(AvailabilityFilter filter, CancellationToken cancellationToken = default)
        => await _balances
            .Find(BalanceFilter(filter.ItemId, filter.SkuId, filter.SkuLevel, filter.WarehouseId, filter.LocationId))
            .ToListAsync(cancellationToken);

    public async Task<TransactionPage> QueryTransactionsAsync(TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var filter = TransactionScope();
        if (query.ItemId.HasValue) filter &= f.Eq(x => x.ItemId, query.ItemId.Value);
        if (query.LotId is not null) filter &= f.Eq(x => x.LotId, query.LotId);
        if (query.LocationId is not null)
            filter &= f.Or(f.Eq(x => x.FromLocationId, query.LocationId), f.Eq(x => x.ToLocationId, query.LocationId));
        if (query.PostingFromUtc.HasValue) filter &= f.Gte(x => x.PostingDate, query.PostingFromUtc.Value);
        if (query.PostingToUtcExclusive.HasValue) filter &= f.Lt(x => x.PostingDate, query.PostingToUtcExclusive.Value);

        var total = await _transactions.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _transactions.Find(filter)
            .Sort(Builders<InventoryTransaction>.Sort.Descending(x => x.PostingDate).Descending(x => x.CreatedAt))
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        return new TransactionPage(items, total);
    }

    // ── the single write ──────────────────────────────────────────────────────────────────────────────────

    public async Task<LedgerPostingResult> PostAsync(LedgerPosting posting, CancellationToken cancellationToken = default)
    {
        var transaction = posting.Transaction;
        StampScope(transaction);
        StampScope(posting.Audit);
        StampScope(posting.Outbox);

        var snapshotLeg = LedgerLegs.SnapshotLeg(posting.Legs);

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        try
        {
            return await session.WithTransactionAsync(async (s, ct) =>
            {
                if (++attempt > 1)
                {
                    _retries.Retried(TransactionRetryCounter.Posting);
                    _logger.LogInformation("Ledger posting transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                }

                await TouchLegLocationsAsync(s, posting.Legs, ct);

                var number = await NextTransactionNumberAsync(s, transaction.PostingDate.UtcDateTime.Year, ct);
                transaction.TransactionNumber = number;
                posting.Audit.TransactionNumber = number;
                posting.Outbox.Payload.TransactionNumber = number;

                InventoryBalance? snapshotRow = null;
                foreach (var leg in posting.Legs.OrderBy(l => l.IsDecrease ? 0 : 1))
                {
                    var row = leg.IsDecrease
                        ? await DecreaseAsync(s, leg, transaction, ct)
                        : await IncreaseAsync(s, leg, transaction, ct);
                    if (ReferenceEquals(leg, snapshotLeg))
                    {
                        snapshotRow = row;
                    }
                }

                transaction.ResultSnapshot = (snapshotRow ?? throw new InvalidOperationException("Snapshot leg was not applied.")).ToSnapshot();

                await ApplySkuPositionAsync(s, posting, ct);

                await _transactions.InsertOneAsync(s, transaction, cancellationToken: ct);
                await _audit.InsertOneAsync(s, posting.Audit, cancellationToken: ct);
                await _outbox.InsertOneAsync(s, posting.Outbox, cancellationToken: ct);

                return LedgerPostingResult.Posted(transaction);
            },
            new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
            cancellationToken);
        }
        catch (UncoveredQuantityException uncovered)
        {
            return LedgerPostingResult.Insufficient(uncovered.Available, uncovered.Requested);
        }
        catch (LocationNotPostableException notPostable)
        {
            return LedgerPostingResult.NotPostable(notPostable.LocationId);
        }
        catch (MongoWriteException ex) when (IsDuplicateOn(ex, InventoryIndexConfiguration.IdempotencyIndex))
        {
            return LedgerPostingResult.KeyTaken();
        }
        catch (MongoWriteException ex) when (IsDuplicateOn(ex, InventoryIndexConfiguration.ReversalIndex))
        {
            return LedgerPostingResult.Reversed();
        }
    }

    /// <summary>
    /// The S2 half of a posting (pack ASSUMPTION-0173-13..16), in the same transaction as the legs: the SKU position
    /// moves by the posting's NET AVAILABLE delta (a transfer between two AVAILABLE rows nets 0 and touches nothing —
    /// reservations are SKU-level, not bin-level). On a net decrease the SKU's due reservations are expired, a matching
    /// reservation is consumed, and only then is <c>AvailableOnHand − Reserved ≥ 0</c> enforced, so a holder can issue
    /// its own reserved stock while nobody else can.
    /// </summary>
    private async Task ApplySkuPositionAsync(IClientSessionHandle session, LedgerPosting posting, CancellationToken ct)
    {
        var transaction = posting.Transaction;
        var netAvailable = posting.Legs
            .Where(l => l.Dimension.StockStatus == StockStatus.Available)
            .Sum(l => l.Delta);
        if (netAvailable == 0m)
        {
            return;
        }

        var sku = new SkuKey(transaction.ItemId, transaction.SkuId, transaction.SkuLevel);
        var now = transaction.CreatedAt;
        if (netAvailable < 0m)
        {
            await _reservations.ExpireDueAsync(session, sku, transaction.CorrelationId, transaction.CreatedBy, now, ct);
            await _reservations.ConsumeAsync(session, sku, transaction.SourceModule, transaction.SourceDocumentId,
                transaction.SourceLineId, -netAvailable, transaction.Id, transaction.CorrelationId, transaction.CreatedBy, now, ct);
        }

        await _reservations.ApplyAvailableDeltaAsync(session, sku, netAvailable, transaction.BaseUomId, now, ct);
    }

    /// <summary>
    /// K-13 inside the posting transaction: each distinct (warehouse, location) of the legs must match an Active
    /// stockable location of this company whose path contains the warehouse; matching bumps its StockTouchSeq (the
    /// write that serialises the posting with a lifecycle change of the same document).
    /// </summary>
    private async Task TouchLegLocationsAsync(IClientSessionHandle session, IReadOnlyList<LedgerLeg> legs, CancellationToken ct)
    {
        var f = Builders<Location>.Filter;
        foreach (var (warehouseId, locationId) in legs.Select(l => (l.Dimension.WarehouseId, l.Dimension.LocationId)).Distinct())
        {
            if (!TryCanonicalId(locationId, out var location) || !TryCanonicalId(warehouseId, out var warehouse))
            {
                throw new LocationNotPostableException(locationId ?? string.Empty);
            }

            var filter = f.Eq(x => x.Id, location)
                         & f.Eq(x => x.TenantId, (Guid?)_tenantId)
                         & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                         & f.Eq(x => x.IsDeleted, false)
                         & f.Eq(x => x.Status, LocationStatus.Active)
                         & f.Eq(x => x.IsStockable, true)
                         & f.AnyEq(x => x.AncestorIds, warehouse);
            var touched = await _locations.UpdateOneAsync(session, filter,
                Builders<Location>.Update.Inc(x => x.StockTouchSeq, 1L), cancellationToken: ct);
            if (touched.MatchedCount == 0)
            {
                throw new LocationNotPostableException(locationId!);
            }
        }
    }

    /// <summary>A location / warehouse id is the canonical lower-case UUID string the location master returns.</summary>
    private static bool TryCanonicalId(string? wire, out Guid id)
        => Guid.TryParseExact(wire, "D", out id) && string.Equals(wire, id.ToString("D"), StringComparison.Ordinal);

    private async Task<string> NextTransactionNumberAsync(IClientSessionHandle session, int year, CancellationToken ct)
    {
        var id = $"{_tenantId:N}:{_legalEntityId:N}:{year.ToString(CultureInfo.InvariantCulture)}";
        var counter = await _sequences.FindOneAndUpdateAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update
                .Inc("Value", 1L)
                .SetOnInsert("TenantId", _tenantId.ToString())
                .SetOnInsert("LegalEntityId", _legalEntityId.ToString())
                .SetOnInsert("Year", year),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);
        var value = counter["Value"].ToInt64();
        return string.Create(CultureInfo.InvariantCulture, $"INV-{year:0000}-{value:0000000}");
    }

    /// <summary>
    /// Applies a negative leg only if the row covers it (DEC-INV-12): <c>Available ≥ q</c> on an AVAILABLE row,
    /// <c>OnHand ≥ q</c> otherwise. The condition is part of the update filter, so check and write are one atomic
    /// step inside the transaction.
    /// </summary>
    private async Task<InventoryBalance> DecreaseAsync(IClientSessionHandle session, LedgerLeg leg, InventoryTransaction transaction, CancellationToken ct)
    {
        var requested = -leg.Delta;
        var coverage = leg.Dimension.StockStatus == StockStatus.Available
            ? Builders<InventoryBalance>.Filter.Gte(x => x.Available, requested)
            : Builders<InventoryBalance>.Filter.Gte(x => x.OnHand, requested);

        var filter = DimensionFilter(leg.Dimension) & coverage;

        var updated = await _balances.FindOneAndUpdateAsync(
            session,
            filter,
            MovementUpdate(leg, transaction),
            new FindOneAndUpdateOptions<InventoryBalance> { ReturnDocument = ReturnDocument.After },
            ct);

        if (updated is not null)
        {
            return updated;
        }

        var current = await _balances.Find(session, DimensionFilter(leg.Dimension)).FirstOrDefaultAsync(ct);
        var available = current is null
            ? 0m
            : BalanceBuckets.Coverage(current.StockStatus, current.OnHand, current.Reserved);
        throw new UncoveredQuantityException(available, requested);
    }

    private async Task<InventoryBalance> IncreaseAsync(IClientSessionHandle session, LedgerLeg leg, InventoryTransaction transaction, CancellationToken ct)
    {
        var bucket = BalanceBuckets.BucketFor(leg.Dimension.StockStatus);
        var update = MovementUpdate(leg, transaction)
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.BaseUomId, leg.BaseUomId)
            .SetOnInsert(x => x.CreatedAt, transaction.CreatedAt)
            .SetOnInsert(x => x.DeletedAt, null)
            .SetOnInsert(x => x.Reserved, 0m);

        // A new row starts every bucket the leg does not move at zero (the moved one is $inc-ed from nothing).
        if (bucket != BalanceBucket.Available) update = update.SetOnInsert(x => x.Available, 0m);
        if (bucket != BalanceBucket.QualityInspection) update = update.SetOnInsert(x => x.QualityInspection, 0m);
        if (bucket != BalanceBucket.Blocked) update = update.SetOnInsert(x => x.Blocked, 0m);
        if (bucket != BalanceBucket.InTransit) update = update.SetOnInsert(x => x.InTransit, 0m);

        return await _balances.FindOneAndUpdateAsync(
            session,
            DimensionFilter(leg.Dimension),
            update,
            new FindOneAndUpdateOptions<InventoryBalance> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);
    }

    /// <summary>OnHand and the row's status bucket move together by the leg delta (pack §4 bucket rule).</summary>
    private static UpdateDefinition<InventoryBalance> MovementUpdate(LedgerLeg leg, InventoryTransaction transaction)
    {
        var u = Builders<InventoryBalance>.Update;
        var bucketMove = BalanceBuckets.BucketFor(leg.Dimension.StockStatus) switch
        {
            BalanceBucket.Available => u.Inc(x => x.Available, leg.Delta),
            BalanceBucket.QualityInspection => u.Inc(x => x.QualityInspection, leg.Delta),
            BalanceBucket.Blocked => u.Inc(x => x.Blocked, leg.Delta),
            BalanceBucket.InTransit => u.Inc(x => x.InTransit, leg.Delta),
            _ => throw new ArgumentOutOfRangeException(nameof(leg))
        };

        return u.Combine(
            u.Inc(x => x.OnHand, leg.Delta),
            bucketMove,
            u.Inc(x => x.Version, 1),
            u.Set(x => x.LastMovementId, transaction.Id),
            u.Set(x => x.LastPostingDate, transaction.PostingDate),
            u.Set(x => x.UpdatedAt, transaction.CreatedAt));
    }

    // ── scope ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Tenant + LegalEntity + IsDeleted = false — the mandatory entry filter of every transaction query.</summary>
    private FilterDefinition<InventoryTransaction> TransactionScope()
        => Builders<InventoryTransaction>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<InventoryTransaction>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryTransaction>.Filter.Eq(x => x.IsDeleted, false);

    /// <summary>Tenant + LegalEntity + IsDeleted = false — the mandatory entry filter of every balance query.</summary>
    private FilterDefinition<InventoryBalance> BalanceScope()
        => Builders<InventoryBalance>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<InventoryBalance>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryBalance>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<InventoryBalance> BalanceFilter(Guid? itemId, Guid? skuId, SkuLevel? skuLevel, string? warehouseId, string? locationId)
    {
        var f = Builders<InventoryBalance>.Filter;
        var filter = BalanceScope();
        if (itemId.HasValue) filter &= f.Eq(x => x.ItemId, itemId.Value);
        if (skuId.HasValue) filter &= f.Eq(x => x.SkuId, skuId.Value);
        if (skuLevel.HasValue) filter &= f.Eq(x => x.SkuLevel, skuLevel.Value);
        if (warehouseId is not null) filter &= f.Eq(x => x.WarehouseId, warehouseId);
        if (locationId is not null) filter &= f.Eq(x => x.LocationId, locationId);
        return filter;
    }

    /// <summary>The full dimension key (pack §4) inside the caller's scope. Equality on every member, nulls included.</summary>
    private FilterDefinition<InventoryBalance> DimensionFilter(InventoryDimension d)
    {
        var f = Builders<InventoryBalance>.Filter;
        return BalanceScope()
               & f.Eq(x => x.ItemId, d.ItemId)
               & f.Eq(x => x.SkuId, d.SkuId)
               & f.Eq(x => x.SkuLevel, d.SkuLevel)
               & f.Eq(x => x.WarehouseId, d.WarehouseId)
               & f.Eq(x => x.LocationId, d.LocationId)
               & f.Eq(x => x.LotId, d.LotId)
               & f.Eq(x => x.StockStatus, d.StockStatus)
               & f.Eq(x => x.OwnerId, d.OwnerId)
               & f.Eq(x => x.ProjectId, d.ProjectId);
    }

    private void StampScope(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    private static bool IsDuplicateOn(MongoWriteException ex, string indexName)
        => ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
           && ex.WriteError.Message.Contains(indexName, StringComparison.Ordinal);
}
