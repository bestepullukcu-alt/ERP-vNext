using System.Text.RegularExpressions;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class FeatureDefinitionRepository : GlobalRepository<FeatureDefinition>, IFeatureDefinitionRepository
{
    public FeatureDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_subscription_features")
    {
    }

    public Task<bool> ExistsByCodeAsync(string featureCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureDefinition>>
        {
            ExecutionFilter,
            Builders<FeatureDefinition>.Filter.Eq(x => x.FeatureCode, featureCode)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<FeatureDefinition>.Filter.And(filters)).AnyAsync(ct);
    }

    public Task<bool> ExistsBySlugAsync(string featureSlug, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureDefinition>>
        {
            ExecutionFilter,
            Builders<FeatureDefinition>.Filter.Eq(x => x.FeatureSlug, featureSlug)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<FeatureDefinition>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> UpdateAsync(FeatureDefinition feature, byte[]? expectedRowVersion = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureDefinition>>
        {
            ExecutionFilter,
            Builders<FeatureDefinition>.Filter.Eq(x => x.Id, feature.Id)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        feature.UpdatedAt = DateTimeOffset.UtcNow;
        feature.RowVersion = Guid.NewGuid().ToByteArray();

        var result = await Collection.ReplaceOneAsync(
            Builders<FeatureDefinition>.Filter.And(filters),
            feature,
            cancellationToken: ct);

        return result.ModifiedCount == 1;
    }

    public async Task<(IReadOnlyList<FeatureDefinition> Items, long TotalCount)> QueryAsync(FeatureDefinitionsQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<FeatureDefinition>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<FeatureDefinition>.Filter.Or(
                Builders<FeatureDefinition>.Filter.Regex(x => x.FeatureCode, regex),
                Builders<FeatureDefinition>.Filter.Regex(x => x.FeatureSlug, regex),
                Builders<FeatureDefinition>.Filter.Regex(x => x.DisplayName, regex),
                Builders<FeatureDefinition>.Filter.Regex(x => x.Description, regex)));
        }

        if (query.CategoryId.HasValue)
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Eq(x => x.CategoryId, query.CategoryId.Value));
        }

        if (query.Status.HasValue)
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Eq(x => x.Status, query.Status.Value));
        }

        if (query.IsCoreFeature.HasValue)
        {
            filters.Add(Builders<FeatureDefinition>.Filter.Eq(x => x.IsCoreFeature, query.IsCoreFeature.Value));
        }

        var filter = Builders<FeatureDefinition>.Filter.And(filters);
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

    private static SortDefinition<FeatureDefinition> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "featurecode" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.FeatureCode) : Builders<FeatureDefinition>.Sort.Ascending(x => x.FeatureCode),
            "featureslug" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.FeatureSlug) : Builders<FeatureDefinition>.Sort.Ascending(x => x.FeatureSlug),
            "displayname" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.DisplayName) : Builders<FeatureDefinition>.Sort.Ascending(x => x.DisplayName),
            "status" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.Status) : Builders<FeatureDefinition>.Sort.Ascending(x => x.Status),
            "createdat" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.CreatedAt) : Builders<FeatureDefinition>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.UpdatedAt) : Builders<FeatureDefinition>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<FeatureDefinition>.Sort.Descending(x => x.SortOrder) : Builders<FeatureDefinition>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.FeatureCode)
        };
    }
}
