using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ModuleServiceRepository : GlobalRepository<ModuleService>, IModuleServiceRepository
{
    public ModuleServiceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_module_services")
    {
    }

    public async Task<ModuleService?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<ModuleService>.Filter.And(
            ExecutionFilter,
            Builders<ModuleService>.Filter.Eq(x => x.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModuleService>>
        {
            ExecutionFilter,
            Builders<ModuleService>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModuleService>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<ModuleService>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(ModuleService item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ModuleService>.Filter.And(
            ExecutionFilter,
            Builders<ModuleService>.Filter.Eq(x => x.Id, item.Id));
        await Collection.ReplaceOneAsync(filter, item, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<ModuleService> Items, long TotalCount)> QueryAsync(ModuleServiceQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModuleService>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<ModuleService>.Filter.Or(
                Builders<ModuleService>.Filter.Regex(x => x.Code, regex),
                Builders<ModuleService>.Filter.Regex(x => x.DisplayName, regex),
                Builders<ModuleService>.Filter.Regex(x => x.Description, regex)));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(Builders<ModuleService>.Filter.Eq(x => x.IsActive, query.IsActive.Value));
        }

        var filter = Builders<ModuleService>.Filter.And(filters);
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

    public async Task<IReadOnlyList<ModuleService>> GetActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<ModuleService>.Filter.And(
            ExecutionFilter,
            Builders<ModuleService>.Filter.Eq(x => x.IsActive, true));

        return await Collection.Find(filter)
            .Sort(Builders<ModuleService>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.DisplayName))
            .ToListAsync(ct);
    }

    private static SortDefinition<ModuleService> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "code" => descending ? Builders<ModuleService>.Sort.Descending(x => x.Code) : Builders<ModuleService>.Sort.Ascending(x => x.Code),
            "displayname" => descending ? Builders<ModuleService>.Sort.Descending(x => x.DisplayName) : Builders<ModuleService>.Sort.Ascending(x => x.DisplayName),
            "isactive" => descending ? Builders<ModuleService>.Sort.Descending(x => x.IsActive) : Builders<ModuleService>.Sort.Ascending(x => x.IsActive),
            "createdat" => descending ? Builders<ModuleService>.Sort.Descending(x => x.CreatedAt) : Builders<ModuleService>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<ModuleService>.Sort.Descending(x => x.UpdatedAt) : Builders<ModuleService>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<ModuleService>.Sort.Descending(x => x.SortOrder) : Builders<ModuleService>.Sort.Ascending(x => x.SortOrder)
        };
    }
}
