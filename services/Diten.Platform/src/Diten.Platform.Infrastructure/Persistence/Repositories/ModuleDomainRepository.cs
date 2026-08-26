using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ModuleDomainRepository : GlobalRepository<ModuleDomain>, IModuleDomainRepository
{
    public ModuleDomainRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.ModuleDomains)
    {
    }

    public async Task<ModuleDomain?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<ModuleDomain>.Filter.And(
            ExecutionFilter,
            Builders<ModuleDomain>.Filter.Eq(x => x.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        // FIX-DOMAIN-DEDUP — gate on the NORMALIZED key, not the raw Code, so a differently-formatted but
        // logically-identical code (e.g. "master-data-management" vs an existing "MASTERDATAMANAGEMENT") is
        // reported as in-use with the friendly 409 rather than slipping past into the unique-index E11000 path.
        var key = ModuleTaxonomyCanonicalizer.NormalizeKey(code);
        var filters = new List<FilterDefinition<ModuleDomain>>
        {
            ExecutionFilter,
            Builders<ModuleDomain>.Filter.Eq(x => x.CodeKey, key)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModuleDomain>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<ModuleDomain>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(ModuleDomain item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ModuleDomain>.Filter.And(
            ExecutionFilter,
            Builders<ModuleDomain>.Filter.Eq(x => x.Id, item.Id));
        await Collection.ReplaceOneAsync(filter, item, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<ModuleDomain> Items, long TotalCount)> QueryAsync(ModuleDomainQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModuleDomain>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<ModuleDomain>.Filter.Or(
                Builders<ModuleDomain>.Filter.Regex(x => x.Code, regex),
                Builders<ModuleDomain>.Filter.Regex(x => x.DisplayName, regex),
                Builders<ModuleDomain>.Filter.Regex(x => x.Description, regex)));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(Builders<ModuleDomain>.Filter.Eq(x => x.IsActive, query.IsActive.Value));
        }

        var filter = Builders<ModuleDomain>.Filter.And(filters);
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

    public async Task<IReadOnlyList<ModuleDomain>> GetActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<ModuleDomain>.Filter.And(
            ExecutionFilter,
            Builders<ModuleDomain>.Filter.Eq(x => x.IsActive, true));

        return await Collection.Find(filter)
            .Sort(Builders<ModuleDomain>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.DisplayName))
            .ToListAsync(ct);
    }

    private static SortDefinition<ModuleDomain> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "code" => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.Code) : Builders<ModuleDomain>.Sort.Ascending(x => x.Code),
            "displayname" => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.DisplayName) : Builders<ModuleDomain>.Sort.Ascending(x => x.DisplayName),
            "isactive" => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.IsActive) : Builders<ModuleDomain>.Sort.Ascending(x => x.IsActive),
            "createdat" => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.CreatedAt) : Builders<ModuleDomain>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.UpdatedAt) : Builders<ModuleDomain>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<ModuleDomain>.Sort.Descending(x => x.SortOrder) : Builders<ModuleDomain>.Sort.Ascending(x => x.SortOrder)
        };
    }
}
