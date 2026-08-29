using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountRelationshipRepository : IAccountRelationshipRepository
{
    private readonly IMongoCollection<AccountRelationship> _collection;

    public AccountRelationshipRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AccountRelationship>("account_relationships");
    }

    private static FilterDefinition<AccountRelationship> ActiveTenant(Guid tenantId)
        => Builders<AccountRelationship>.Filter.Where(r => r.TenantId == tenantId && !r.IsDeleted);

    public async Task<AccountRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<AccountRelationship>.Filter.Eq(r => r.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsActivePairAsync(
        Guid tenantId, Guid sourceAccountId, Guid targetAccountId, string relationshipType, bool includeReverse, Guid? excludeId, CancellationToken cancellationToken)
    {
        var b = Builders<AccountRelationship>.Filter;
        var forward = b.Where(r => r.SourceAccountId == sourceAccountId && r.TargetAccountId == targetAccountId);
        var pair = includeReverse
            ? forward | b.Where(r => r.SourceAccountId == targetAccountId && r.TargetAccountId == sourceAccountId)
            : forward;

        // Historically closed (ended/inactive) relationships never block a new active pair — history is preserved
        // in the list projection but excluded from the uniqueness check.
        var filter = ActiveTenant(tenantId)
            & b.Nin(r => r.Status, RelationshipLifecycle.ClosedStatuses)
            & b.Eq(r => r.RelationshipType, relationshipType) & pair;
        if (excludeId is { } id)
        {
            filter &= b.Ne(r => r.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountRelationship>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var b = Builders<AccountRelationship>.Filter;
        var filter = ActiveTenant(tenantId) & (b.Eq(r => r.SourceAccountId, accountId) | b.Eq(r => r.TargetAccountId, accountId));
        return await _collection.Find(filter).SortBy(r => r.RelationshipType).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountRelationship>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenant(tenantId)).SortBy(r => r.RelationshipType).ToListAsync(cancellationToken);

    public async Task InsertAsync(AccountRelationship relationship, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(relationship, cancellationToken: cancellationToken);

    public async Task UpdateAsync(AccountRelationship relationship, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountRelationship>.Filter.Where(r => r.Id == relationship.Id && r.TenantId == relationship.TenantId);
        await _collection.ReplaceOneAsync(filter, relationship, cancellationToken: cancellationToken);
    }
}
