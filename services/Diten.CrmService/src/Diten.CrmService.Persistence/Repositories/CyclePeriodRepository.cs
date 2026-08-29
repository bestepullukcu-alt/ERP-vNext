using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0165 FU06 CyclePeriod persistence — ONE collection (<c>cycle_periods</c>), tenant scoped, soft-delete aware,
/// and with <b>no delete method</b>: ending a period is the <c>closed</c> lifecycle, because deleting one would take
/// every past plan's explanation with it.
/// <para>StartDate / EndDate / ActivatedAt / ClosedAt are DateTimeOffset and therefore stored as BSON arrays: they are
/// never index keys and never server-side sort keys (the parallel-array trap, which 500s the query). Ordering happens
/// in memory over plain integers. Code and sequence uniqueness are enforced in the handler, so no partial index needs
/// a <c>$ne</c> filter — which crash-loops the service at startup.</para>
/// <para>Every write is a single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token,
/// so no multi-document transaction and no compensation is needed on a standalone dev Mongo.</para>
/// </summary>
public sealed class CyclePeriodRepository : ICyclePeriodRepository
{
    public const string CollectionName = "cycle_periods";

    private readonly IMongoCollection<PeriodEntity> _collection;

    public CyclePeriodRepository(IMongoDatabase database)
        => _collection = database.GetCollection<PeriodEntity>(CollectionName);

    private static FilterDefinition<PeriodEntity> Tenant(Guid tenantId)
        => Builders<PeriodEntity>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<PeriodEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => (await _collection.Find(Tenant(tenantId) & Builders<PeriodEntity>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken))?.EnsureScopeType();

    public async Task<IReadOnlyList<PeriodEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PeriodEntity>> ListByCodeAsync(
        Guid tenantId, string cycleCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<PeriodEntity>.Filter.Eq(x => x.CycleCode, cycleCode))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PeriodEntity>> ListByYearAsync(
        Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<PeriodEntity>.Filter.Eq(x => x.Year, year))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PeriodEntity>> ListActiveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PeriodEntity>.Filter.Eq(x => x.CycleStatus, CyclePeriodStatuses.Active))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task InsertAsync(PeriodEntity entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(PeriodEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<PeriodEntity>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    /// <summary>
    /// In-memory ordering over integers only — never over the DateTimeOffset fields — and the FU07 read-time scope
    /// normalisation.
    /// <para><b>This is not a migration.</b> Nothing is written back: a row created by FU06 simply reports the scope it
    /// always had (no business unit → tenant, a business unit → business-unit), and the field is persisted only when
    /// the row is next written for its own reasons. That is what lets FU07 add a scope to the identity key without a
    /// backfill and without touching Mongo — and it works because scope narrowing happens HERE, in memory, rather than
    /// as a Mongo filter that a missing field could silently exclude a row from.</para>
    /// </summary>
    private static IReadOnlyList<PeriodEntity> Ordered(IEnumerable<PeriodEntity> rows)
        => rows
            .Select(x => x.EnsureScopeType())
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.SequenceInYear)
            .ToList();
}
