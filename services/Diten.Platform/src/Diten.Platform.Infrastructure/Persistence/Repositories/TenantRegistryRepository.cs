using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantRegistryRepository : GlobalRepository<Tenant>, ITenantRegistryRepository
{
    public TenantRegistryRepository(IPlatformDbContext dbContext, ITenantContext tenantContext) 
        : base(dbContext.Database, tenantContext, "tenants") { }

    public async Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.And(
            ExecutionFilter,
            Builders<Tenant>.Filter.Eq(t => t.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.And(
            ExecutionFilter,
            Builders<Tenant>.Filter.Eq(t => t.Slug, slug));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.And(
            ExecutionFilter,
            Builders<Tenant>.Filter.Eq(t => t.Domain, domain));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.And(
            ExecutionFilter,
            Builders<Tenant>.Filter.Eq(t => t.Status, TenantStatus.Active));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.Eq(t => t.Id, id);
        var update = Builders<Tenant>.Update
            .Set(t => t.Status, status)
            .Set(t => t.UpdatedAt, DateTimeOffset.UtcNow);

        if (status == TenantStatus.Active)
        {
            update = update.Set(t => t.ProvisionedAt, DateTimeOffset.UtcNow);
        }
        else if (status == TenantStatus.Suspended)
        {
            update = update.Set(t => t.SuspendedAt, DateTimeOffset.UtcNow);
        }

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.And(
            ExecutionFilter,
            Builders<Tenant>.Filter.Eq(x => x.Id, tenant.Id));

        await Collection.ReplaceOneAsync(filter, tenant, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<Tenant>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new MongoDB.Bson.BsonRegularExpression(escaped, "i");
            filters.Add(Builders<Tenant>.Filter.Or(
                Builders<Tenant>.Filter.Regex(x => x.Code, regex),
                Builders<Tenant>.Filter.Regex(x => x.Name, regex),
                Builders<Tenant>.Filter.Regex(x => x.DisplayName, regex),
                Builders<Tenant>.Filter.Regex(x => x.Domain, regex)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<TenantStatus>(query.Status, true, out var status))
        {
            filters.Add(Builders<Tenant>.Filter.Eq(x => x.Status, status));
        }

        if (!string.IsNullOrWhiteSpace(query.Region))
        {
            filters.Add(Builders<Tenant>.Filter.Eq(x => x.Region, query.Region.Trim().ToUpperInvariant()));
        }

        var filter = Builders<Tenant>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var sortDefinition = BuildSort(query.Sort);
        var skip = (Math.Max(query.Page, 1) - 1) * Math.Max(query.PageSize, 1);
        var limit = Math.Clamp(query.PageSize, 1, 200);

        var items = await Collection.Find(filter)
            .Sort(sortDefinition)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = await Collection.Aggregate()
            .Match(ExecutionFilter)
            .Group(
                _ => 1,
                group => new TenantRegistryStats(
                    group.Count(),
                    group.Sum(x => x.Status == TenantStatus.Active ? 1 : 0),
                    group.Sum(x => x.Status == TenantStatus.Provisioning ? 1 : 0),
                    group.Sum(x => x.Status == TenantStatus.Suspended ? 1 : 0),
                    group.Sum(x => x.Status == TenantStatus.Deactivated ? 1 : 0),
                    group.Sum(x => x.SubscriptionStatus == Diten.Platform.Domain.Enums.TenantSubscriptionStatus.Trialing ? 1 : 0),
                    group.Sum(x => x.StorageQuotaGb > 0 && x.StorageUsedGb > x.StorageQuotaGb ? 1 : 0)))
            .FirstOrDefaultAsync(ct);

        return stats ?? new TenantRegistryStats(0, 0, 0, 0, 0, 0, 0);
    }

    private static SortDefinition<Tenant> BuildSort(string sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "-createdAt" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;
        field = field.ToLowerInvariant();

        return field switch
        {
            "code" => descending
                ? Builders<Tenant>.Sort.Descending(x => x.Code)
                : Builders<Tenant>.Sort.Ascending(x => x.Code),
            "name" => descending
                ? Builders<Tenant>.Sort.Descending(x => x.Name)
                : Builders<Tenant>.Sort.Ascending(x => x.Name),
            "status" => descending
                ? Builders<Tenant>.Sort.Descending(x => x.Status)
                : Builders<Tenant>.Sort.Ascending(x => x.Status),
            "updatedat" => descending
                ? Builders<Tenant>.Sort.Descending(x => x.UpdatedAt)
                : Builders<Tenant>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending
                ? Builders<Tenant>.Sort.Descending(x => x.CreatedAt)
                : Builders<Tenant>.Sort.Ascending(x => x.CreatedAt)
        };
    }
}
