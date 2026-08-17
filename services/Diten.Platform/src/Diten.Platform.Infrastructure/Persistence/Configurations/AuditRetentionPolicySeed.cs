using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Settings;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

internal static class AuditRetentionPolicySeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, AuditRetentionSeedOptions options, CancellationToken ct = default)
    {
        options.Validate();

        var collection = database.GetCollection<AuditEventRetentionPolicy>(AuditCollectionNames.AuditEventRetentionPolicies);
        var categories = Enum.GetValues<AuditCategory>()
            .Where(category => category != AuditCategory.Unknown);

        foreach (var category in categories)
        {
            var filter = Builders<AuditEventRetentionPolicy>.Filter.And(
                Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.Category, category),
                Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.PlanTierCode, AuditEventRetentionPolicy.DefaultPlanTierCode),
                Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false));

            var existing = await collection.Find(filter).FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                var policy = CreateDefaultPolicy(category, options);
                await collection.InsertOneAsync(policy, cancellationToken: ct);
                continue;
            }

            // Existing policy migrations belong in a dedicated migrator, not the idempotent seed.
            existing.Validate();
        }
    }

    private static AuditEventRetentionPolicy CreateDefaultPolicy(AuditCategory category, AuditRetentionSeedOptions options)
    {
        var policy = new AuditEventRetentionPolicy
        {
            Category = category,
            PlanTierCode = AuditEventRetentionPolicy.DefaultPlanTierCode,
            MinimumRetentionDays = options.MinimumRetentionDays,
            DefaultRetentionDays = options.DefaultRetentionDays,
            MaximumRetentionDays = options.MaximumRetentionDays,
            HotStorageDays = options.HotStorageDays,
            AllowTenantOverride = options.AllowTenantOverride,
            IsActive = true,
            ColdStoragePrepared = options.ColdStoragePrepared
        };

        policy.Validate();
        return policy;
    }
}
