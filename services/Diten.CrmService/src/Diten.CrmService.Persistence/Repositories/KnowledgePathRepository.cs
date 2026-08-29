using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0162 FU04 KnowledgePath persistence — one collection (<c>knowledge_paths</c>); steps are embedded (D2). Same
/// rules as the FU02/FU03 knowledge repositories: tenant scoped, soft-delete aware, no delete method (closing is the
/// soft archive lifecycle). EffectiveFrom / EffectiveTo / ArchivedAt / StepSetFrozenAt (DateTimeOffset → BSON array) are
/// never sorted server-side nor used as index keys; ordering happens in memory. Code uniqueness is enforced in the
/// handler (an archived code is reusable → no partial <c>$ne</c> filter, which crash-loops a partial index). Every write
/// is a single-document replace guarded by the optimistic <see cref="EntityBase.Version"/> token, so no multi-document
/// transaction is needed. The embedded Guid members take the string-Guid class-map convention (see Persistence DI) so
/// filters never silently return nothing (the AccountTerritoryAssignment lesson).
/// </summary>
public sealed class KnowledgePathRepository : IKnowledgePathRepository
{
    public const string CollectionName = "knowledge_paths";

    private readonly IMongoCollection<KnowledgePath> _collection;

    public KnowledgePathRepository(IMongoDatabase database)
        => _collection = database.GetCollection<KnowledgePath>(CollectionName);

    private static FilterDefinition<KnowledgePath> Tenant(Guid tenantId)
        => Builders<KnowledgePath>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<KnowledgePath?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<KnowledgePath>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.PathCode).ThenBy(x => x.PathVersion).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(
        Guid tenantId, string pathCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<KnowledgePath>.Filter.Eq(x => x.PathCode, pathCode))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.PathVersion).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task InsertAsync(KnowledgePath entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<KnowledgePath>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}
