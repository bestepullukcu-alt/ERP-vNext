using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private const int MaxHierarchyWalk = 50;
    private readonly IMongoCollection<Account> _collection;

    public AccountRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Account>("accounts");
    }

    private static FilterDefinition<Account> ActiveTenant(Guid tenantId)
        => Builders<Account>.Filter.Where(a => a.TenantId == tenantId && !a.IsDeleted);

    public async Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Account>.Filter.Eq(a => a.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Account>.Filter.Eq(a => a.AccountCode, accountCode);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Account>.Filter.Eq(a => a.AccountCode, accountCode);
        if (excludeId is { } id)
        {
            filter &= Builders<Account>.Filter.Ne(a => a.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Account> Items, long Total)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var regex = Builders<Account>.Filter.Regex(a => a.AccountName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Account>.Filter.Regex(a => a.AccountCode, new MongoDB.Bson.BsonRegularExpression(term, "i"));
            filter &= regex;
        }

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _collection.Find(filter)
            .SortBy(a => a.AccountName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Account>.Filter.Eq(a => a.ParentAccountId, parentId);
        return await _collection.Find(filter).SortBy(a => a.AccountName).ToListAsync(cancellationToken);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken cancellationToken)
    {
        var current = (Guid?)candidateParentId;
        var steps = 0;
        while (current is { } cursor && steps++ < MaxHierarchyWalk)
        {
            if (cursor == accountId)
            {
                return true;
            }

            var node = await GetByIdAsync(tenantId, cursor, cancellationToken);
            current = node?.ParentAccountId;
        }

        return false;
    }

    public async Task InsertAsync(Account account, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(account, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken)
    {
        var filter = Builders<Account>.Filter.Where(a => a.Id == account.Id && a.TenantId == account.TenantId);
        await _collection.ReplaceOneAsync(filter, account, cancellationToken: cancellationToken);
    }
}
