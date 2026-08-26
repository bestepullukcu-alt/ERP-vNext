using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ModulePageDescriptorRepository : TenantRepository<ModulePageDescriptor>, IModulePageDescriptorRepository
{
    private readonly IMongoCollection<ModuleCatalogItem> _moduleCatalogCollection;

    public ModulePageDescriptorRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_module_page_descriptors")
    {
        _moduleCatalogCollection = dbContext.GetCollection<ModuleCatalogItem>(PlatformCollections.ModuleCatalog);
    }

    public async Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default)
    {
        var filter = Builders<ModuleCatalogItem>.Filter.And(
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsDeleted, false),
            Builders<ModuleCatalogItem>.Filter.Eq(x => x.ModuleCode, moduleCode));

        return await _moduleCatalogCollection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModulePageDescriptor>>
        {
            ExecutionFilter,
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.ModuleCode, moduleCode),
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.PageCode, pageCode)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModulePageDescriptor>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<ModulePageDescriptor>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModulePageDescriptor>>
        {
            ExecutionFilter,
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.ModuleCode, moduleCode),
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.RoutePath, routePath)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModulePageDescriptor>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<ModulePageDescriptor>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default)
    {
        descriptor.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ModulePageDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.Id, descriptor.Id));
        await Collection.ReplaceOneAsync(filter, descriptor, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.ModuleCode, moduleCode));

        return await Collection.Find(filter)
            .Sort(Builders<ModulePageDescriptor>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.PageCode))
            .ToListAsync(ct);
    }

    // FEAT-CATALOG-PERM-DELETE-SYNC — ExecutionFilter excludes soft-deleted rows, so a count taken AFTER the delete
    // of the owning descriptor naturally excludes it; a result of 0 (across pages + actions) means the last reference.
    public async Task<long> CountByRequiredPermissionAsync(string permissionKey, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDescriptor>.Filter.Eq(x => x.RequiredPermission, permissionKey));

        return await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModulePageDescriptor>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.ModuleCode))
        {
            filters.Add(Builders<ModulePageDescriptor>.Filter.Eq(x => x.ModuleCode, query.ModuleCode));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<ModulePageDescriptor>.Filter.Or(
                Builders<ModulePageDescriptor>.Filter.Regex(x => x.ModuleCode, regex),
                Builders<ModulePageDescriptor>.Filter.Regex(x => x.PageCode, regex),
                Builders<ModulePageDescriptor>.Filter.Regex(x => x.DisplayName, regex),
                Builders<ModulePageDescriptor>.Filter.Regex(x => x.RoutePath, regex),
                Builders<ModulePageDescriptor>.Filter.Regex(x => x.RequiredPermission, regex)));
        }

        if (query.PageTypes is { Count: > 0 })
        {
            filters.Add(Builders<ModulePageDescriptor>.Filter.In(x => x.PageType, query.PageTypes));
        }

        if (query.Statuses is { Count: > 0 })
        {
            filters.Add(Builders<ModulePageDescriptor>.Filter.In(x => x.Status, query.Statuses));
        }

        var filter = Builders<ModulePageDescriptor>.Filter.And(filters);
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

    private static SortDefinition<ModulePageDescriptor> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "modulecode" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.ModuleCode) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.ModuleCode),
            "pagecode" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.PageCode) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.PageCode),
            "displayname" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.DisplayName) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.DisplayName),
            "routepath" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.RoutePath) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.RoutePath),
            "pagetype" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.PageType) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.PageType),
            "status" => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.Status) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.Status),
            _ => descending ? Builders<ModulePageDescriptor>.Sort.Descending(x => x.SortOrder) : Builders<ModulePageDescriptor>.Sort.Ascending(x => x.SortOrder)
        };
    }
}
