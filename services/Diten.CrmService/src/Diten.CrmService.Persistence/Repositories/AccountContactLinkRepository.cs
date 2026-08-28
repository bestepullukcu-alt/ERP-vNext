using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountContactLinkRepository : IAccountContactLinkRepository
{
    private readonly IMongoCollection<AccountContactLink> _collection;

    public AccountContactLinkRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AccountContactLink>("account_contact_links");
    }

    private static FilterDefinition<AccountContactLink> ActiveTenant(Guid tenantId)
        => Builders<AccountContactLink>.Filter.Where(l => l.TenantId == tenantId && !l.IsDeleted);

    /// <summary>Not-deleted AND not historically closed (ended/inactive). Used by the uniqueness checks so an ended
    /// link never blocks a new active one — the list projections deliberately keep closed rows for history.</summary>
    private static FilterDefinition<AccountContactLink> OpenTenant(Guid tenantId)
        => ActiveTenant(tenantId) & Builders<AccountContactLink>.Filter.Nin(l => l.Status, RelationshipLifecycle.ClosedStatuses);

    public async Task<AccountContactLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<AccountContactLink>.Filter.Eq(l => l.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsActiveAsync(
        Guid tenantId, Guid accountId, Guid contactId, string roleCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = OpenTenant(tenantId)
            & Builders<AccountContactLink>.Filter.Where(l => l.AccountId == accountId && l.ContactId == contactId && l.RoleCode == roleCode);
        if (excludeId is { } id)
        {
            filter &= Builders<AccountContactLink>.Filter.Ne(l => l.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsPrimaryAsync(
        Guid tenantId, Guid accountId, string roleCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = OpenTenant(tenantId)
            & Builders<AccountContactLink>.Filter.Where(l => l.AccountId == accountId && l.RoleCode == roleCode && l.IsPrimary);
        if (excludeId is { } id)
        {
            filter &= Builders<AccountContactLink>.Filter.Ne(l => l.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<AccountContactLink>.Filter.Eq(l => l.AccountId, accountId);
        return await _collection.Find(filter).SortByDescending(l => l.IsPrimary).ThenBy(l => l.RoleCode).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<AccountContactLink>.Filter.Eq(l => l.ContactId, contactId);
        return await _collection.Find(filter).SortByDescending(l => l.IsPrimary).ThenBy(l => l.RoleCode).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountContactLink>> ListByContactIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
    {
        if (contactIds is null || contactIds.Count == 0) return [];
        var filter = ActiveTenant(tenantId) & Builders<AccountContactLink>.Filter.In(l => l.ContactId, contactIds);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenant(tenantId)).ToListAsync(cancellationToken);

    public async Task InsertAsync(AccountContactLink link, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(link, cancellationToken: cancellationToken);

    public async Task UpdateAsync(AccountContactLink link, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountContactLink>.Filter.Where(l => l.Id == link.Id && l.TenantId == link.TenantId);
        await _collection.ReplaceOneAsync(filter, link, cancellationToken: cancellationToken);
    }
}
