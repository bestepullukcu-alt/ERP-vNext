using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class FeatureCategoryRepository : GlobalRepository<FeatureCategory>, IFeatureCategoryRepository
{
    public FeatureCategoryRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.FeatureCategories)
    {
    }

    public Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureCategory>>
        {
            ExecutionFilter,
            Builders<FeatureCategory>.Filter.Eq(x => x.CategoryCode, categoryCode)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<FeatureCategory>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<FeatureCategory>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> UpdateAsync(FeatureCategory category, byte[]? expectedRowVersion = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureCategory>>
        {
            ExecutionFilter,
            Builders<FeatureCategory>.Filter.Eq(x => x.Id, category.Id)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<FeatureCategory>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        category.UpdatedAt = DateTimeOffset.UtcNow;
        category.RowVersion = Guid.NewGuid().ToByteArray();

        var result = await Collection.ReplaceOneAsync(
            Builders<FeatureCategory>.Filter.And(filters),
            category,
            cancellationToken: ct);

        return result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<FeatureCategory>> GetAllAsync(FeatureCategoryStatus? status = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureCategory>> { ExecutionFilter };

        if (status.HasValue)
        {
            filters.Add(Builders<FeatureCategory>.Filter.Eq(x => x.Status, status.Value));
        }

        return await Collection.Find(Builders<FeatureCategory>.Filter.And(filters))
            .Sort(Builders<FeatureCategory>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.CategoryCode))
            .ToListAsync(ct);
    }
}
