using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) content-set persistence. Tenant scoped, soft-delete aware, no delete method (closing is the
/// soft archive lifecycle). The active-code guard excludes archived rows with an <c>ArchivedAt == null</c> equality
/// filter (never <c>$ne</c>, which crash-loops partial indexes). Optimistic concurrency is <c>EntityBase.Version</c>.
/// </summary>
public sealed class ContentSetRepository : IContentSetRepository
{
    public const string CollectionName = "content_sets";

    private readonly IMongoCollection<ContentSet> _collection;

    public ContentSetRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ContentSet>(CollectionName);

    private static FilterDefinition<ContentSet> Tenant(Guid tenantId)
        => Builders<ContentSet>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ContentSet?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ContentSet>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentSet>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SetCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<ContentSet?> GetActiveByCodeAsync(
        Guid tenantId, string setCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ContentSet>.Filter.Eq(x => x.SetCode, setCode)
                & Builders<ContentSet>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(ContentSet entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ContentSet entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ContentSet>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
