using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-KP-2 — <c>knowledge_path_revisions</c>. Same rules as <see cref="KnowledgePathRepository"/>: tenant scoped,
/// soft-delete aware, no delete method, single-document optimistic replace; ordering in memory (CreatedAt is a
/// DateTimeOffset BSON array — never a sort key). The embedded Guid members take the string-Guid class-map convention.
/// </summary>
public sealed class KnowledgePathRevisionRepository : IKnowledgePathRevisionRepository
{
    public const string CollectionName = "knowledge_path_revisions";

    private readonly IMongoCollection<KnowledgePathRevision> _collection;

    public KnowledgePathRevisionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<KnowledgePathRevision>(CollectionName);

    private static FilterDefinition<KnowledgePathRevision> Tenant(Guid tenantId)
        => Builders<KnowledgePathRevision>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<KnowledgePathRevision?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<KnowledgePathRevision>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgePathRevision>> ListByPathAsync(
        Guid tenantId, Guid pathId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<KnowledgePathRevision>.Filter.Eq(x => x.PathId, pathId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.RevisionNumber).ToList();
    }

    public async Task InsertAsync(KnowledgePathRevision entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(KnowledgePathRevision entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<KnowledgePathRevision>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}
