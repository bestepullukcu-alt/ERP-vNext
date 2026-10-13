using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W3 (W3-BE-b) — objection library persistence. Tenant scoped, soft-delete aware, optimistic replace on the
/// technical version. ApprovedAt / RetiredAt (DateTimeOffset → BSON array) are never sorted server-side.
/// </summary>
public sealed class ObjectionResponseRepository : IObjectionResponseRepository
{
    public const string CollectionName = "objection_responses";

    private readonly IMongoCollection<ObjectionResponse> _collection;

    public ObjectionResponseRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ObjectionResponse>(CollectionName);

    private static FilterDefinition<ObjectionResponse> Tenant(Guid tenantId)
        => Builders<ObjectionResponse>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ObjectionResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ObjectionResponse>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ObjectionResponse>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.CategoryCode, StringComparer.Ordinal).ThenBy(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ObjectionResponse>> ListByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ObjectionResponse>();
        }

        return await _collection.Find(Tenant(tenantId) & Builders<ObjectionResponse>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(ObjectionResponse entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(ObjectionResponse entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<ObjectionResponse>.Filter.Where(x =>
                x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }
}
