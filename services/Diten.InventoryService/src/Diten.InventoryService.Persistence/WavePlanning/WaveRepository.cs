using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.WavePlanning;

/// <summary>
/// MOD-0180 waves on MongoDB (the MOD-0181 count-repository pattern). Every query starts from the caller's Tenant + LegalEntity — there is no
/// overload without the scope (AC-3 sabotage: drop the tenant / company predicate → the isolation tests are red). Each command runs in ONE
/// multi-document transaction; every wave write is a compare-and-set on <c>Version</c>; the timeline rows are insert-only; a membership is
/// closed, never deleted; the central audit event is enlisted only when a record changed (pack §8.13). Only the <c>wms_wave_*</c> collections
/// are written here — MOD-0178's are not touched (pack §2.3 rule 2).
/// </summary>
public sealed class WaveRepository : IWaveRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Wave> _waves;
    private readonly IMongoCollection<WaveOrder> _orders;
    private readonly IMongoCollection<WaveEvent> _events;
    private readonly IMongoCollection<WaveWarehouseSettings> _settings;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WaveRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant)
    {
        _client = client;
        _waves = database.GetCollection<Wave>(WaveCollections.Waves);
        _orders = database.GetCollection<WaveOrder>(WaveCollections.Orders);
        _events = database.GetCollection<WaveEvent>(WaveCollections.Events);
        _settings = database.GetCollection<WaveWarehouseSettings>(WaveCollections.Settings);
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

    public async Task<Wave?> FindAsync(Guid waveId, CancellationToken cancellationToken = default)
        => await _waves.Find(Company<Wave>() & Builders<Wave>.Filter.Eq(x => x.Id, waveId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<Wave?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _waves.Find(Company<Wave>() & Builders<Wave>.Filter.Eq(x => x.CreatedBy, createdBy)
                             & Builders<Wave>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<WavePage> ListAsync(WaveQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<Wave>.Filter;
        var filter = Company<Wave>() & f.Eq(x => x.WarehouseId, query.WarehouseId);
        if (query.Statuses.Count > 0) filter &= f.In(x => x.Status, query.Statuses);
        if (query.WaveType is { } type) filter &= f.Eq(x => x.WaveType, type);
        if (query.Search is not null) filter &= f.Regex(x => x.Number, new BsonRegularExpression(Regex.Escape(query.Search), "i"));
        if (query.CutoffFrom is { } from) filter &= f.Gte(x => x.CutoffAtUtc, from);
        if (query.CutoffTo is { } to) filter &= f.Lte(x => x.CutoffAtUtc, to);

        var total = await _waves.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _waves.Find(filter).Sort(Builders<Wave>.Sort.Ascending(x => x.CutoffAtUtc).Ascending(x => x.Number))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new WavePage(items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, WaveTotals>> TotalsAsync(IReadOnlyCollection<Guid> waveIds, CancellationToken cancellationToken = default)
    {
        if (waveIds.Count == 0) return new Dictionary<Guid, WaveTotals>();
        var active = await _orders.Find(Company<WaveOrder>() & Builders<WaveOrder>.Filter.In(x => x.WaveId, waveIds)
                                        & Builders<WaveOrder>.Filter.Eq(x => x.Active, true)).ToListAsync(cancellationToken);
        return active.GroupBy(o => o.WaveId)
            .ToDictionary(g => g.Key, g => new WaveTotals(g.Count(), g.Sum(o => o.LineCount), g.Sum(o => o.PlannedTaskCount)));
    }

    public async Task<WaveOrderPage> OrdersAsync(Guid waveId, IReadOnlyCollection<WaveOrderState> states, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = Company<WaveOrder>() & Builders<WaveOrder>.Filter.Eq(x => x.WaveId, waveId);
        if (states.Count > 0) filter &= Builders<WaveOrder>.Filter.In(x => x.State, states);
        var total = await _orders.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _orders.Find(filter)
            .Sort(Builders<WaveOrder>.Sort.Ascending(x => x.AddedAt).Ascending(x => x.OutboundNumber).Ascending(x => x.Id))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new WaveOrderPage(items, total);
    }

    public async Task<IReadOnlyList<WaveOrder>> ActiveOrdersAsync(Guid waveId, CancellationToken cancellationToken = default)
        => await _orders.Find(Company<WaveOrder>() & Builders<WaveOrder>.Filter.Eq(x => x.WaveId, waveId)
                              & Builders<WaveOrder>.Filter.Eq(x => x.Active, true)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WaveOrder>> MembershipsAsync(Guid waveId, IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => outboundIds.Count == 0
            ? []
            : await _orders.Find(Company<WaveOrder>() & Builders<WaveOrder>.Filter.Eq(x => x.WaveId, waveId)
                                 & Builders<WaveOrder>.Filter.In(x => x.OutboundId, outboundIds))
                .Sort(Builders<WaveOrder>.Sort.Descending(x => x.AddedAt).Descending(x => x.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WaveOrder>> ActiveMembershipsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => outboundIds.Count == 0
            ? []
            : await _orders.Find(Company<WaveOrder>() & Builders<WaveOrder>.Filter.In(x => x.OutboundId, outboundIds)
                                 & Builders<WaveOrder>.Filter.Eq(x => x.Active, true)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ActiveOutboundIdsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => await _orders.Find(Company<WaveOrder>() & Builders<WaveOrder>.Filter.Eq(x => x.WarehouseId, warehouseId)
                              & Builders<WaveOrder>.Filter.Eq(x => x.Active, true)).Project(x => x.OutboundId).ToListAsync(cancellationToken);

    public async Task<long> ReleasingOrderCountAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var releasing = await _waves.Find(Company<Wave>() & Builders<Wave>.Filter.Eq(x => x.WarehouseId, warehouseId)
                                          & Builders<Wave>.Filter.Eq(x => x.Status, WaveStatus.Releasing)).Project(x => x.Id).ToListAsync(cancellationToken);
        if (releasing.Count == 0) return 0;
        return await _orders.CountDocumentsAsync(Company<WaveOrder>() & Builders<WaveOrder>.Filter.In(x => x.WaveId, releasing)
                                                 & Builders<WaveOrder>.Filter.Eq(x => x.State, WaveOrderState.Planned), cancellationToken: cancellationToken);
    }

    public async Task<WaveEventPage> EventsAsync(Guid streamId, WaveEventStream stream, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Company<WaveEvent>() & Builders<WaveEvent>.Filter.Eq(x => x.WaveId, streamId) & Builders<WaveEvent>.Filter.Eq(x => x.Stream, stream);
        var total = await _events.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _events.Find(filter).SortBy(x => x.Seq).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new WaveEventPage(items, total);
    }

    public async Task<WaveEvent?> FindKeyEventAsync(Guid streamId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<WaveEvent>() & Builders<WaveEvent>.Filter.Eq(x => x.WaveId, streamId)
                              & Builders<WaveEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<WaveEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<WaveEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<WaveWarehouseSettings?> FindSettingsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => await _settings.Find(Company<WaveWarehouseSettings>() & Builders<WaveWarehouseSettings>.Filter.Eq(x => x.WarehouseId, warehouseId))
            .FirstOrDefaultAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WaveWriteOutcome> InTransactionAsync(Func<IWaveWriteSession, CancellationToken, Task<WaveWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    var outcome = await work(new WriteSession(this, s), ct);
                    // BL-501: the marked command's central-audit event commits with this transaction — and only when it changed a record.
                    if (outcome.Affected > 0) await CentralAuditTransaction.EnlistAsync(s, outcome.EntityId, ct);
                    return outcome;
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new WaveDuplicateException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new WaveDuplicateException(index, ex);
        }
    }

    internal static WaveUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(WaveIndexConfiguration.ActiveOrderIndex, StringComparison.Ordinal)) return WaveUniqueIndex.ActiveOrder;
        if (message.Contains(WaveIndexConfiguration.NumberIndex, StringComparison.Ordinal)) return WaveUniqueIndex.Number;
        if (message.Contains(WaveIndexConfiguration.CreateKeyIndex, StringComparison.Ordinal)) return WaveUniqueIndex.CreateKey;
        if (message.Contains(WaveIndexConfiguration.SettingsWarehouseIndex, StringComparison.Ordinal)) return WaveUniqueIndex.Settings;
        return message.Contains(WaveIndexConfiguration.EventSeqIndex, StringComparison.Ordinal)
               || message.Contains(WaveIndexConfiguration.EventKeyIndex, StringComparison.Ordinal)
            ? WaveUniqueIndex.Event
            : null;
    }

    private sealed class WriteSession(WaveRepository owner, IClientSessionHandle session) : IWaveWriteSession
    {
        public Task InsertWaveAsync(Wave wave, CancellationToken cancellationToken = default)
        {
            owner.Stamp(wave);
            return owner._waves.InsertOneAsync(session, wave, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceWaveAsync(Wave wave, int expectedVersion, CancellationToken cancellationToken = default)
        {
            owner.Stamp(wave);
            var filter = owner.Company<Wave>() & Builders<Wave>.Filter.Eq(x => x.Id, wave.Id) & Builders<Wave>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await owner._waves.ReplaceOneAsync(session, filter, wave, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertOrdersAsync(IReadOnlyList<WaveOrder> orders, CancellationToken cancellationToken = default)
        {
            if (orders.Count == 0) return Task.CompletedTask;
            foreach (var o in orders) owner.Stamp(o);
            return owner._orders.InsertManyAsync(session, orders, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task<long> RemovePlannedOrdersAsync(Guid waveId, IReadOnlyCollection<Guid>? outboundIds, Guid closedBy, DateTimeOffset at, string? reason,
            CancellationToken cancellationToken = default)
        {
            var f = Builders<WaveOrder>.Filter;
            var filter = owner.Company<WaveOrder>() & f.Eq(x => x.WaveId, waveId) & f.Eq(x => x.State, WaveOrderState.Planned);
            if (outboundIds is not null) filter &= f.In(x => x.OutboundId, outboundIds);
            var update = Builders<WaveOrder>.Update.Set(x => x.State, WaveOrderState.Removed).Set(x => x.Active, false).Set(x => x.ClosedBy, closedBy)
                .Set(x => x.ClosedAt, at).Set(x => x.CloseReason, reason).Set(x => x.UpdatedAt, at).Inc(x => x.Version, 1);
            var result = await owner._orders.UpdateManyAsync(session, filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount;
        }

        public Task InsertEventsAsync(IReadOnlyList<WaveEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return Task.CompletedTask;
            foreach (var e in events) owner.Stamp(e);
            return owner._events.InsertManyAsync(session, events, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public Task InsertSettingsAsync(WaveWarehouseSettings settings, CancellationToken cancellationToken = default)
        {
            owner.Stamp(settings);
            return owner._settings.InsertOneAsync(session, settings, cancellationToken: cancellationToken);
        }

        public async Task<bool> UpdateSettingsAsync(WaveWarehouseSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
        {
            var f = Builders<WaveWarehouseSettings>.Filter;
            var filter = owner.Company<WaveWarehouseSettings>() & f.Eq(x => x.Id, settings.Id) & f.Eq(x => x.Version, expectedVersion);
            var update = Builders<WaveWarehouseSettings>.Update.Set(x => x.WaveManaged, settings.WaveManaged).Set(x => x.TimeZoneId, settings.TimeZoneId)
                .Set(x => x.RiskMinutesBeforeCutoff, settings.RiskMinutesBeforeCutoff).Set(x => x.RiskProgressPercent, settings.RiskProgressPercent)
                .Set(x => x.PendingTaskMinutes, settings.PendingTaskMinutes).Set(x => x.EventSeq, settings.EventSeq).Set(x => x.UpdatedAt, settings.UpdatedAt)
                .Set(x => x.Version, settings.Version);
            var result = await owner._settings.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public async Task PassGateAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        {
            var f = Builders<WaveWarehouseSettings>.Filter;
            var filter = owner.Company<WaveWarehouseSettings>() & f.Eq(x => x.WarehouseId, warehouseId) & f.Eq(x => x.WaveManaged, true);
            var result = await owner._settings.UpdateOneAsync(session, filter, Builders<WaveWarehouseSettings>.Update.Inc(x => x.GateSeq, 1),
                cancellationToken: cancellationToken);
            if (result.MatchedCount != 1) throw new WaveGateClosedException();
        }
    }
}
