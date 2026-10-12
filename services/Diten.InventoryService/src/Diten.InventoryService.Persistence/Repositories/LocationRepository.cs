using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// MOD-0173-FU02 location store on MongoDB. Every query starts from <see cref="Scope"/> (Tenant + LegalEntity +
/// IsDeleted = false) taken from the resolved request scope — a location of another company does not exist here.
///
/// <para><b>Changes</b> run in ONE multi-document transaction (<see cref="InTransactionAsync{T}"/>): the location
/// document(s), the parent's structure counter and the K-11 history rows commit together or not at all. The ledger
/// posting writes the same location document (<c>StockTouchSeq</c>), so a lifecycle change racing a posting is a Mongo
/// WriteConflict; the driver re-runs the loser's whole body on the committed state, which re-checks stock presence.</para>
///
/// <para><b>Stock presence (K-10)</b> is read from the ledger's balance rows (OnHand ≠ 0, any status) — no second stock
/// store, no quantity leaves this class.</para>
///
/// <para><b>History</b> is only ever inserted: there is no update, replace or delete of a history row (K-11).</para>
/// </summary>
public sealed class LocationRepository : ILocationRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Location> _locations;
    private readonly IMongoCollection<LocationChangeEntry> _history;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<LocationRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public LocationRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext,
        TransactionRetryCounter retries,
        ILogger<LocationRepository> logger)
    {
        _client = client;
        _locations = database.GetCollection<Location>(InventoryCollections.Locations);
        _history = database.GetCollection<LocationChangeEntry>(InventoryCollections.LocationHistory);
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _retries = retries;
        _logger = logger;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Location?> FindAsync(Guid locationId, CancellationToken cancellationToken = default)
        => await _locations.Find(Scope() & Builders<Location>.Filter.Eq(x => x.Id, locationId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Location>> FindManyAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default)
        => locationIds.Count == 0
            ? []
            : await _locations.Find(Scope() & Builders<Location>.Filter.In(x => x.Id, locationIds)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Location>> ListAsync(LocationType? type, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var f = Builders<Location>.Filter;
        var filter = Scope();
        if (type.HasValue) filter &= f.Eq(x => x.Type, type.Value);
        if (parentId.HasValue) filter &= f.Eq(x => x.ParentId, parentId.Value);
        return await _locations.Find(filter)
            .Sort(Builders<Location>.Sort.Ascending(x => x.CodeNormalized).Ascending(x => x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationSearchPage> SearchAsync(LocationSearch search, CancellationToken cancellationToken = default)
    {
        var f = Builders<Location>.Filter;
        var filter = Scope();
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var pattern = new MongoDB.Bson.BsonRegularExpression(Regex.Escape(search.Search.Trim()), "i");
            filter &= f.Or(f.Regex(x => x.Code, pattern), f.Regex(x => x.Name, pattern));
        }

        if (search.Types.Count > 0) filter &= f.In(x => x.Type, search.Types);
        if (search.Statuses.Count > 0) filter &= f.In(x => x.Status, search.Statuses);
        if (search.IsStockable.HasValue) filter &= f.Eq(x => x.IsStockable, search.IsStockable.Value);
        if (search.ParentId.HasValue) filter &= f.Eq(x => x.ParentId, search.ParentId.Value);

        var s = Builders<Location>.Sort;
        var primary = search.OrderBy switch
        {
            LocationSortField.Name => search.Descending ? s.Descending(x => x.Name) : s.Ascending(x => x.Name),
            LocationSortField.Type => search.Descending ? s.Descending(x => x.Type) : s.Ascending(x => x.Type),
            LocationSortField.Status => search.Descending ? s.Descending(x => x.Status) : s.Ascending(x => x.Status),
            _ => search.Descending ? s.Descending(x => x.CodeNormalized) : s.Ascending(x => x.CodeNormalized)
        };

        var total = await _locations.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _locations.Find(filter)
            .Sort(s.Combine(primary, s.Ascending(x => x.CodeNormalized), s.Ascending(x => x.Id)))
            .Skip((search.Page - 1) * search.PageSize)
            .Limit(search.PageSize)
            .ToListAsync(cancellationToken);
        return new LocationSearchPage(items, total);
    }

    public async Task<LocationHistoryPage> HistoryAsync(Guid locationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = HistoryScope() & Builders<LocationChangeEntry>.Filter.Eq(x => x.LocationId, locationId);
        var total = await _history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _history.Find(filter)
            .Sort(Builders<LocationChangeEntry>.Sort.Descending(x => x.OccurredAt).Descending(x => x.CreatedAt).Ascending(x => x.Field))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new LocationHistoryPage(items, total);
    }

    public async Task<bool> ExistsInAnotherCompanyAsync(Guid locationId, CancellationToken cancellationToken = default)
        => await _locations.Find(Builders<Location>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
                                 & Builders<Location>.Filter.Ne(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                 & Builders<Location>.Filter.Eq(x => x.IsDeleted, false)
                                 & Builders<Location>.Filter.Eq(x => x.Id, locationId))
            .AnyAsync(cancellationToken);

    public async Task<Location?> FindByCreateKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _locations.Find(Scope() & Builders<Location>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Location>> FindByBulkKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _locations.Find(Scope() & Builders<Location>.Filter.Eq(x => x.BulkIdempotencyKey, idempotencyKey))
            .Sort(Builders<Location>.Sort.Ascending(x => x.CodeNormalized))
            .ToListAsync(cancellationToken);

    public Task<LocationStockPresence> StockPresenceAsync(Location location, CancellationToken cancellationToken = default)
        => PresenceAsync(null, location, cancellationToken);

    public Task<IReadOnlySet<string>> TakenCodesAsync(IReadOnlyCollection<string> normalizedCodes, CancellationToken cancellationToken = default)
        => TakenAsync(null, normalizedCodes, cancellationToken);

    // ── the transaction ───────────────────────────────────────────────────────────────────────────────────

    public async Task<T> InTransactionAsync<T>(Func<ILocationTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        try
        {
            return await session.WithTransactionAsync(
                (s, ct) =>
                {
                    if (++attempt > 1)
                    {
                        _retries.Retried(TransactionRetryCounter.Location);
                        _logger.LogInformation("Location transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    return work(new Transaction(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new LocationUniqueViolationException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new LocationUniqueViolationException(index, ex);
        }
    }

    private sealed class Transaction : ILocationTransaction
    {
        private readonly LocationRepository _owner;
        private readonly IClientSessionHandle _session;

        public Transaction(LocationRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public async Task<Location?> FindAsync(Guid locationId, CancellationToken cancellationToken = default)
            => await _owner._locations.Find(_session, _owner.Scope() & Builders<Location>.Filter.Eq(x => x.Id, locationId))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<bool> HasActiveChildAsync(Guid locationId, CancellationToken cancellationToken = default)
            => await _owner._locations.Find(_session, _owner.Scope()
                                                       & Builders<Location>.Filter.Eq(x => x.ParentId, locationId)
                                                       & Builders<Location>.Filter.Eq(x => x.Status, LocationStatus.Active))
                .AnyAsync(cancellationToken);

        public async Task<IReadOnlyList<Location>> SubtreeAsync(Guid locationId, CancellationToken cancellationToken = default)
            => await _owner._locations.Find(_session, _owner.Scope()
                                                       & Builders<Location>.Filter.Or(
                                                           Builders<Location>.Filter.Eq(x => x.Id, locationId),
                                                           Builders<Location>.Filter.AnyEq(x => x.AncestorIds, locationId)))
                .ToListAsync(cancellationToken);

        public Task<LocationStockPresence> StockPresenceAsync(Location location, CancellationToken cancellationToken = default)
            => _owner.PresenceAsync(_session, location, cancellationToken);

        public Task<IReadOnlySet<string>> TakenCodesAsync(IReadOnlyCollection<string> normalizedCodes, CancellationToken cancellationToken = default)
            => _owner.TakenAsync(_session, normalizedCodes, cancellationToken);

        public async Task InsertAsync(IReadOnlyList<Location> locations, CancellationToken cancellationToken = default)
        {
            foreach (var location in locations)
            {
                _owner.StampScope(location);
            }

            await _owner._locations.InsertManyAsync(_session, locations, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task<bool> ReplaceAsync(Location location, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.StampScope(location);
            location.Version = expectedVersion + 1;
            var result = await _owner._locations.ReplaceOneAsync(
                _session,
                _owner.Scope() & Builders<Location>.Filter.Eq(x => x.Id, location.Id) & Builders<Location>.Filter.Eq(x => x.Version, expectedVersion),
                location,
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task SetAncestorsAsync(Guid locationId, IReadOnlyList<Guid> ancestorIds, CancellationToken cancellationToken = default)
            => _owner._locations.UpdateOneAsync(
                _session,
                _owner.Scope() & Builders<Location>.Filter.Eq(x => x.Id, locationId),
                Builders<Location>.Update.Set(x => x.AncestorIds, ancestorIds.ToList()),
                cancellationToken: cancellationToken);

        public Task TouchStructureAsync(Guid locationId, CancellationToken cancellationToken = default)
            => _owner._locations.UpdateOneAsync(
                _session,
                _owner.Scope() & Builders<Location>.Filter.Eq(x => x.Id, locationId),
                Builders<Location>.Update.Inc(x => x.StructureSeq, 1L),
                cancellationToken: cancellationToken);

        public async Task AppendHistoryAsync(IReadOnlyList<LocationChangeEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0)
            {
                return;
            }

            foreach (var entry in entries)
            {
                _owner.StampScope(entry);
            }

            await _owner._history.InsertManyAsync(_session, entries, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }
    }

    // ── shared ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// K-10: <c>HasStock</c> = a balance row with OnHand ≠ 0 on the node itself; <c>SubtreeHasStock</c> = such a row on
    /// a descendant, or (for a warehouse) any row whose WarehouseId is the node. Any stock status counts (LOC-09).
    /// </summary>
    private async Task<LocationStockPresence> PresenceAsync(IClientSessionHandle? session, Location location, CancellationToken ct)
    {
        var lf = Builders<Location>.Filter;
        var descendantsFilter = Scope() & lf.AnyEq(x => x.AncestorIds, location.Id);
        var descendants = session is null
            ? await _locations.Find(descendantsFilter).Project(x => x.Id).ToListAsync(ct)
            : await _locations.Find(session, descendantsFilter).Project(x => x.Id).ToListAsync(ct);

        var b = Builders<InventoryBalance>.Filter;
        var withStock = BalanceScope() & b.Ne(x => x.OnHand, 0m);
        var self = location.Id.ToString("D");
        var selfFilter = withStock & b.Eq(x => x.LocationId, self);
        var below = descendants.Select(d => d.ToString("D")).ToList();
        var subtreeFilter = withStock & b.Or(b.In(x => x.LocationId, below), b.Eq(x => x.WarehouseId, self) & b.Ne(x => x.LocationId, self));

        var hasStock = session is null
            ? await _balances.Find(selfFilter).AnyAsync(ct)
            : await _balances.Find(session, selfFilter).AnyAsync(ct);
        var subtreeHasStock = session is null
            ? await _balances.Find(subtreeFilter).AnyAsync(ct)
            : await _balances.Find(session, subtreeFilter).AnyAsync(ct);
        return new LocationStockPresence(hasStock, subtreeHasStock);
    }

    private async Task<IReadOnlySet<string>> TakenAsync(IClientSessionHandle? session, IReadOnlyCollection<string> normalizedCodes, CancellationToken ct)
    {
        if (normalizedCodes.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var filter = Scope() & Builders<Location>.Filter.In(x => x.CodeNormalized, normalizedCodes);
        var taken = session is null
            ? await _locations.Find(filter).Project(x => x.CodeNormalized).ToListAsync(ct)
            : await _locations.Find(session, filter).Project(x => x.CodeNormalized).ToListAsync(ct);
        return taken.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Tenant + LegalEntity + IsDeleted = false — the mandatory entry filter of every location query.</summary>
    private FilterDefinition<Location> Scope()
        => Builders<Location>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<Location>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<Location>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<LocationChangeEntry> HistoryScope()
        => Builders<LocationChangeEntry>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<LocationChangeEntry>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<LocationChangeEntry>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<InventoryBalance> BalanceScope()
        => Builders<InventoryBalance>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<InventoryBalance>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryBalance>.Filter.Eq(x => x.IsDeleted, false);

    private void StampScope(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    private static LocationUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null)
        {
            return null;
        }

        if (message.Contains(InventoryIndexConfiguration.LocationCodeIndex, StringComparison.Ordinal)) return LocationUniqueIndex.Code;
        if (message.Contains(InventoryIndexConfiguration.LocationCreateKeyIndex, StringComparison.Ordinal)) return LocationUniqueIndex.CreateIdempotencyKey;
        return null;
    }
}
