using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-15 (CAND-CAP-0011) ContentSetRevision persistence. Tenant scoped, soft-delete aware, no delete method
/// (archive-only). The frozen manifest is immutable; only the review status/decision transition is written back through
/// <see cref="UpdateAsync"/>. Guid FKs (ContentSetId) round-trip as strings via the RegisterClassMaps convention.
/// </summary>
public sealed class ContentSetRevisionRepository : IContentSetRevisionRepository
{
    public const string CollectionName = "content_set_revisions";

    private readonly IMongoCollection<ContentSetRevision> _collection;

    public ContentSetRevisionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ContentSetRevision>(CollectionName);

    private static FilterDefinition<ContentSetRevision> Tenant(Guid tenantId)
        => Builders<ContentSetRevision>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ContentSetRevision?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ContentSetRevision>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(
        Guid tenantId, Guid contentSetId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ContentSetRevision>.Filter.Eq(x => x.ContentSetId, contentSetId))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.RevisionNumber).ToList();
    }

    public async Task InsertAsync(ContentSetRevision entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ContentSetRevision entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ContentSetRevision>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
