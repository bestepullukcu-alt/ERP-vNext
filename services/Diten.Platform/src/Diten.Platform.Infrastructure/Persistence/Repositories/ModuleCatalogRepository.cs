using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ModuleCatalogRepository : GlobalRepository<ModuleCatalogItem>, ITransactionalModuleCatalogRepository
{
    private readonly IPlatformDbContext _dbContext;

    public ModuleCatalogRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_module_catalog")
    {
        _dbContext = dbContext;
    }

    public async Task<ModuleCatalogItem> CreateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, _dbContext), item, cancellationToken: ct);
        return item;
    }

    public async Task<ModuleCatalogItem?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) =>
        await Collection.Find(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public Task<ModuleCatalogItem?> GetByCodeAsync(IPlatformTransactionSession session, string moduleCode, CancellationToken ct = default) =>
        GetByCodeCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), moduleCode, ct);

    public async Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default)
        => await GetByCodeCoreAsync(null, moduleCode, ct);

    private async Task<ModuleCatalogItem?> GetByCodeCoreAsync(IClientSessionHandle? session, string moduleCode, CancellationToken ct)
    {
        var filter = Builders<ModuleCatalogItem>.Filter.And(
            ExecutionFilter,
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.ModuleCode, moduleCode));
        return session is null
            ? await Collection.Find(filter).FirstOrDefaultAsync(ct)
            : await Collection.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsByCodeAsync(IPlatformTransactionSession session, string moduleCode, Guid? excludeId = null, CancellationToken ct = default) =>
        ExistsByCodeCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), moduleCode, excludeId, ct);

    public async Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default)
        => await ExistsByCodeCoreAsync(null, moduleCode, excludeId, ct);

    private async Task<bool> ExistsByCodeCoreAsync(IClientSessionHandle? session, string moduleCode, Guid? excludeId, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<ModuleCatalogItem>>
        {
            ExecutionFilter,
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.ModuleCode, moduleCode)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        var filter = Builders<ModuleCatalogItem>.Filter.And(filters);
        return session is null
            ? await Collection.Find(filter).AnyAsync(ct)
            : await Collection.Find(session, filter).AnyAsync(ct);
    }

    public Task UpdateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default) =>
        UpdateCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), item, ct);

    [Obsolete("Authoritative module-catalog mutations require an explicit Platform transaction session.")]
    public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless module-catalog mutation is disabled until the caller supplies the Platform transaction session.");

    private async Task UpdateCoreAsync(IClientSessionHandle? session, ModuleCatalogItem item, CancellationToken ct)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        // Session-owned updates deliberately address a soft-deleted document as well so an
        // explicit reactivation can restore it atomically with projection/version/outboxes.
        var filter = Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, item.Id);
        if (session is null)
        {
            await Collection.ReplaceOneAsync(filter, item, cancellationToken: ct);
        }
        else
        {
            await Collection.ReplaceOneAsync(session, filter, item, cancellationToken: ct);
        }
    }

    public async Task DeleteAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default)
    {
        var update = Builders<ModuleCatalogItem>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);
        await Collection.UpdateOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<ModuleCatalogItem>.Filter.And(ExecutionFilter, Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, id)),
            update,
            cancellationToken: ct);
    }

    [Obsolete("Authoritative module-catalog mutations require an explicit Platform transaction session.")]
    public override Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless module-catalog mutation is disabled until the caller supplies the Platform transaction session.");

    [Obsolete("Authoritative module-catalog mutations require an explicit Platform transaction session.")]
    public override Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless module-catalog mutation is disabled until the caller supplies the Platform transaction session.");

    public async Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModuleCatalogItem>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<ModuleCatalogItem>.Filter.Or(
                Builders<ModuleCatalogItem>.Filter.Regex(x => x.ModuleCode, regex),
                Builders<ModuleCatalogItem>.Filter.Regex(x => x.ModuleName, regex),
                Builders<ModuleCatalogItem>.Filter.Regex(x => x.DisplayName, regex),
                Builders<ModuleCatalogItem>.Filter.Regex(x => x.Domain, regex),
                Builders<ModuleCatalogItem>.Filter.Regex(x => x.Service, regex)));
        }

        var domains = SplitValues(query.Domain);
        if (domains.Count > 0)
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.In(x => x.Domain, domains));
        }

        var services = SplitValues(query.Service);
        if (services.Count > 0)
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.In(x => x.Service, services));
        }

        if (query.Statuses is { Count: > 0 })
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.In(x => x.Status, query.Statuses));
        }

        if (query.IsCoreModule.HasValue)
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsCoreModule, query.IsCoreModule.Value));
        }

        if (query.IsTenantAssignable.HasValue)
        {
            filters.Add(Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsTenantAssignable, query.IsTenantAssignable.Value));
        }

        var filter = Builders<ModuleCatalogItem>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await Collection.Find(filter)
            .Sort(BuildSort(query.Sort))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default)
    {
        var filter = Builders<ModuleCatalogItem>.Filter.And(
            ExecutionFilter,
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.Status, ModuleCatalogStatus.Active),
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsTenantAssignable, true));

        return await Collection.Find(filter)
            .Sort(Builders<ModuleCatalogItem>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.ModuleCode))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default)
    {
        var result = await Collection.Aggregate()
            .Match(ExecutionFilter)
            .Group(x => x.Status, g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return result.ToDictionary(x => x.Status, x => (long)x.Count);
    }

    private static SortDefinition<ModuleCatalogItem> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "modulecode" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.ModuleCode) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.ModuleCode),
            "modulename" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.ModuleName) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.ModuleName),
            "displayname" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.DisplayName) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.DisplayName),
            "domain" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.Domain) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.Domain),
            "service" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.Service) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.Service),
            "status" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.Status) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.Status),
            "version" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.Version) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.Version),
            "createdat" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.CreatedAt) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.UpdatedAt) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<ModuleCatalogItem>.Sort.Descending(x => x.SortOrder) : Builders<ModuleCatalogItem>.Sort.Ascending(x => x.SortOrder)
        };
    }

    private static IReadOnlyList<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
