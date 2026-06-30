using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// BOOTSTRAP-ONLY seed for the operator-managed module service collection, gated by a one-time seed marker.
/// The taxonomy is owned by the operator: defaults (from the legacy <see cref="ModuleCatalogService"/> enum) are
/// inserted ONLY on a fresh install (no marker + empty collection). On an existing curated DB (no marker but data
/// present) it preserves the data and just records the marker; once the marker exists it never runs again, so even
/// deleting every service will not bring defaults back on restart. The enum is preserved purely as the default source.
/// (Mirrors ModuleDomainSeed.)
/// </summary>
public static class ModuleServiceSeed
{
    private const string SeedKey = "ModuleService";

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        // Already bootstrapped once → never re-seed (operator may have intentionally deleted everything).
        if (await SeedMarkerStore.ExistsAsync(database, SeedKey, ct))
        {
            return;
        }

        var collection = database.GetCollection<ModuleService>("platform_module_services");
        var hasLive = await collection.Find(x => x.IsDeleted == false).AnyAsync(ct);

        if (BootstrapSeedPolicy.Decide(markerExists: false, hasLiveRecords: hasLive) == SeedDecision.SeedAndMark)
        {
            var values = Enum.GetValues<ModuleCatalogService>();
            var defaults = values.Select((value, index) => new ModuleService
            {
                Code = value.ToString().ToUpperInvariant(),
                DisplayName = GetDisplayName(value),
                SortOrder = (index + 1) * 10,
                IsActive = true
            }).ToList();

            // Idempotent per-Code insert (mirrors SubscriptionPlanSeed): the Code is the HARD identity, so never
            // insert a default whose Code already has a live row — a blind InsertMany is what produced duplicates.
            foreach (var item in defaults)
            {
                var exists = await collection
                    .Find(x => x.IsDeleted == false && x.Code == item.Code)
                    .AnyAsync(ct);
                if (!exists)
                {
                    await collection.InsertOneAsync(item, cancellationToken: ct);
                }
            }
        }

        // Record the marker in BOTH non-skip cases (fresh seed AND preserve-existing) so this never runs again.
        await SeedMarkerStore.SetAsync(database, SeedKey, ct);
    }

    private static string GetDisplayName(ModuleCatalogService value)
    {
        var member = typeof(ModuleCatalogService).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }
}
