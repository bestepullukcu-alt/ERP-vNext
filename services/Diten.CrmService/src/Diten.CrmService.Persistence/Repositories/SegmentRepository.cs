using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0167 FU02 Segment persistence — ONE collection (<c>segments</c>) with the criteria tree embedded (D2), so a rule
/// and its criteria share one document and one optimistic token. Tenant scoped, soft-delete aware, and with <b>no
/// delete method</b>: closing a segment is the soft archive lifecycle, because deleting one would take every past
/// explanation of "why was this person selected?" with it.
/// <para>EffectiveFrom / EffectiveTo / CriteriaFrozenAt / ActivatedAt / ArchivedAt are DateTimeOffset and therefore
/// stored as BSON arrays: they are never index keys and never server-side sort keys (the parallel-array trap). Ordering
/// happens in memory. Code uniqueness is enforced in the handler, so no partial index needs a <c>$ne</c> filter — which
/// crash-loops the service at startup.</para>
/// <para>Every write is a single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token,
/// so no multi-document transaction and no compensation is needed on a standalone dev Mongo.</para>
/// </summary>
public sealed class SegmentRepository : ISegmentRepository
{
    public const string CollectionName = "segments";

    private readonly IMongoCollection<Segment> _collection;

    public SegmentRepository(IMongoDatabase database)
        => _collection = database.GetCollection<Segment>(CollectionName);

    private static FilterDefinition<Segment> Tenant(Guid tenantId)
        => Builders<Segment>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<Segment>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SegmentCode).ThenBy(x => x.SegmentVersion).ToList();
    }

    public async Task<IReadOnlyList<Segment>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<Segment>.Filter.Eq(x => x.VersionLineageId, versionLineageId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SegmentVersion).ToList();
    }

    public async Task<IReadOnlyList<Segment>> ListByCodeAsync(
        Guid tenantId, string segmentCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<Segment>.Filter.Eq(x => x.SegmentCode, segmentCode))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SegmentVersion).ToList();
    }

    public async Task InsertAsync(Segment entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<Segment>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}
