using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0155 FU06 CycleCapacity persistence — ONE collection (<c>cycle_capacities</c>), tenant scoped, soft-delete
/// aware, and with <b>no delete method</b>: retiring a capacity is the soft archive, because deleting one would take
/// the inputs an old estimate was made from with it.
/// <para>The month rows are EMBEDDED, so the aggregate is a single document: every write is a single-document
/// operation guarded by the optimistic <see cref="EntityBase.Version"/> token, and no multi-document transaction —
/// and therefore no compensation on a standalone dev Mongo — is ever needed.</para>
/// <para>Nothing here sorts or indexes on a DateTimeOffset. Those are stored as BSON arrays, and using two of them
/// together is the documented parallel-array trap that 500s the query; ordering happens in memory over plain
/// integers.</para>
/// </summary>
public sealed class CycleCapacityRepository : ICycleCapacityRepository
{
    public const string CollectionName = "cycle_capacities";

    private readonly IMongoCollection<CapacityEntity> _collection;
    private readonly ICycleCapacityDefaultsProvider _defaults;

    public CycleCapacityRepository(IMongoDatabase database, ICycleCapacityDefaultsProvider defaults)
    {
        _collection = database.GetCollection<CapacityEntity>(CollectionName);
        _defaults = defaults;
    }

    /// <summary>
    /// MOD-0155 FU07 read-time normalisation. Every row that leaves this repository has a per-month FTE, whether it was
    /// written by FU07 or by FU06 (whose single root value is copied onto each month from the document's extra
    /// elements).
    /// <para><b>Nothing is written back</b>, exactly like <c>CyclePeriodRepository</c>'s <c>EnsureScopeType</c>: the
    /// value is persisted only when the row is next written for its own reasons. Doing it HERE rather than in a handler
    /// is what makes it impossible for one read path to forget.</para>
    /// </summary>
    private CapacityEntity? Normalize(CapacityEntity? entity)
        => entity?
            .EnsureMonthlyFte(_defaults.Current.Fte)
            .EnsureBetweenVisitTime(_defaults.Current.BetweenVisitTimeMinutes);

    private static FilterDefinition<CapacityEntity> Tenant(Guid tenantId)
        => Builders<CapacityEntity>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Normalize(await _collection
            .Find(Tenant(tenantId) & Builders<CapacityEntity>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken));

    /// <summary>
    /// The capacity pinned to a period. <b>Archived rows are excluded</b>, which is what makes archiving the deliberate
    /// way to free a period for a fresh capacity: the 1:1 rule and this lookup ask the same question, so they can never
    /// disagree about whether a period is taken.
    /// </summary>
    public async Task<CapacityEntity?> GetByCyclePeriodAsync(
        Guid tenantId, Guid cyclePeriodId, CancellationToken cancellationToken)
        => Normalize(await _collection
            .Find(Tenant(tenantId)
                  & Builders<CapacityEntity>.Filter.Eq(x => x.CyclePeriodId, cyclePeriodId)
                  & Builders<CapacityEntity>.Filter.Eq(x => x.IsArchived, false))
            .FirstOrDefaultAsync(cancellationToken));

    public async Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows
            .Select(r => r
                .EnsureMonthlyFte(_defaults.Current.Fte)
                .EnsureBetweenVisitTime(_defaults.Current.BetweenVisitTimeMinutes))
            .ToList();
    }

    public async Task InsertAsync(CapacityEntity entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(
        CapacityEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<CapacityEntity>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}
