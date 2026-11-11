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
/// MOD-0178 tasks on MongoDB (the role repository's pattern). Every query starts from the caller's Tenant + LegalEntity — there is no
/// overload without the scope (K3 sabotage (d) / (p): drop the company / tenant predicate → the isolation tests are red). Each command runs
/// in ONE multi-document transaction; every task write is a compare-and-set on <c>Version</c> (pack §8.3 "Sürüm koşulu"), the timeline rows
/// are INSERT-ONLY, and the central audit event is enlisted only when a task changed (pack §8.8).
/// </summary>
public sealed class WarehouseTaskRepository : IWarehouseTaskRepository
{
    private static readonly WarehouseTaskStatus[] OpenStatuses = [WarehouseTaskStatus.Open, WarehouseTaskStatus.InProgress];

    private readonly IMongoClient _client;
    private readonly IMongoCollection<WarehouseTask> _tasks;
    private readonly IMongoCollection<WarehouseTaskEvent> _events;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WarehouseTaskRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant)
    {
        _client = client;
        _tasks = database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);
        _events = database.GetCollection<WarehouseTaskEvent>(WarehouseCollections.TaskEvents);
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

    // DL-4: the S2 pick tasks share wms_tasks; the S1 putaway surface reads PUTAWAY tasks only — a pick task id is unknown to it (404)
    private static FilterDefinition<WarehouseTask> Putaway() => Builders<WarehouseTask>.Filter.Eq(x => x.Kind, WarehouseTaskKind.Putaway);

    public async Task<WarehouseTask?> FindAsync(Guid taskId, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Putaway() & Builders<WarehouseTask>.Filter.Eq(x => x.Id, taskId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<WarehouseTask?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.CreatedBy, createdBy)
                             & Builders<WarehouseTask>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseTask>> FindManyAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Putaway() & Builders<WarehouseTask>.Filter.In(x => x.Id, taskIds)).ToListAsync(cancellationToken);

    // DL-5: the pick surface names its kind explicitly (the S1 members above stay putaway-only)
    public async Task<WarehouseTask?> FindOfKindAsync(Guid taskId, WarehouseTaskKind kind, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.Kind, kind) & Builders<WarehouseTask>.Filter.Eq(x => x.Id, taskId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseTask>> FindManyOfKindAsync(IReadOnlyCollection<Guid> taskIds, WarehouseTaskKind kind, CancellationToken cancellationToken = default)
        => await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.Kind, kind) & Builders<WarehouseTask>.Filter.In(x => x.Id, taskIds))
            .ToListAsync(cancellationToken);

    public async Task<WarehouseTaskPage> ListAsync(WarehouseTaskQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<WarehouseTask>.Filter;
        var filter = Company<WarehouseTask>() & f.Eq(x => x.Kind, query.Kind);
        if (query.OutboundId is { } outbound) filter &= f.Eq(x => x.OutboundId, outbound);
        if (query.Statuses.Count > 0) filter &= f.In(x => x.Status, query.Statuses);
        if (query.WarehouseId is { } warehouse) filter &= f.Eq(x => x.WarehouseId, warehouse);
        if (query.AssigneeUserId is { } assignee) filter &= f.Eq(x => x.AssigneeUserId, assignee);
        if (query.Priority is { } priority) filter &= f.Eq(x => x.Priority, priority);
        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.Number, pattern), f.Regex(x => x.LotNumber, pattern));
        }

        var sort = query.OldestFirst
            ? Builders<WarehouseTask>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Number)
            : Builders<WarehouseTask>.Sort.Descending(x => x.CreatedAt).Descending(x => x.Number);
        var total = await _tasks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _tasks.Find(filter).Sort(sort).Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new WarehouseTaskPage(items, total);
    }

    public async Task<IReadOnlyList<WarehouseTask>> OpenTasksFromAsync(IReadOnlyCollection<Guid> fromLocationIds, CancellationToken cancellationToken = default)
        => fromLocationIds.Count == 0
            ? []
            : await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.In(x => x.FromLocationId, fromLocationIds)
                                & Builders<WarehouseTask>.Filter.In(x => x.Status, OpenStatuses)).ToListAsync(cancellationToken);

    public async Task<WarehouseTaskEventPage> EventsAsync(Guid taskId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<WarehouseTaskEvent>() & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.TaskId, taskId);
        var total = await _events.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _events.Find(filter).SortBy(x => x.Seq).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new WarehouseTaskEventPage(items, total);
    }

    public async Task<WarehouseTaskEvent?> FindKeyEventAsync(Guid taskId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<WarehouseTaskEvent>() & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.TaskId, taskId)
                              & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseTaskEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<WarehouseTaskEvent>() & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<WarehouseTaskEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).ToListAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseTaskWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    var outcome = await work(new WriteSession(this, s), ct);
                    // BL-501: the marked command's central-audit event commits with this transaction — and only when it changed a task.
                    if (outcome.Affected > 0) await CentralAuditTransaction.EnlistAsync(s, outcome.EntityId, ct);
                    return outcome;
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new WarehouseTaskDuplicateException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new WarehouseTaskDuplicateException(index, ex);
        }
    }

    internal static WarehouseTaskUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(WarehouseIndexConfiguration.TaskNumberIndex, StringComparison.Ordinal)) return WarehouseTaskUniqueIndex.Number;
        if (message.Contains(WarehouseIndexConfiguration.TaskCreateKeyIndex, StringComparison.Ordinal)) return WarehouseTaskUniqueIndex.CreateKey;
        if (message.Contains(WarehouseIndexConfiguration.TaskSerialSlotIndex, StringComparison.Ordinal)) return WarehouseTaskUniqueIndex.SerialSlot;
        return message.Contains(WarehouseIndexConfiguration.TaskEventSeqIndex, StringComparison.Ordinal)
               || message.Contains(WarehouseIndexConfiguration.TaskEventKeyIndex, StringComparison.Ordinal)
            ? WarehouseTaskUniqueIndex.Event
            : null;
    }

    private sealed class WriteSession : IWarehouseTaskWriteSession
    {
        private readonly WarehouseTaskRepository _owner;
        private readonly IClientSessionHandle _session;

        // ── DL-5: the pick task's outbound, in the same transaction ──

        private IMongoCollection<OutboundShipment> Outbounds => _owner._tasks.Database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);

        private IMongoCollection<OutboundShipmentEvent> OutboundEvents => _owner._tasks.Database.GetCollection<OutboundShipmentEvent>(WarehouseCollections.OutboundEvents);

        public async Task<OutboundShipment?> FindOutboundAsync(Guid outboundId, CancellationToken cancellationToken = default)
            => await Outbounds.Find(_session, _owner.Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.Id, outboundId))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<WarehouseTask>> PickTasksOfAsync(Guid outboundId, CancellationToken cancellationToken = default)
            => await _owner._tasks.Find(_session, _owner.Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.OutboundId, outboundId)
                                                  & Builders<WarehouseTask>.Filter.Eq(x => x.Kind, WarehouseTaskKind.Pick)).ToListAsync(cancellationToken);

        public async Task<bool> ReplaceOutboundAsync(OutboundShipment outbound, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(outbound);
            var filter = _owner.Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.Id, outbound.Id)
                         & Builders<OutboundShipment>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await Outbounds.ReplaceOneAsync(_session, filter, outbound, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task TouchOutboundAsync(Guid outboundId, CancellationToken cancellationToken = default)
            => Outbounds.UpdateOneAsync(_session, _owner.Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.Id, outboundId),
                Builders<OutboundShipment>.Update.Inc(x => x.PickProgress, 1), cancellationToken: cancellationToken);

        public Task InsertOutboundEventsAsync(IReadOnlyList<OutboundShipmentEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return Task.CompletedTask;
            foreach (var e in events) _owner.Stamp(e);
            return OutboundEvents.InsertManyAsync(_session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public WriteSession(WarehouseTaskRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public Task InsertTaskAsync(WarehouseTask task, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(task);
            return _owner._tasks.InsertOneAsync(_session, task, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceTaskAsync(WarehouseTask task, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(task);
            var filter = _owner.Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.Eq(x => x.Id, task.Id)
                         & Builders<WarehouseTask>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await _owner._tasks.ReplaceOneAsync(_session, filter, task, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertEventsAsync(IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return Task.CompletedTask;
            foreach (var e in events) _owner.Stamp(e);
            return _owner._events.InsertManyAsync(_session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }
    }
}

/// <summary>
/// The reconciler's cross-tenant scan (pack §8.3 "Seçim", §4.6 sweeper indexes) — the one deliberate read without a tenant predicate,
/// used ONLY by the hosted service; each row is processed afterwards in its own Tenant + company scope. Read-only.
/// </summary>
public sealed class WarehousePendingScan : IWarehousePendingScan
{
    private readonly IMongoCollection<WarehouseTask> _tasks;

    public WarehousePendingScan(IMongoDatabase database) => _tasks = database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);

    public Task<IReadOnlyList<PendingScanRow>> PendingAsync(DateTimeOffset olderThan, (Guid TenantId, Guid TaskId)? after, int limit,
        IReadOnlyCollection<Guid>? onlyTenants = null, CancellationToken cancellationToken = default)
        => ScanAsync(Builders<WarehouseTask>.Filter.ElemMatch(x => x.Confirmations,
            Builders<TaskConfirmation>.Filter.Eq(c => c.Status, ConfirmationStatus.Pending) & Builders<TaskConfirmation>.Filter.Lte(c => c.LastAttemptAtUtc, olderThan)),
            c => c.Status == ConfirmationStatus.Pending, after, limit, onlyTenants, cancellationToken);

    public Task<IReadOnlyList<PendingScanRow>> DefensiveAsync(DateTimeOffset since, (Guid TenantId, Guid TaskId)? after, int limit,
        IReadOnlyCollection<Guid>? onlyTenants = null, CancellationToken cancellationToken = default)
        => ScanAsync(Builders<WarehouseTask>.Filter.ElemMatch(x => x.Confirmations,
            Builders<TaskConfirmation>.Filter.Eq(c => c.Status, ConfirmationStatus.Refused) & Builders<TaskConfirmation>.Filter.Eq(c => c.RefusedOnRetry, true)
            & Builders<TaskConfirmation>.Filter.In(c => c.RefusalSource, [RefusalSource.PreCheck, RefusalSource.Ledger4xx])
            & Builders<TaskConfirmation>.Filter.Gte(c => c.RefusedAtUtc, since)),
            c => c.Status == ConfirmationStatus.Refused && c.RefusedOnRetry, after, limit, onlyTenants, cancellationToken);

    private async Task<IReadOnlyList<PendingScanRow>> ScanAsync(FilterDefinition<WarehouseTask> match, Func<TaskConfirmation, bool> actorOf,
        (Guid TenantId, Guid TaskId)? after, int limit, IReadOnlyCollection<Guid>? onlyTenants, CancellationToken ct)
    {
        var filter = match;
        if (onlyTenants is not null) filter &= Builders<WarehouseTask>.Filter.In(x => x.TenantId, onlyTenants.Select(t => (Guid?)t));
        if (after is { } cursor)
        {
            // (TenantId, _id) order, strictly after the cursor; ids are stored as strings
            var tenant = cursor.TenantId.ToString("D");
            var id = cursor.TaskId.ToString("D");
            filter &= new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("TenantId", new BsonDocument("$gt", tenant)),
                new BsonDocument { ["TenantId"] = tenant, ["_id"] = new BsonDocument("$gt", id) }
            });
        }

        var rows = await _tasks.Find(filter)
            .Sort(Builders<WarehouseTask>.Sort.Ascending(x => x.TenantId).Ascending(x => x.Id))
            .Limit(limit)
            .ToListAsync(ct);
        return rows.Where(r => r.TenantId is not null && r.LegalEntityId is not null)
            .Select(r => (Task: r, Confirmation: r.Confirmations.FirstOrDefault(actorOf)))
            .Where(r => r.Confirmation is not null)
            .Select(r => new PendingScanRow(r.Task.Id, r.Task.TenantId!.Value, r.Task.LegalEntityId!.Value, r.Confirmation!.ConfirmedBy, r.Confirmation.ConfirmedByName))
            .ToList();
    }
}
