using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// <see cref="IWarehouseMonitoringReads"/> on MongoDB (pack §8.23, DL-7) — READ-ONLY (<c>Find</c> / <c>Count</c> only; nothing is stored for S3).
/// Every query starts from the caller's Tenant + LegalEntity (sabotage: drop the company predicate → the isolation tests are red). The INVENTORY
/// movements of the Activity Trace are read from the ledger's own collection with <c>sourceModule = MOD-0178</c> (the <c>WarehouseStockReads</c>
/// precedent: a read of MOD-0173's projection, never a write).
/// </summary>
public sealed class WarehouseMonitoringReads : IWarehouseMonitoringReads
{
    /// <summary>The <c>wms_outbox</c> status of a delivered row (the transport is not wired yet — pack §8.9; the count is 0 until it is).</summary>
    public const string DeliveredStatus = "Delivered";

    private const string Module = "MOD-0178";

    private readonly IMongoCollection<WarehouseTask> _tasks;
    private readonly IMongoCollection<WarehouseTaskEvent> _taskEvents;
    private readonly IMongoCollection<OutboundShipment> _outbounds;
    private readonly IMongoCollection<OutboundShipmentEvent> _outboundEvents;
    private readonly IMongoCollection<WarehouseOutboxMessage> _outbox;
    private readonly IMongoCollection<InventoryTransaction> _movements;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WarehouseMonitoringReads(IMongoDatabase database, ITenantContext tenant)
    {
        _tasks = database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);
        _taskEvents = database.GetCollection<WarehouseTaskEvent>(WarehouseCollections.TaskEvents);
        _outbounds = database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments);
        _outboundEvents = database.GetCollection<OutboundShipmentEvent>(WarehouseCollections.OutboundEvents);
        _outbox = database.GetCollection<WarehouseOutboxMessage>(WarehouseCollections.Outbox);
        _movements = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Company<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId);

    // ── exceptions ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WarehouseTask>> TasksWithOpenConfirmationsAsync(Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        var filter = Company<WarehouseTask>()
                     & (FilterDefinition<WarehouseTask>)new BsonDocument("Confirmations.Status",
                         new BsonDocument("$in", new BsonArray { nameof(ConfirmationStatus.Pending), nameof(ConfirmationStatus.Refused), nameof(ConfirmationStatus.IntegrityError) }));
        if (warehouseId is { } w) filter &= Builders<WarehouseTask>.Filter.Eq(x => x.WarehouseId, w);
        return await _tasks.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundShipment>> ExceptionOutboundsAsync(Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        var f = Builders<OutboundShipment>.Filter;
        var live = f.In(x => x.Status, [OutboundStatus.Allocating, OutboundStatus.Allocated, OutboundStatus.Picked, OutboundStatus.Packed, OutboundStatus.ReadyToShip]);
        var releasePending = f.In(x => x.Status, [OutboundStatus.Cancelled, OutboundStatus.Shipped])
                             & (FilterDefinition<OutboundShipment>)new BsonDocument("Lines.Allocations.ReservationReleaseState", nameof(ReservationReleaseState.Pending));
        var unresolved = f.Eq(x => x.Status, OutboundStatus.Shipped)
                         & (FilterDefinition<OutboundShipment>)new BsonDocument("ShortDispatchUnresolved.0", new BsonDocument("$exists", true));
        var orphan = f.Eq(x => x.Status, OutboundStatus.AllocationFailed)
                     & (FilterDefinition<OutboundShipment>)new BsonDocument("AllocationFailure.Kind", nameof(AllocationFailureKind.KeyReused));
        var filter = Company<OutboundShipment>() & f.Or(live, releasePending, unresolved, orphan);
        if (warehouseId is { } w) filter &= f.Eq(x => x.WarehouseId, w);
        return await _outbounds.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundShipment>> OutboundsAtStagingAsync(IReadOnlyCollection<Guid> nodes, CancellationToken cancellationToken = default)
        => nodes.Count == 0
            ? []
            : await _outbounds.Find(Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.In(x => x.StagingLocationId, nodes)).ToListAsync(cancellationToken);

    // ── activity ──────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseActivityPage> ActivityAsync(WarehouseActivityQuery query, CancellationToken cancellationToken = default)
    {
        var take = query.Page * query.PageSize;
        var rows = new List<WarehouseActivityRow>();
        long total = 0;
        bool Wants(WarehouseActivitySource source) => query.Source is null || query.Source == source;

        // task timeline rows (putaway and pick)
        if (Wants(WarehouseActivitySource.PutawayTask) || Wants(WarehouseActivitySource.PickTask))
        {
            var ef = Builders<WarehouseTaskEvent>.Filter;
            var filter = Company<WarehouseTaskEvent>() & ef.Gte(x => x.AtUtc, query.FromUtc) & ef.Lte(x => x.AtUtc, query.ToUtc);
            if (query.WarehouseId is not null || query.Source is not null)
            {
                var tf = Company<WarehouseTask>();
                if (query.WarehouseId is { } w) tf &= Builders<WarehouseTask>.Filter.Eq(x => x.WarehouseId, w);
                if (query.Source is { } s)
                    tf &= Builders<WarehouseTask>.Filter.Eq(x => x.Kind, s == WarehouseActivitySource.PickTask ? WarehouseTaskKind.Pick : WarehouseTaskKind.Putaway);
                var ids = await _tasks.Find(tf).Project(x => x.Id).ToListAsync(cancellationToken);
                filter &= ef.In(x => x.TaskId, ids);
            }

            total += await _taskEvents.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var events = await _taskEvents.Find(filter).Sort(Builders<WarehouseTaskEvent>.Sort.Descending(x => x.AtUtc).Descending(x => x.Seq)).Limit(take)
                .ToListAsync(cancellationToken);
            var taskIds = events.Select(e => e.TaskId).Distinct().ToList();
            var tasks = taskIds.Count == 0
                ? new Dictionary<Guid, WarehouseTask>()
                : (await _tasks.Find(Company<WarehouseTask>() & Builders<WarehouseTask>.Filter.In(x => x.Id, taskIds)).ToListAsync(cancellationToken)).ToDictionary(t => t.Id);
            rows.AddRange(events.Select(e =>
            {
                var task = tasks.GetValueOrDefault(e.TaskId);
                var source = task?.Kind == WarehouseTaskKind.Pick ? WarehouseActivitySource.PickTask : WarehouseActivitySource.PutawayTask;
                return new WarehouseActivityRow(source, e.Kind.ToString(), e.AtUtc, e.TaskId, task?.Number, e.Seq, task?.WarehouseId, e.ActorUserId, e.ActorName, e.System, e.Data);
            }));
        }

        // outbound timeline rows (Data never holds a ship-to value — §21)
        if (Wants(WarehouseActivitySource.Outbound))
        {
            var ef = Builders<OutboundShipmentEvent>.Filter;
            var filter = Company<OutboundShipmentEvent>() & ef.Gte(x => x.AtUtc, query.FromUtc) & ef.Lte(x => x.AtUtc, query.ToUtc);
            if (query.WarehouseId is { } w)
            {
                var ids = await _outbounds.Find(Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.Eq(x => x.WarehouseId, w)).Project(x => x.Id)
                    .ToListAsync(cancellationToken);
                filter &= ef.In(x => x.OutboundId, ids);
            }

            total += await _outboundEvents.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var events = await _outboundEvents.Find(filter).Sort(Builders<OutboundShipmentEvent>.Sort.Descending(x => x.AtUtc).Descending(x => x.Seq)).Limit(take)
                .ToListAsync(cancellationToken);
            var outboundIds = events.Select(e => e.OutboundId).Distinct().ToList();
            var heads = outboundIds.Count == 0
                ? new Dictionary<Guid, (string Number, Guid Warehouse)>()
                : (await _outbounds.Find(Company<OutboundShipment>() & Builders<OutboundShipment>.Filter.In(x => x.Id, outboundIds))
                    .Project(x => new { x.Id, x.OutboundNumber, x.WarehouseId }).ToListAsync(cancellationToken))
                .ToDictionary(o => o.Id, o => (o.OutboundNumber, o.WarehouseId));
            rows.AddRange(events.Select(e =>
            {
                var head = heads.TryGetValue(e.OutboundId, out var h) ? h : ((string Number, Guid Warehouse)?)null;
                return new WarehouseActivityRow(WarehouseActivitySource.Outbound, e.Kind.ToString(), e.AtUtc, e.OutboundId, head?.Number, e.Seq, head?.Warehouse,
                    e.ActorUserId, e.ActorName, e.System, e.Data);
            }));
        }

        // INVENTORY movements posted by MOD-0178 (putaway / pick / put-back LOCATION_TRANSFERs)
        if (Wants(WarehouseActivitySource.Movement))
        {
            var mf = Builders<InventoryTransaction>.Filter;
            var filter = Company<InventoryTransaction>() & mf.Eq(x => x.SourceModule, Module) & mf.Gte(x => x.CreatedAt, query.FromUtc) & mf.Lte(x => x.CreatedAt, query.ToUtc);
            if (query.WarehouseId is { } w)
            {
                var text = w.ToString("D");
                filter &= mf.Or(mf.Eq(x => x.FromWarehouseId, text), mf.Eq(x => x.ToWarehouseId, text));
            }

            total += await _movements.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var movements = await _movements.Find(filter).Sort(Builders<InventoryTransaction>.Sort.Descending(x => x.CreatedAt).Descending(x => x.TransactionNumber)).Limit(take)
                .ToListAsync(cancellationToken);
            rows.AddRange(movements.Select(m => new WarehouseActivityRow(WarehouseActivitySource.Movement, WireNames.ToWire(m.MovementType), m.CreatedAt, m.Id,
                m.TransactionNumber, null, Guid.TryParse(m.ToWarehouseId ?? m.FromWarehouseId, out var wh) ? wh : null, null, null, false,
                new Dictionary<string, string?>
                {
                    ["sourceType"] = m.SourceType,
                    ["sourceDocumentId"] = m.SourceDocumentId,
                    ["sourceLineId"] = m.SourceLineId,
                    ["quantity"] = m.BaseQuantity.ToString(CultureInfo.InvariantCulture),
                    ["uomId"] = m.BaseUomId,
                    ["lotId"] = m.LotId,
                    ["fromLocationId"] = m.FromLocationId,
                    ["toLocationId"] = m.ToLocationId
                })));
        }

        var page = rows.OrderByDescending(r => r.AtUtc).ThenBy(r => r.Source).ThenByDescending(r => r.Seq ?? 0)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new WarehouseActivityPage(page, total);
    }

    // ── integration ───────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseIntegrationSnapshot> IntegrationAsync(CancellationToken cancellationToken = default)
    {
        var of = Builders<WarehouseOutboxMessage>.Filter;
        var ready = Company<WarehouseOutboxMessage>() & of.Eq(x => x.EventName, WarehouseOutboxMessage.OutboundShipmentReadyEventName);
        var pendingFilter = ready & of.Eq(x => x.Status, WarehouseOutboxMessage.PendingStatus);
        var pending = await _outbox.CountDocumentsAsync(pendingFilter, cancellationToken: cancellationToken);
        var delivered = await _outbox.CountDocumentsAsync(ready & of.Eq(x => x.Status, DeliveredStatus), cancellationToken: cancellationToken);
        var oldestPending = await _outbox.Find(pendingFilter).SortBy(x => x.OccurredAt).Limit(1).Project(x => (DateTimeOffset?)x.OccurredAt).FirstOrDefaultAsync(cancellationToken);

        var obf = Builders<OutboundShipment>.Filter;
        var readyFilter = Company<OutboundShipment>() & obf.Eq(x => x.Status, OutboundStatus.ReadyToShip);
        var readyCount = await _outbounds.CountDocumentsAsync(readyFilter, cancellationToken: cancellationToken);
        var oldestReady = await _outbounds.Find(readyFilter).SortBy(x => x.ReadyAtUtc).Limit(1).Project(x => x.ReadyAtUtc).FirstOrDefaultAsync(cancellationToken);

        var lastShipped = await _outboundEvents.Find(Company<OutboundShipmentEvent>() & Builders<OutboundShipmentEvent>.Filter.Eq(x => x.Kind, OutboundEventKind.Shipped))
            .SortByDescending(x => x.AtUtc).Limit(1).Project(x => (DateTimeOffset?)x.AtUtc).FirstOrDefaultAsync(cancellationToken);

        var pendingTasks = await _tasks.Find(Company<WarehouseTask>()
                                            & (FilterDefinition<WarehouseTask>)new BsonDocument("Confirmations.Status", nameof(ConfirmationStatus.Pending)))
            .ToListAsync(cancellationToken);
        long Count(WarehouseTaskKind kind) => pendingTasks.Where(t => t.Kind == kind).Sum(t => t.Confirmations.Count(c => c.Status == ConfirmationStatus.Pending));

        return new WarehouseIntegrationSnapshot(pending, delivered, oldestPending, readyCount, oldestReady, lastShipped, Count(WarehouseTaskKind.Putaway),
            Count(WarehouseTaskKind.Pick));
    }
}
