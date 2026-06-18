using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class ModuleCatalogSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        // Minimal, non-destructive seed called at startup (same pattern as SubscriptionPlanSeed/TenantSeed).
        // Idempotent: exists-check by ModuleCode then insert.
        var collection = database.GetCollection<ModuleCatalogItem>("platform_module_catalog");

        var seeds = new[]
        {
            new ModuleCatalogItem
            {
                ModuleCode = "GOLDENSLIM",
                ModuleName = "Golden Reference Slim",
                DisplayName = "Golden Slim",
                Domain = "DevEnablement",
                Service = "DevEnablement",
                Status = ModuleCatalogStatus.Active,
                IsTenantAssignable = true,
                SortOrder = 100
            }
        };

        foreach (var seed in seeds)
        {
            var exists = await collection
                .Find(x => x.IsDeleted == false && x.ModuleCode == seed.ModuleCode)
                .AnyAsync(ct);
            if (exists)
            {
                continue;
            }

            await collection.InsertOneAsync(seed, cancellationToken: ct);
        }
    }
}
