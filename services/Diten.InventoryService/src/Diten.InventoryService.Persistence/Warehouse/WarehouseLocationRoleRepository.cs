using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// MOD-0178 location roles on MongoDB (the MOD-0177 <c>RecallRepository</c> pattern). Every query starts from the caller's Tenant + LegalEntity
/// — there is no overload without the scope (K3 sabotage (d) / (p): drop the company / tenant predicate → the isolation tests are red). Live
/// reads add <c>IsDeleted = false</c>. Each command runs in ONE multi-document transaction; the history rows are INSERT-ONLY (the write session
/// only calls <c>InsertOne</c> on them). The central audit event is enlisted in the same transaction, and only when the business write changed
/// a record (pack §8.8: a 0-record attempt — a stale compare-and-set — leaves neither a history row nor a central-audit row).
/// </summary>
public sealed class WarehouseLocationRoleRepository : IWarehouseLocationRoleRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<WarehouseLocationRole> _roles;
    private readonly IMongoCollection<WarehouseLocationRoleEvent> _events;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WarehouseLocationRoleRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant)
    {
        _client = client;
        _roles = database.GetCollection<WarehouseLocationRole>(WarehouseCollections.LocationRoles);
        _events = database.GetCollection<WarehouseLocationRoleEvent>(WarehouseCollections.LocationRoleEvents);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    // ── scope ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Tenant + company, deleted records included (the idempotency lookups).</summary>
    private FilterDefinition<T> Company<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId);

    private FilterDefinition<WarehouseLocationRole> Live() => Company<WarehouseLocationRole>() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.IsDeleted, false);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseLocationRole?> FindAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _roles.Find(Live() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.Id, roleId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<WarehouseLocationRole?> FindIncludingDeletedAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _roles.Find(Company<WarehouseLocationRole>() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.Id, roleId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseLocationRole>> FindManyAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
        => await _roles.Find(Live() & Builders<WarehouseLocationRole>.Filter.In(x => x.Id, roleIds)).ToListAsync(cancellationToken);

    public async Task<WarehouseLocationRole?> FindByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => await _roles.Find(Live() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.LocationId, locationId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseLocationRole>> FindByLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default)
        => locationIds.Count == 0
            ? []
            : await _roles.Find(Live() & Builders<WarehouseLocationRole>.Filter.In(x => x.LocationId, locationIds)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseLocationRole>> FindWithRoleAsync(WarehouseRole role, CancellationToken cancellationToken = default)
        => await _roles.Find(Live() & Builders<WarehouseLocationRole>.Filter.AnyEq(x => x.Roles, role)).ToListAsync(cancellationToken);

    public async Task<WarehouseLocationRolePage> ListAsync(WarehouseLocationRoleQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<WarehouseLocationRole>.Filter;
        var filter = Live();
        if (query.WarehouseId is { } warehouse) filter &= f.Eq(x => x.WarehouseId, warehouse);
        if (query.Role is { } role) filter &= f.AnyEq(x => x.Roles, role);
        var total = await _roles.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _roles.Find(filter)
            .Sort(Builders<WarehouseLocationRole>.Sort.Descending(x => x.CreatedAt).Ascending(x => x.Id))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new WarehouseLocationRolePage(items, total);
    }

    public async Task<WarehouseLocationRole?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _roles.Find(Company<WarehouseLocationRole>() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.CreatedBy, createdBy)
                             & Builders<WarehouseLocationRole>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<WarehouseLocationRoleEvent?> FindKeyEventAsync(Guid roleId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<WarehouseLocationRoleEvent>() & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.RoleId, roleId)
                              & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseLocationRoleEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await _events.Find(Company<WarehouseLocationRoleEvent>() & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.ActorUserId, actorUserId)
                              & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.RequestOp, requestOp)
                              & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseLocationRoleEvent>> HistoryAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _events.Find(Company<WarehouseLocationRoleEvent>() & Builders<WarehouseLocationRoleEvent>.Filter.Eq(x => x.RoleId, roleId))
            .SortBy(x => x.Seq).ToListAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseLocationRoleWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
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
            throw new WarehouseLocationRoleDuplicateException(index, ex);
        }
    }

    internal static WarehouseLocationRoleUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(WarehouseIndexConfiguration.LocationIndex, StringComparison.Ordinal)) return WarehouseLocationRoleUniqueIndex.Location;
        if (message.Contains(WarehouseIndexConfiguration.CreateKeyIndex, StringComparison.Ordinal)) return WarehouseLocationRoleUniqueIndex.CreateKey;
        return message.Contains(WarehouseIndexConfiguration.EventSeqIndex, StringComparison.Ordinal)
               || message.Contains(WarehouseIndexConfiguration.EventKeyIndex, StringComparison.Ordinal)
            ? WarehouseLocationRoleUniqueIndex.Event
            : null;
    }

    private sealed class WriteSession : IWarehouseLocationRoleWriteSession
    {
        private readonly WarehouseLocationRoleRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(WarehouseLocationRoleRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public Task InsertRoleAsync(WarehouseLocationRole role, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(role);
            return _owner._roles.InsertOneAsync(_session, role, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceRoleAsync(WarehouseLocationRole role, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(role);
            var filter = _owner.Live() & Builders<WarehouseLocationRole>.Filter.Eq(x => x.Id, role.Id)
                         & Builders<WarehouseLocationRole>.Filter.Eq(x => x.Version, expectedVersion);
            var result = await _owner._roles.ReplaceOneAsync(_session, filter, role, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertEventAsync(WarehouseLocationRoleEvent roleEvent, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(roleEvent);
            return _owner._events.InsertOneAsync(_session, roleEvent, cancellationToken: cancellationToken);
        }
    }
}
