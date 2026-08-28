using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryAssignmentRuleRepository : ITerritoryAssignmentRuleRepository
{
    private readonly IMongoCollection<TerritoryAssignmentRule> _collection;

    public TerritoryAssignmentRuleRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TerritoryAssignmentRule>("territory_assignment_rules");
    }

    private static FilterDefinition<TerritoryAssignmentRule> ActiveModel(Guid tenantId, Guid modelId)
        => Builders<TerritoryAssignmentRule>.Filter.Where(r => r.TenantId == tenantId && r.ModelId == modelId && !r.IsDeleted);

    public async Task<TerritoryAssignmentRule?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveModel(tenantId, modelId) & Builders<TerritoryAssignmentRule>.Filter.Eq(r => r.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string ruleCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = ActiveModel(tenantId, modelId) & Builders<TerritoryAssignmentRule>.Filter.Eq(r => r.RuleCode, ruleCode);
        if (excludeId is { } id)
        {
            filter &= Builders<TerritoryAssignmentRule>.Filter.Ne(r => r.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerritoryAssignmentRule>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveModel(tenantId, modelId))
            .SortBy(r => r.Priority).ThenBy(r => r.RuleCode)
            .ToListAsync(cancellationToken);

    public async Task InsertAsync(TerritoryAssignmentRule rule, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(rule, cancellationToken: cancellationToken);

    public async Task UpdateAsync(TerritoryAssignmentRule rule, CancellationToken cancellationToken)
    {
        var filter = Builders<TerritoryAssignmentRule>.Filter.Where(r => r.Id == rule.Id && r.TenantId == rule.TenantId);
        await _collection.ReplaceOneAsync(filter, rule, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// FU03 read-only account seam over the MOD-0149 <c>accounts</c> collection. Deliberately implements only reads —
/// MOD-0151 consumes Account attributes as preview input and never writes to the master (pack §11.1).
/// </summary>
public sealed class TerritoryAccountReader : ITerritoryAccountReader
{
    private readonly IMongoCollection<Account> _collection;

    public TerritoryAccountReader(IMongoDatabase database)
    {
        _collection = database.GetCollection<Account>("accounts");
    }

    public async Task<IReadOnlyList<TerritoryAccountSnapshot>> GetByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Account>.Filter.In(a => a.Id, accountIds);
        var accounts = await _collection.Find(filter).ToListAsync(cancellationToken);
        return accounts.Select(ToSnapshot).ToList();
    }

    private static FilterDefinition<Account> ActiveTenant(Guid tenantId)
        => Builders<Account>.Filter.Where(a => a.TenantId == tenantId && !a.IsDeleted);

    public async Task<IReadOnlyList<TerritoryAccountSnapshot>> ListForPreviewAsync(
        Guid tenantId, int limit, CancellationToken cancellationToken)
    {
        var accounts = await _collection.Find(ActiveTenant(tenantId))
            .SortBy(a => a.AccountCode)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return accounts.Select(a => new TerritoryAccountSnapshot(
            a.Id, a.AccountCode, a.AccountName, a.AccountType, a.AccountCategory, a.Status,
            a.CountryRef, a.CityRef, a.DistrictRef)).ToList();
    }

    public async Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.CountDocumentsAsync(ActiveTenant(tenantId), cancellationToken: cancellationToken);

    private static TerritoryAccountSnapshot ToSnapshot(Account a) => new(
        a.Id, a.AccountCode, a.AccountName, a.AccountType, a.AccountCategory, a.Status,
        a.CountryRef, a.CityRef, a.DistrictRef);
}
