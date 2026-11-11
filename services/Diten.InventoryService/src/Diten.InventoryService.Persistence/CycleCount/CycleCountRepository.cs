using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.CycleCounting;

/// <summary>
/// MOD-0181 counts on MongoDB (the MOD-0178 task-repository pattern). Every query starts from the caller's Tenant + LegalEntity — there is no
/// overload without the scope (AC-5 sabotage: drop the company / tenant predicate → the isolation tests are red). Each command runs in ONE
/// multi-document transaction; every count write is a compare-and-set on <c>Version</c>; the timeline rows and bin claims are insert-only
/// (claims are only ever closed); the central audit event is enlisted only when a count changed (pack §8.11). The ledger's balance and movement
/// collections are READ here with <c>Find</c> only (pack §2.3 rule 2) — no ledger repository, no write.
/// </summary>
public sealed class CycleCountRepository : ICycleCountRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<CycleCount> _counts;
    private readonly IMongoCollection<CycleCountLine> _lines;
    private readonly IMongoCollection<CycleCountEvent> _events;
    private readonly IMongoCollection<CycleCountBinClaim> _claims;
    private readonly ICycleCountReleaseProbe _probe;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public CycleCountRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant, ICycleCountReleaseProbe probe)
    {
        _client = client;
        _database = database;
        _counts = database.GetCollection<CycleCount>(CycleCountCollections.Counts);
        _lines = database.GetCollection<CycleCountLine>(CycleCountCollections.Lines);
        _events = database.GetCollection<CycleCountEvent>(CycleCountCollections.Events);
        _claims = database.GetCollection<CycleCountBinClaim>(CycleCountCollections.BinClaims);
        _probe = probe;
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Company<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId);

    private static FilterDefinition<CycleCount> Live() => Builders<CycleCount>.Filter.Eq(x => x.IsDeleted, false);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<CycleCount?> FindAsync(Guid countId, CancellationToken cancellationToken = default)
        => await _counts.Find(Company<CycleCount>() & Live() & Builders<CycleCount>.Filter.Eq(x => x.Id, countId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<CycleCount?> FindIncludingDeletedAsync(Guid countId, CancellationToken cancellationToken = default)
        => await _counts.Find(Company<CycleCount>() & Builders<CycleCount>.Filter.Eq(x => x.Id, countId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CycleCount>> FindManyAsync(IReadOnlyCollection<Guid> countIds, CancellationToken cancellationToken = default)
        => countIds.Count == 0
            ? []
            : await _counts.Find(Company<CycleCount>() & Live() & Builders<CycleCount>.Filter.In(x => x.Id, countIds)).ToListAsync(cancellationToken);

    public async Task<CycleCount?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _counts.Find(Company<CycleCount>() & Builders<CycleCount>.Filter.Eq(x => x.CreatedBy, createdBy)
                              & Builders<CycleCount>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<CycleCountPage> ListAsync(CycleCountQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<CycleCount>.Filter;
        var filter = Company<CycleCount>() & Live();
        if (query.Statuses.Count > 0) filter &= f.In(x => x.Status, query.Statuses);
        if (query.WarehouseId is { } warehouse) filter &= f.Eq(x => x.WarehouseId, warehouse);
        if (query.Search is not null) filter &= f.Regex(x => x.Number, new BsonRegularExpression(Regex.Escape(query.Search), "i"));

        var total = await _counts.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _counts.Find(filter).Sort(Builders<CycleCount>.Sort.Descending(x => x.CreatedAt).Descending(x => x.Number))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new CycleCountPage(items, total);
    }

    public async Task<CycleCountLinePage> LinesAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<CycleCountLine>() & Builders<CycleCountLine>.Filter.Eq(x => x.CountId, countId);
        var total = await _lines.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _lines.Find(filter)
            .Sort(Builders<CycleCountLine>.Sort.Ascending(x => x.LocationId).Ascending(x => x.SkuId).Ascending(x => x.LotId).Ascending(x => x.Id))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new CycleCountLinePage(items, total);
    }

    public async Task<CycleCountEventPage> EventsAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<CycleCountEvent>() & Builders<CycleCountEvent>.Filter.Eq(x => x.CountId, countId);
        var total = await _events.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _events.Find(filter).SortBy(x => x.Seq).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new CycleCountEventPage(items, total);
    }

    public async Task<CycleCountEvent?> FindKeyEventAsync(Guid countId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<CycleCountEvent>() & Builders<CycleCountEvent>.Filter.Eq(x => x.CountId, countId)
                              & Builders<CycleCountEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<CycleCountEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<CycleCountEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CycleCountEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<CycleCountEvent>() & Builders<CycleCountEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<CycleCountEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<CycleCountEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).ToListAsync(cancellationToken);

    /// <summary>
    /// The SKUs with AVAILABLE stock in the scope, read OUTSIDE any transaction — only to fetch their PRODUCT-MASTER serial flags before the
    /// release's snapshot transaction opens (no HTTP call while it is open). The snapshot itself is read again inside that transaction.
    /// </summary>
    public async Task<IReadOnlyList<(Guid ItemId, Guid SkuId, SkuLevel Level)>> ScopeSkusAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var bins = await _database.GetCollection<Location>(InventoryCollections.Locations).Find(ScopeNodes(request)).Project(x => x.Id).ToListAsync(cancellationToken);
            if (bins.Count == 0) return [];
            var rows = WriteSession.Rows(await _database.GetCollection<InventoryBalance>(InventoryCollections.Balances)
                .Find(ScopeBalances(bins.Select(b => b.ToString("D")).ToList(), request)).ToListAsync(cancellationToken));
            return rows.Select(r => (r.ItemId, r.SkuId, r.SkuLevel)).Distinct().ToList();
        }
        catch (MongoException ex)
        {
            throw new CycleCountInventoryUnavailableException("The stock of the scope could not be read.", ex);
        }
    }

    private FilterDefinition<Location> ScopeNodes(CycleCountScopeRequest request)
    {
        var lf = Builders<Location>.Filter;
        var nodes = lf.Eq(x => x.TenantId, _tenantId) & lf.Eq(x => x.LegalEntityId, _legalEntityId) & lf.Eq(x => x.IsDeleted, false)
                    & lf.Eq(x => x.IsStockable, true) & lf.Eq(x => x.Status, LocationStatus.Active) & lf.AnyEq(x => x.AncestorIds, request.WarehouseId);
        return request.Full ? nodes : nodes & lf.Or(lf.In(x => x.Id, request.ScopeLocationIds), lf.AnyIn(x => x.AncestorIds, request.ScopeLocationIds));
    }

    private FilterDefinition<InventoryBalance> ScopeBalances(IReadOnlyCollection<string> binText, CycleCountScopeRequest request)
    {
        var bf = Builders<InventoryBalance>.Filter;
        var balances = bf.Eq(x => x.TenantId, _tenantId) & bf.Eq(x => x.LegalEntityId, _legalEntityId) & bf.Eq(x => x.IsDeleted, false)
                       & bf.In(x => x.LocationId, binText) & bf.Gt(x => x.OnHand, 0m) & bf.Eq(x => x.StockStatus, StockStatus.Available);
        return request.ScopeSkuIds.Count > 0 ? balances & bf.In(x => x.SkuId, request.ScopeSkuIds) : balances;
    }

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<CycleCountWriteOutcome> InTransactionAsync(Func<ICycleCountWriteSession, CancellationToken, Task<CycleCountWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    var outcome = await work(new WriteSession(this, s), ct);
                    // BL-501: the marked command's central-audit event commits with this transaction — and only when it changed a count.
                    if (outcome.Affected > 0) await CentralAuditTransaction.EnlistAsync(s, outcome.EntityId, ct);
                    return outcome;
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new CycleCountDuplicateException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new CycleCountDuplicateException(index, ex);
        }
    }

    internal static CycleCountUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(CycleCountIndexConfiguration.BinClaimOpenIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.BinClaim;
        if (message.Contains(CycleCountIndexConfiguration.NumberIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.Number;
        if (message.Contains(CycleCountIndexConfiguration.CreateKeyIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.CreateKey;
        if (message.Contains(CycleCountIndexConfiguration.LineKeyIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.Line;
        return message.Contains(CycleCountIndexConfiguration.EventSeqIndex, StringComparison.Ordinal)
               || message.Contains(CycleCountIndexConfiguration.EventKeyIndex, StringComparison.Ordinal)
            ? CycleCountUniqueIndex.Event
            : null;
    }

    private sealed class WriteSession(CycleCountRepository owner, IClientSessionHandle session) : ICycleCountWriteSession
    {
        public static List<CycleCountSnapshotRow> Rows(IEnumerable<InventoryBalance> balances)
            => balances.Where(b => Guid.TryParse(b.LocationId, out _))
                .Select(b => new CycleCountSnapshotRow(Guid.Parse(b.LocationId!), b.ItemId, b.SkuId, b.SkuLevel, b.LotId, b.StockStatus, b.OnHand, b.BaseUomId))
                .ToList();

        public Task InsertCountAsync(CycleCount count, CancellationToken cancellationToken = default)
        {
            owner.Stamp(count);
            return owner._counts.InsertOneAsync(session, count, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceCountAsync(CycleCount count, int expectedVersion, CancellationToken cancellationToken = default)
        {
            owner.Stamp(count);
            var filter = owner.Company<CycleCount>() & Builders<CycleCount>.Filter.Eq(x => x.Id, count.Id) & Builders<CycleCount>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await owner._counts.ReplaceOneAsync(session, filter, count, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertLinesAsync(IReadOnlyList<CycleCountLine> lines, CancellationToken cancellationToken = default)
        {
            if (lines.Count == 0) return Task.CompletedTask;
            foreach (var l in lines) owner.Stamp(l);
            return owner._lines.InsertManyAsync(session, lines, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public Task InsertEventsAsync(IReadOnlyList<CycleCountEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return Task.CompletedTask;
            foreach (var e in events) owner.Stamp(e);
            return owner._events.InsertManyAsync(session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public Task InsertBinClaimsAsync(IReadOnlyList<CycleCountBinClaim> claims, CancellationToken cancellationToken = default)
        {
            if (claims.Count == 0) return Task.CompletedTask;
            foreach (var c in claims) owner.Stamp(c);
            return owner._claims.InsertManyAsync(session, claims, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task<long> ReleaseBinClaimsAsync(Guid countId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var filter = owner.Company<CycleCountBinClaim>() & Builders<CycleCountBinClaim>.Filter.Eq(x => x.CountId, countId)
                         & Builders<CycleCountBinClaim>.Filter.Eq(x => x.Open, true);
            var update = Builders<CycleCountBinClaim>.Update.Set(x => x.Open, false).Set(x => x.ReleasedAt, at).Set(x => x.UpdatedAt, at).Inc(x => x.Version, 1);
            var result = await owner._claims.UpdateManyAsync(session, filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount;
        }

        public async Task<CycleCountScopeSnapshot> ReadScopeSnapshotAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // (a1) the scope: the stockable Active nodes of the subtree (Full: of the whole warehouse) — LOCATION, read only
                var bins = (await owner._database.GetCollection<Location>(InventoryCollections.Locations).Find(session, owner.ScopeNodes(request))
                        .Project(x => x.Id).ToListAsync(cancellationToken))
                    .OrderBy(x => x).ToList();
                if (bins.Count == 0) return new CycleCountScopeSnapshot([], [], []);

                // (a2) the AVAILABLE balance rows with stock on those bins (Q-8 = A), the SKU filter when set — the ledger's projection, read only
                var binText = bins.Select(b => b.ToString("D")).ToList();
                var rows = Rows(await owner._database.GetCollection<InventoryBalance>(InventoryCollections.Balances).Find(session, owner.ScopeBalances(binText, request))
                    .ToListAsync(cancellationToken));

                // pack §8.2 rule 4 test seam — between the balance read and the movement-id read (production: no-op)
                await owner._probe.AfterBalanceReadAsync(cancellationToken);

                // (b) the window's movements on the scope bins — from the SAME snapshot, so a posting is either in both reads or in neither
                var tf = Builders<InventoryTransaction>.Filter;
                var movements = tf.Eq(x => x.TenantId, owner._tenantId) & tf.Eq(x => x.LegalEntityId, owner._legalEntityId)
                                & tf.Gte(x => x.CreatedAt, request.WindowStart) & tf.Or(tf.In(x => x.ToLocationId, binText), tf.In(x => x.FromLocationId, binText));
                var ids = await owner._database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Find(session, movements)
                    .Project(x => x.Id).ToListAsync(cancellationToken);

                return new CycleCountScopeSnapshot(bins, rows, ids.OrderBy(x => x).ToList());
            }
            catch (MongoException ex) when (!ex.HasErrorLabel("TransientTransactionError") && ex is not MongoWriteException)
            {
                throw new CycleCountInventoryUnavailableException("The stock snapshot could not be read.", ex);
            }
        }
    }
}
