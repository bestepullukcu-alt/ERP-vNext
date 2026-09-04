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

    public async Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
        IReadOnlyCollection<Guid>? accountIdScope, CancellationToken cancellationToken)
    {
        var tenantFilter = ActiveTenant(tenantId);
        var filter = tenantFilter;

        // MOD-0151 territory-coverage constraint (Territory Node / Country Scope chips), pre-resolved by the handler to
        // the set of current-coverage account ids and ANDed on here. A non-null but empty set means the coverage filter
        // matched nothing, so short-circuit to zero rows without a query (UnfilteredTotal still reports tenant-wide).
        var hasIdScope = accountIdScope is not null;
        if (hasIdScope && accountIdScope!.Count == 0)
        {
            var unfilteredEmpty = await _collection.CountDocumentsAsync(tenantFilter, cancellationToken: cancellationToken);
            return ([], 0, unfilteredEmpty);
        }
        if (hasIdScope)
        {
            filter &= Builders<Account>.Filter.In(a => a.Id, accountIdScope!);
        }
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        if (hasSearch)
        {
            var term = search!.Trim();
            var regex = Builders<Account>.Filter.Regex(a => a.AccountName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Account>.Filter.Regex(a => a.AccountCode, new MongoDB.Bson.BsonRegularExpression(term, "i"));
            filter &= regex;
        }

        // Inline-filter chips: Status / AccountType are plain stored fields, so these are cheap equality (IN)
        // predicates ANDed onto the tenant filter. The {TenantId, ...} prefix narrows first and equality does not
        // trigger MongoDB's 32MB in-memory sort, so no extra index is required. Multi-select ⇒ Filter.In.
        var hasStatusFilter = statuses is { Count: > 0 };
        var hasTypeFilter = accountTypes is { Count: > 0 };
        if (hasStatusFilter)
        {
            filter &= Builders<Account>.Filter.In(a => a.Status, statuses!);
        }
        if (hasTypeFilter)
        {
            filter &= Builders<Account>.Filter.In(a => a.AccountType, accountTypes!);
        }

        // recordsFiltered (respects search + chip filters) and recordsTotal (tenant-wide, ignores both). When nothing
        // narrows the set the two are identical, so avoid the extra count round-trip.
        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var unfilteredTotal = (hasSearch || hasStatusFilter || hasTypeFilter || hasIdScope)
            ? await _collection.CountDocumentsAsync(tenantFilter, cancellationToken: cancellationToken)
            : total;

        var items = await _collection.Find(filter)
            .Sort(BuildSort(sortBy, sortDir))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total, unfilteredTotal);
    }

    // Only AccountName/AccountCode are allowed sort keys: each is the second field of a {TenantId, field} index, so
    // both ascending and descending are served as an index scan and never trigger MongoDB's 32MB in-memory sort on
    // the full tenant set. Any other (or missing) column falls back to AccountName ascending.
    private static SortDefinition<Account> BuildSort(string? sortBy, string? sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "accountcode" => descending
                ? Builders<Account>.Sort.Descending(a => a.AccountCode)
                : Builders<Account>.Sort.Ascending(a => a.AccountCode),
            "accountname" => descending
                ? Builders<Account>.Sort.Descending(a => a.AccountName)
                : Builders<Account>.Sort.Ascending(a => a.AccountName),
            _ => Builders<Account>.Sort.Ascending(a => a.AccountName)
        };
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
