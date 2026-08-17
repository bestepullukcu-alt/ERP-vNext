using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlanFeatureMappingRepository : GlobalRepository<PlanFeatureMapping>, IPlanFeatureMappingRepository
{
    public PlanFeatureMappingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_plan_feature_mappings")
    {
    }

    public async Task<IReadOnlyList<PlanFeatureMapping>> GetByPlanIdAsync(Guid subscriptionPlanId, CancellationToken ct = default)
    {
        var filter = Builders<PlanFeatureMapping>.Filter.And(
            ExecutionFilter,
            Builders<PlanFeatureMapping>.Filter.Eq(x => x.SubscriptionPlanId, subscriptionPlanId));

        return await Collection.Find(filter)
            .Sort(Builders<PlanFeatureMapping>.Sort.Ascending(x => x.FeatureDefinitionId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PlanFeatureMapping>> GetByFeatureIdAsync(Guid featureDefinitionId, CancellationToken ct = default)
    {
        var filter = Builders<PlanFeatureMapping>.Filter.And(
            ExecutionFilter,
            Builders<PlanFeatureMapping>.Filter.Eq(x => x.FeatureDefinitionId, featureDefinitionId));

        return await Collection.Find(filter)
            .Sort(Builders<PlanFeatureMapping>.Sort.Ascending(x => x.SubscriptionPlanId))
            .ToListAsync(ct);
    }

    public Task<PlanFeatureMapping?> GetByPlanAndFeatureAsync(Guid subscriptionPlanId, Guid featureDefinitionId, CancellationToken ct = default)
    {
        var filter = Builders<PlanFeatureMapping>.Filter.And(
            ExecutionFilter,
            Builders<PlanFeatureMapping>.Filter.Eq(x => x.SubscriptionPlanId, subscriptionPlanId),
            Builders<PlanFeatureMapping>.Filter.Eq(x => x.FeatureDefinitionId, featureDefinitionId));

        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<bool> UpsertAsync(PlanFeatureMapping mapping, byte[]? expectedRowVersion = null, CancellationToken ct = default)
    {
        var existing = await GetByPlanAndFeatureAsync(mapping.SubscriptionPlanId, mapping.FeatureDefinitionId, ct);
        if (existing is null)
        {
            mapping.RowVersion = Guid.NewGuid().ToByteArray();
            await CreateAsync(mapping, ct);
            return true;
        }

        if (expectedRowVersion is { Length: > 0 } && !existing.RowVersion.SequenceEqual(expectedRowVersion))
        {
            return false;
        }

        mapping.RowVersion = Guid.NewGuid().ToByteArray();
        var filters = new List<FilterDefinition<PlanFeatureMapping>>
        {
            ExecutionFilter,
            Builders<PlanFeatureMapping>.Filter.Eq(x => x.Id, existing.Id)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<PlanFeatureMapping>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        var update = Builders<PlanFeatureMapping>.Update
            .Set(x => x.AvailabilityStatus, mapping.AvailabilityStatus)
            .Set(x => x.EffectiveFromUtc, mapping.EffectiveFromUtc)
            .Set(x => x.EffectiveToUtc, mapping.EffectiveToUtc)
            .Set(x => x.RowVersion, mapping.RowVersion)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateOneAsync(
            Builders<PlanFeatureMapping>.Filter.And(filters),
            update,
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
}
