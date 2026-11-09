using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// MOD-0178 S2 outbound shipments on MongoDB (the task repository's pattern). Every query starts from the caller's Tenant + LegalEntity — there
/// is no overload without the scope (sabotage (ct): drop the company predicate → the isolation tests are red). Each command runs in ONE
/// multi-document transaction; every outbound write is a compare-and-set on <c>Version</c>, the timeline rows are INSERT-ONLY, transaction B
/// writes the pick tasks (<c>wms_tasks</c>, <c>Kind = Pick</c>) and their timeline in the same transaction, and the central audit event is
/// enlisted only when an outbound changed (pack §21).
/// </summary>
public sealed class OutboundShipmentRepository : IOutboundShipmentRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<OutboundShipment> _outbounds;
    private readonly IMongoCollection<OutboundShipmentEvent> _events;
    private readonly IMongoCollection<WarehouseTask> _tasks;
    private readonly IMongoCollection<WarehouseTaskEvent> _taskEvents;
    private readonly IMongoCollection<HandlingUnit> _packages;
    private readonly IMongoCollection<WarehouseOutboxMessage> _outbox;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public OutboundShipmentRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant)
    {
        _client = client;
        _outbounds = database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);
        _events = database.GetCollection<OutboundShipmentEvent>(WarehouseCollections.OutboundEvents);
        _tasks = database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);
        _taskEvents = database.GetCollection<WarehouseTaskEvent>(WarehouseCollections.TaskEvents);
        _packages = database.GetCollection<HandlingUnit>(WarehouseCollections.HandlingUnits);
        _outbox = database.GetCollection<WarehouseOutboxMessage>(WarehouseCollections.Outbox);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Company<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<OutboundShipment?> FindAsync(Guid outboundId, CancellationToken cancellationToken = default)
        => await _outbounds.Find(Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.Id, outboundId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<OutboundShipment?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _outbounds.Find(Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.CreatedBy, createdBy)
                                 & Builders<OutboundShipment>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<OutboundShipmentPage> ListAsync(OutboundShipmentQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<OutboundShipment>.Filter;
        var filter = Company<OutboundShipment>() & f.In(x => x.Status, query.Statuses);
        if (query.WarehouseId is { } warehouse) filter &= f.Eq(x => x.WarehouseId, warehouse);
        // DL-5 (pack §8.24 rule 4): the frozen list never carries an Item-level outbound (no line is Item — a mix is refused at creation)
        if (query.ExcludeItemLevel) filter &= new BsonDocument("Lines.SkuLevel", new BsonDocument("$ne", nameof(Domain.Enums.SkuLevel.Item)));
        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.OutboundNumber, pattern), f.Regex(x => x.OrderRef, pattern));
        }

        var sort = query.OldestFirst
            ? Builders<OutboundShipment>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.OutboundNumber)
            : Builders<OutboundShipment>.Sort.Descending(x => x.CreatedAt).Descending(x => x.OutboundNumber);
        var total = await _outbounds.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _outbounds.Find(filter).Sort(sort).Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new OutboundShipmentPage(items, total);
    }

    public async Task<OutboundEventPage> EventsAsync(Guid outboundId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<OutboundShipmentEvent>() & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.OutboundId, outboundId);
        var total = await _events.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _events.Find(filter).SortBy(x => x.Seq).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new OutboundEventPage(items, total);
    }

    public async Task<OutboundShipmentEvent?> FindKeyEventAsync(Guid outboundId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<OutboundShipmentEvent>() & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.OutboundId, outboundId)
                              & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseTask>> PickTasksAsync(Guid outboundId, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.OutboundId, outboundId)
                             & Builders<WarehouseTask>.Filter.Eq(x => x.Kind, WarehouseTaskKind.Pick)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseTask>> PickTasksOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => outboundIds.Count == 0
            ? []
            : await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.In(x => x.OutboundId, outboundIds.Select(i => (Guid?)i))
                                & Builders<WarehouseTask>.Filter.Eq(x => x.Kind, WarehouseTaskKind.Pick)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HandlingUnit>> PackagesAsync(Guid outboundId, CancellationToken cancellationToken = default)
        => await _packages.Find(Company<HandlingUnit>() & Builders<HandlingUnit>.Filter.Eq(x => x.OutboundId, outboundId))
            .SortBy(x => x.PackageNumber).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HandlingUnit>> PackagesOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => outboundIds.Count == 0
            ? []
            : await _packages.Find(Company<HandlingUnit>() & Builders<HandlingUnit>.Filter.In(x => x.OutboundId, outboundIds))
                .SortBy(x => x.PackageNumber).ToListAsync(cancellationToken);

    public async Task<HandlingUnit?> FindPackageAsync(Guid packageId, CancellationToken cancellationToken = default)
        => await _packages.Find(Company<HandlingUnit>() & Builders<HandlingUnit>.Filter.Eq(x => x.Id, packageId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<HandlingUnit?> FindPackageByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _packages.Find(Company<HandlingUnit>() & Builders<HandlingUnit>.Filter.Eq(x => x.CreatedBy, createdBy)
                                & Builders<HandlingUnit>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseWriteOutcome> InTransactionAsync(Func<IOutboundWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    var outcome = await work(new WriteSession(this, s), ct);
                    // BL-501: the marked command's central-audit event commits with this transaction — and only when it changed an outbound.
                    if (outcome.Affected > 0) await CentralAuditTransaction.EnlistAsync(s, outcome.EntityId, ct);
                    return outcome;
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new OutboundDuplicateException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new OutboundDuplicateException(index, ex);
        }
    }

    internal static OutboundUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(WarehouseIndexConfiguration.OutboundNumberIndex, StringComparison.Ordinal)) return OutboundUniqueIndex.Number;
        if (message.Contains(WarehouseIndexConfiguration.OutboundCreateKeyIndex, StringComparison.Ordinal)) return OutboundUniqueIndex.CreateKey;
        if (message.Contains(WarehouseIndexConfiguration.OutboundEventSeqIndex, StringComparison.Ordinal)
            || message.Contains(WarehouseIndexConfiguration.OutboundEventKeyIndex, StringComparison.Ordinal)) return OutboundUniqueIndex.Event;
        if (message.Contains(WarehouseIndexConfiguration.PackageNumberIndex, StringComparison.Ordinal)
            || message.Contains(WarehouseIndexConfiguration.PackageCreateKeyIndex, StringComparison.Ordinal)) return OutboundUniqueIndex.Package;
        return WarehouseTaskRepository.UniqueIndexOf(category, message) is not null ? OutboundUniqueIndex.Task : null;
    }

    private sealed class WriteSession : IOutboundWriteSession
    {
        private readonly OutboundShipmentRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(OutboundShipmentRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public Task InsertOutboundAsync(OutboundShipment outbound, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(outbound);
            return _owner._outbounds.InsertOneAsync(_session, outbound, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceOutboundAsync(OutboundShipment outbound, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(outbound);
            var filter = _owner.Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.Id, outbound.Id)
                         & Builders<OutboundShipment>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await _owner._outbounds.ReplaceOneAsync(_session, filter, outbound, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertEventsAsync(IReadOnlyList<OutboundShipmentEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return Task.CompletedTask;
            foreach (var e in events) _owner.Stamp(e);
            return _owner._events.InsertManyAsync(_session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task InsertPickTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default)
        {
            if (tasks.Count == 0) return;
            foreach (var t in tasks) _owner.Stamp(t);
            foreach (var e in events) _owner.Stamp(e);
            await _owner._tasks.InsertManyAsync(_session, tasks, new InsertManyOptions { IsOrdered = true }, cancellationToken);
            if (events.Count > 0) await _owner._taskEvents.InsertManyAsync(_session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task<bool> ReplaceTaskAsync(WarehouseTask task, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(task);
            var filter = _owner.Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.Id, task.Id)
                         & Builders<WarehouseTask>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await _owner._tasks.ReplaceOneAsync(_session, filter, task, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default)
            => InsertPickTasksAsync(tasks, events, cancellationToken);

        public Task InsertPackageAsync(HandlingUnit package, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(package);
            return _owner._packages.InsertOneAsync(_session, package, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplacePackageAsync(HandlingUnit package, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(package);
            var filter = _owner.Company<HandlingUnit>() & Builders<HandlingUnit>.Filter.Eq(x => x.Id, package.Id)
                         & Builders<HandlingUnit>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await _owner._packages.ReplaceOneAsync(_session, filter, package, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertOutboxAsync(WarehouseOutboxMessage message, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(message);
            return _owner._outbox.InsertOneAsync(_session, message, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// The maintenance service's cross-tenant scan (pack §8.17 step 6, §4.6 <c>(TenantId, Status, AllocationLeaseUntilUtc)</c>) — the one deliberate
/// outbound read without a tenant predicate, used ONLY by the hosted service; each row is processed afterwards in its own Tenant + company
/// scope with the outbound's creator as actor. Read-only.
/// </summary>
public sealed class OutboundMaintenanceScan : IOutboundMaintenanceScan
{
    private readonly IMongoCollection<OutboundShipment> _outbounds;

    public OutboundMaintenanceScan(IMongoDatabase database) => _outbounds = database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);

    public async Task<IReadOnlyList<OutboundMaintenanceRow>> ExpiredAllocationsAsync(DateTimeOffset now, (Guid TenantId, Guid OutboundId)? after, int limit,
        IReadOnlyCollection<Guid>? onlyTenants = null, CancellationToken cancellationToken = default)
    {
        var f = Builders<OutboundShipment>.Filter;
        var filter = f.Eq(x => x.Status, OutboundStatus.Allocating) & f.Lt(x => x.AllocationLeaseUntilUtc, now);
        if (onlyTenants is not null) filter &= f.In(x => x.TenantId, onlyTenants.Select(t => (Guid?)t));
        if (after is { } cursor)
        {
            var tenant = cursor.TenantId.ToString("D");
            var id = cursor.OutboundId.ToString("D");
            filter &= new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("TenantId", new BsonDocument("$gt", tenant)),
                new BsonDocument { ["TenantId"] = tenant, ["_id"] = new BsonDocument("$gt", id) }
            });
        }

        var rows = await _outbounds.Find(filter)
            .Sort(Builders<OutboundShipment>.Sort.Ascending(x => x.TenantId).Ascending(x => x.Id))
            .Limit(limit)
            .ToListAsync(cancellationToken);
        return rows.Where(r => r.TenantId is not null && r.LegalEntityId is not null)
            .Select(r => new OutboundMaintenanceRow(r.Id, r.TenantId!.Value, r.LegalEntityId!.Value, r.CreatedBy, r.CreatedByName))
            .ToList();
    }

    public async Task<IReadOnlyList<OutboundMaintenanceRow>> PendingReleasesAsync((Guid TenantId, Guid OutboundId)? after, int limit,
        IReadOnlyCollection<Guid>? onlyTenants = null, CancellationToken cancellationToken = default)
    {
        var f = Builders<OutboundShipment>.Filter;
        FilterDefinition<OutboundShipment> filter = new BsonDocument("Lines.Allocations.ReservationReleaseState", nameof(ReservationReleaseState.Pending));
        filter &= f.In(x => x.Status, [OutboundStatus.AllocationFailed, OutboundStatus.Shipped, OutboundStatus.Cancelled]);
        if (onlyTenants is not null) filter &= f.In(x => x.TenantId, onlyTenants.Select(t => (Guid?)t));
        if (after is { } cursor)
        {
            var tenant = cursor.TenantId.ToString("D");
            var id = cursor.OutboundId.ToString("D");
            filter &= new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("TenantId", new BsonDocument("$gt", tenant)),
                new BsonDocument { ["TenantId"] = tenant, ["_id"] = new BsonDocument("$gt", id) }
            });
        }

        var rows = await _outbounds.Find(filter)
            .Sort(Builders<OutboundShipment>.Sort.Ascending(x => x.TenantId).Ascending(x => x.Id))
            .Limit(limit)
            .ToListAsync(cancellationToken);
        return rows.Where(r => r.TenantId is not null && r.LegalEntityId is not null)
            .Select(r => new OutboundMaintenanceRow(r.Id, r.TenantId!.Value, r.LegalEntityId!.Value, r.CreatedBy, r.CreatedByName))
            .ToList();
    }
}
