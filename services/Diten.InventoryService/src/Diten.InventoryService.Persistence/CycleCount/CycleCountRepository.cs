using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
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
    private readonly IMongoCollection<CycleCountSettings> _settings;
    private readonly IMongoCollection<CountEvidenceExport> _evidenceExports;
    private readonly IMongoCollection<CountEvidenceFile> _evidenceFiles;
    private readonly ICycleCountReleaseProbe _probe;
    private readonly ICycleCountSubmitProbe _submitProbe;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public CycleCountRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant, ICycleCountReleaseProbe probe,
        ICycleCountSubmitProbe submitProbe)
    {
        _client = client;
        _database = database;
        _counts = database.GetCollection<CycleCount>(CycleCountCollections.Counts);
        _lines = database.GetCollection<CycleCountLine>(CycleCountCollections.Lines);
        _events = database.GetCollection<CycleCountEvent>(CycleCountCollections.Events);
        _claims = database.GetCollection<CycleCountBinClaim>(CycleCountCollections.BinClaims);
        _settings = database.GetCollection<CycleCountSettings>(CycleCountCollections.Settings);
        _evidenceExports = database.GetCollection<CountEvidenceExport>(CycleCountCollections.EvidenceExports);
        _evidenceFiles = database.GetCollection<CountEvidenceFile>(CycleCountCollections.EvidenceFiles);
        _probe = probe;
        _submitProbe = submitProbe;
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

    public async Task<CycleCountLine?> FindLineAsync(Guid countId, Guid lineId, CancellationToken cancellationToken = default)
        => await _lines.Find(Company<CycleCountLine>() & Builders<CycleCountLine>.Filter.Eq(x => x.CountId, countId)
                             & Builders<CycleCountLine>.Filter.Eq(x => x.Id, lineId)).FirstOrDefaultAsync(cancellationToken);

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

    // ── DL-3 reads ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<CycleCountLine?> FindLineByIdAsync(Guid lineId, CancellationToken cancellationToken = default)
        => await _lines.Find(Company<CycleCountLine>() & Builders<CycleCountLine>.Filter.Eq(x => x.Id, lineId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CycleCountLine>> FindLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken cancellationToken = default)
        => lineIds.Count == 0
            ? []
            : await _lines.Find(Company<CycleCountLine>() & Builders<CycleCountLine>.Filter.In(x => x.Id, lineIds)).ToListAsync(cancellationToken);

    public async Task<CycleCountLineSet> VarianceLinesAsync(CountVarianceLineQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<CycleCountLine>.Filter;
        var filter = Company<CycleCountLine>() & f.Ne(x => x.VarianceQuantity, null);
        if (query.WarehouseId is { } warehouse) filter &= f.Eq(x => x.WarehouseId, warehouse);
        if (query.Statuses.Count > 0) filter &= f.In(x => x.Status, query.Statuses);
        if (query.From is { } from) filter &= f.Gte(x => x.CountedAt, from);
        if (query.To is { } to) filter &= f.Lt(x => x.CountedAt, to);
        if (query.SkuId is { } sku) filter &= f.Eq(x => x.SkuId, sku);
        if (query.LotId is not null) filter &= f.Eq(x => x.LotId, query.LotId);

        var sorted = _lines.Find(filter).Sort(Builders<CycleCountLine>.Sort.Descending(x => x.CountedAt).Ascending(x => x.Id));
        if (query.All) return new CycleCountLineSet(await sorted.ToListAsync(cancellationToken), -1);
        var total = await _lines.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return new CycleCountLineSet(await sorted.Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken), total);
    }

    public async Task<CycleCountDashboardCounts> DashboardCountsAsync(Guid? warehouseId, DateTimeOffset periodStart, CancellationToken cancellationToken = default)
    {
        var cf = Builders<CycleCount>.Filter;
        var counts = Company<CycleCount>() & Live()
                     & cf.In(x => x.Status, [CycleCountStatus.Released, CycleCountStatus.Counting, CycleCountStatus.Submitted]);
        var lf = Builders<CycleCountLine>.Filter;
        var lines = Company<CycleCountLine>();
        if (warehouseId is { } warehouse)
        {
            counts &= cf.Eq(x => x.WarehouseId, warehouse);
            lines &= lf.Eq(x => x.WarehouseId, warehouse);
        }

        var period = lines & lf.Ne(x => x.VarianceQuantity, null) & lf.Gte(x => x.CountedAt, periodStart);
        return new CycleCountDashboardCounts(
            await _counts.CountDocumentsAsync(counts, cancellationToken: cancellationToken),
            await _lines.CountDocumentsAsync(lines & lf.Eq(x => x.Status, CycleCountLineStatus.AwaitingApproval), cancellationToken: cancellationToken),
            await _lines.CountDocumentsAsync(lines & lf.Eq(x => x.Status, CycleCountLineStatus.Approved)
                                             & lf.Eq(x => x.Adjustment!.Status, CycleCountAdjustmentStatus.Refused), cancellationToken: cancellationToken),
            await _lines.CountDocumentsAsync(period, cancellationToken: cancellationToken),
            await _lines.CountDocumentsAsync(period & lf.Eq(x => x.VarianceQuantity, 0m), cancellationToken: cancellationToken));
    }

    public async Task<CycleCountSettings?> FindSettingsAsync(CancellationToken cancellationToken = default)
        => await _settings.Find(Company<CycleCountSettings>()).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> WarehouseNodesAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var lf = Builders<Location>.Filter;
            return await _database.GetCollection<Location>(InventoryCollections.Locations).Find(lf.Eq(x => x.TenantId, _tenantId) & lf.Eq(x => x.LegalEntityId, _legalEntityId)
                    & lf.AnyEq(x => x.AncestorIds, warehouseId)).Project(x => x.Id).ToListAsync(cancellationToken);
        }
        catch (MongoException ex)
        {
            throw new CycleCountInventoryUnavailableException("The warehouse's locations could not be read.", ex);
        }
    }

    /// <summary>
    /// Pack §2.4 — the ledger's movement collection, READ only: the company's <c>COUNT_GAIN</c> into / <c>COUNT_LOSS</c> out of the bins, created in
    /// the window. Each branch rides a bin index (<c>ix_tenant_le_tolocation</c> / <c>ix_tenant_le_fromlocation</c>); there is no movement-type
    /// index and 0181 adds none (Q-21 = A).
    /// </summary>
    public async Task<IReadOnlyList<CycleCountLedgerMovementRow>> CountMovementsAsync(CycleCountManualRequest request, CancellationToken cancellationToken = default)
    {
        if (request.BinIds.Count == 0) return [];
        try
        {
            var binText = request.BinIds.Select(b => b.ToString("D")).ToList();
            var tf = Builders<InventoryTransaction>.Filter;
            var filter = tf.Eq(x => x.TenantId, _tenantId) & tf.Eq(x => x.LegalEntityId, _legalEntityId)
                         & tf.Or(tf.In(x => x.ToLocationId, binText) & tf.Eq(x => x.MovementType, MovementType.CountGain),
                             tf.In(x => x.FromLocationId, binText) & tf.Eq(x => x.MovementType, MovementType.CountLoss))
                         & tf.Gte(x => x.CreatedAt, request.From) & tf.Lt(x => x.CreatedAt, request.To);
            return (await _database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Find(filter).ToListAsync(cancellationToken))
                .Select(MovementRow).ToList();
        }
        catch (MongoException ex)
        {
            throw new CycleCountInventoryUnavailableException("The ledger's count movements could not be read.", ex);
        }
    }

    public async Task<IReadOnlyList<CycleCountLedgerMovementRow>> MovementsByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0) return [];
        try
        {
            var tf = Builders<InventoryTransaction>.Filter;
            return (await _database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Find(tf.Eq(x => x.TenantId, _tenantId)
                    & tf.Eq(x => x.LegalEntityId, _legalEntityId) & tf.In(x => x.IdempotencyKey, keys)).ToListAsync(cancellationToken))
                .Select(MovementRow).ToList();
        }
        catch (MongoException ex)
        {
            throw new CycleCountInventoryUnavailableException("The ledger's movements could not be read.", ex);
        }
    }

    public async Task<CycleCountLedgerMovementRow?> LedgerMovementAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tf = Builders<InventoryTransaction>.Filter;
            var found = await _database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Find(tf.Eq(x => x.TenantId, _tenantId)
                & tf.Eq(x => x.LegalEntityId, _legalEntityId) & tf.Eq(x => x.Id, transactionId)).FirstOrDefaultAsync(cancellationToken);
            return found is null ? null : MovementRow(found);
        }
        catch (MongoException ex)
        {
            throw new CycleCountInventoryUnavailableException("The ledger movement could not be read.", ex);
        }
    }

    private static CycleCountLedgerMovementRow MovementRow(InventoryTransaction t) => new(t.Id, t.TransactionNumber, t.MovementType, t.ItemId, t.SkuId, t.SkuLevel,
        t.FromWarehouseId, t.FromLocationId, t.ToWarehouseId, t.ToLocationId, t.LotId, t.FromStockStatus, t.ToStockStatus, t.Quantity, t.UomId, t.BaseQuantity,
        t.SourceModule, t.SourceType, t.SourceDocumentId, t.SourceLineId, t.IdempotencyKey, t.CreatedBy, t.CreatedAt, t.TransactionDate, t.PostingDate,
        t.CorrelationId);

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

    // ── DL-5 Evidence Pack reads (pack §4.5; AC-44: the tenant + company predicate on every one) ─────────

    public async Task<CountEvidenceExport?> FindEvidenceExportByKeyAsync(Guid requestedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _evidenceExports.Find(Company<CountEvidenceExport>() & Builders<CountEvidenceExport>.Filter.Eq(x => x.RequestedBy, requestedBy)
                                       & Builders<CountEvidenceExport>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<CountEvidenceExport?> FindEvidenceExportAsync(Guid exportId, CancellationToken cancellationToken = default)
        => await _evidenceExports.Find(Company<CountEvidenceExport>() & Builders<CountEvidenceExport>.Filter.Eq(x => x.Id, exportId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<CountEvidenceExportPage> ListEvidenceExportsAsync(Guid? countId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<CountEvidenceExport>();
        if (countId is { } id) filter &= Builders<CountEvidenceExport>.Filter.Eq(x => x.CountId, id);
        var total = await _evidenceExports.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _evidenceExports.Find(filter).Sort(Builders<CountEvidenceExport>.Sort.Descending(x => x.RequestedAt).Descending(x => x.Id))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new CountEvidenceExportPage(items, total);
    }

    public async Task<CountEvidenceFile?> FindEvidenceFileAsync(Guid exportId, CancellationToken cancellationToken = default)
        => await _evidenceFiles.Find(Company<CountEvidenceFile>() & Builders<CountEvidenceFile>.Filter.Eq(x => x.ExportId, exportId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CycleCountLocationName>> LocationNamesAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default)
    {
        if (locationIds.Count == 0) return [];
        var lf = Builders<Location>.Filter;
        return (await _database.GetCollection<Location>(InventoryCollections.Locations)
                .Find(lf.Eq(x => x.TenantId, _tenantId) & lf.Eq(x => x.LegalEntityId, _legalEntityId) & lf.In(x => x.Id, locationIds)).ToListAsync(cancellationToken))
            .Select(x => new CycleCountLocationName(x.Id, x.Code, x.Name)).ToList();
    }

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
        if (message.Contains(CycleCountIndexConfiguration.SettingsIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.Settings;
        if (message.Contains(CycleCountIndexConfiguration.EvidenceKeyIndex, StringComparison.Ordinal)) return CycleCountUniqueIndex.EvidenceKey;
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

        public async Task<bool> ReplaceLinesAsync(IReadOnlyList<(CycleCountLine Line, int ExpectedVersion)> lines, CancellationToken cancellationToken = default)
        {
            if (lines.Count == 0) return true;
            var lf = Builders<CycleCountLine>.Filter;
            var models = lines.Select(x =>
            {
                owner.Stamp(x.Line);
                return new ReplaceOneModel<CycleCountLine>(owner.Company<CycleCountLine>() & lf.Eq(l => l.Id, x.Line.Id) & lf.Eq(l => l.Version, x.ExpectedVersion), x.Line);
            }).ToList();
            var result = await owner._lines.BulkWriteAsync(session, models, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            return result.MatchedCount == lines.Count;
        }

        public async Task<IReadOnlyList<CycleCountLine>> ReadLinesAsync(Guid countId, CancellationToken cancellationToken = default)
            => await owner._lines.Find(session, owner.Company<CycleCountLine>() & Builders<CycleCountLine>.Filter.Eq(x => x.CountId, countId)).ToListAsync(cancellationToken);

        public async Task<CycleCountLedgerRead> ReadVarianceLedgerAsync(CycleCountLedgerRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // pack §8.4 test seam (AC-20) — production: no-op; a driver error here is an unreadable ledger
                await owner._submitProbe.BeforeLedgerReadAsync(cancellationToken);
                if (request.BinIds.Count == 0) return new CycleCountLedgerRead([], new Dictionary<CycleCountLineKey, decimal>());

                var binText = request.BinIds.Select(b => b.ToString("D")).ToList();
                var bins = request.BinIds.ToHashSet();

                // the window's movements on the scope bins (CreatedAt, never PostingDate) — the ledger's collection, read only, in THIS snapshot
                var tf = Builders<InventoryTransaction>.Filter;
                var window = tf.Eq(x => x.TenantId, owner._tenantId) & tf.Eq(x => x.LegalEntityId, owner._legalEntityId)
                             & tf.Gte(x => x.CreatedAt, request.WindowStart) & tf.Or(tf.In(x => x.ToLocationId, binText), tf.In(x => x.FromLocationId, binText));
                var transactions = await owner._database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Find(session, window)
                    .ToListAsync(cancellationToken);
                var movements = transactions.Select(t => new CycleCountLedgerMovement(t.Id, t.CreatedAt,
                    LedgerLegs.For(t)
                        .Where(l => Guid.TryParse(l.Dimension.LocationId, out var bin) && bins.Contains(bin))
                        .Select(l => new CycleCountLedgerLeg(new CycleCountLineKey(Guid.Parse(l.Dimension.LocationId!), l.Dimension.SkuId, l.Dimension.SkuLevel,
                            l.Dimension.LotId, l.Dimension.StockStatus), l.Delta))
                        .ToList())).ToList();

                // the balance today of every AVAILABLE row of those bins (zero / negative too), per line key — the ledger's projection, read only
                var bf = Builders<InventoryBalance>.Filter;
                var rows = bf.Eq(x => x.TenantId, owner._tenantId) & bf.Eq(x => x.LegalEntityId, owner._legalEntityId) & bf.Eq(x => x.IsDeleted, false)
                           & bf.In(x => x.LocationId, binText) & bf.Eq(x => x.StockStatus, StockStatus.Available);
                var balances = await owner._database.GetCollection<InventoryBalance>(InventoryCollections.Balances).Find(session, rows).ToListAsync(cancellationToken);
                var today = Rows(balances).GroupBy(r => new CycleCountLineKey(r.LocationId, r.SkuId, r.SkuLevel, r.LotId, r.StockStatus))
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.OnHand));

                return new CycleCountLedgerRead(movements, today);
            }
            catch (MongoException ex) when (!ex.HasErrorLabel("TransientTransactionError") && ex is not MongoWriteException)
            {
                throw new CycleCountInventoryUnavailableException("The ledger could not be read for the variance.", ex);
            }
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

        public async Task<IReadOnlyList<Guid>> TransferBinClaimsAsync(Guid fromCountId, Guid toCountId, IReadOnlyCollection<Guid> binIds, DateTimeOffset at,
            CancellationToken cancellationToken = default)
        {
            if (binIds.Count == 0) return [];
            var cf = Builders<CycleCountBinClaim>.Filter;
            var filter = owner.Company<CycleCountBinClaim>() & cf.Eq(x => x.CountId, fromCountId) & cf.Eq(x => x.Open, true) & cf.In(x => x.BinId, binIds);
            var moved = await owner._claims.Find(session, filter).Project(x => x.BinId).ToListAsync(cancellationToken);
            if (moved.Count == 0) return [];
            var update = Builders<CycleCountBinClaim>.Update.Set(x => x.CountId, toCountId).Set(x => x.TransferredFromCountId, fromCountId)
                .Set(x => x.UpdatedAt, at).Inc(x => x.Version, 1);
            await owner._claims.UpdateManyAsync(session, filter, update, cancellationToken: cancellationToken);
            return moved;
        }

        public async Task InsertEvidenceExportAsync(CountEvidenceExport export, CountEvidenceFile file, CancellationToken cancellationToken = default)
        {
            owner.Stamp(export);
            owner.Stamp(file);
            await owner._evidenceExports.InsertOneAsync(session, export, cancellationToken: cancellationToken);
            await owner._evidenceFiles.InsertOneAsync(session, file, cancellationToken: cancellationToken);
        }

        public Task InsertSettingsAsync(CycleCountSettings settings, CancellationToken cancellationToken = default)
        {
            owner.Stamp(settings);
            return owner._settings.InsertOneAsync(session, settings, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceSettingsAsync(CycleCountSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
        {
            owner.Stamp(settings);
            var sf = Builders<CycleCountSettings>.Filter;
            var result = await owner._settings.ReplaceOneAsync(session,
                owner.Company<CycleCountSettings>() & sf.Eq(x => x.Id, settings.Id) & sf.Eq(x => x.Version, expectedVersion), settings,
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
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
