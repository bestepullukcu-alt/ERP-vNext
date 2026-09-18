using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) content-scope persistence. Tenant scoped, soft-delete aware, no delete method (closing is the
/// soft archive lifecycle). The active-code guard excludes archived rows with an <c>ArchivedAt == null</c> equality
/// filter (never <c>$ne</c>, which crash-loops partial indexes). Period bounds (DateTimeOffset → BSON array) are never
/// sorted server-side.
/// </summary>
public sealed class ContentScopeRepository : IContentScopeRepository
{
    public const string CollectionName = "content_scopes";

    private readonly IMongoCollection<ContentScope> _collection;

    public ContentScopeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ContentScope>(CollectionName);

    private static FilterDefinition<ContentScope> Tenant(Guid tenantId)
        => Builders<ContentScope>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ContentScope?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ContentScope>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentScope>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ScopeCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<ContentScope?> GetActiveByCodeAsync(
        Guid tenantId, string scopeCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ContentScope>.Filter.Eq(x => x.ScopeCode, scopeCode)
                & Builders<ContentScope>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(ContentScope entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ContentScope entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ContentScope>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
