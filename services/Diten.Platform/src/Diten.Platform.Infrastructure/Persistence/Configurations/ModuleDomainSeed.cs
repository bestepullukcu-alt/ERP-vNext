using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// BOOTSTRAP-ONLY seed for the operator-managed module domain collection, gated by a one-time seed marker.
/// The taxonomy is owned by the operator: defaults (from the legacy <see cref="ModuleCatalogDomain"/> enum) are
/// inserted ONLY on a fresh install (no marker + empty collection). On an existing curated DB (no marker but data
/// present) it preserves the data and just records the marker; once the marker exists it never runs again, so even
/// deleting every domain will not bring defaults back on restart. The enum is preserved purely as the default source.
/// </summary>
public static class ModuleDomainSeed
{
    private const string SeedKey = "ModuleDomain";

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        // Already bootstrapped once → never re-seed (operator may have intentionally deleted everything).
        if (await SeedMarkerStore.ExistsAsync(database, SeedKey, ct))
        {
            return;
        }

        var collection = database.GetCollection<ModuleDomain>("platform_module_domains");
        var hasLive = await collection.Find(x => x.IsDeleted == false).AnyAsync(ct);

        if (BootstrapSeedPolicy.Decide(markerExists: false, hasLiveRecords: hasLive) == SeedDecision.SeedAndMark)
        {
            var values = Enum.GetValues<ModuleCatalogDomain>();
            var defaults = values.Select((value, index) => new ModuleDomain
            {
                Code = value.ToString().ToUpperInvariant(),
                DisplayName = GetDisplayName(value),
                SortOrder = (index + 1) * 10,
                IsActive = true
            }).ToList();

            await collection.InsertManyAsync(defaults, cancellationToken: ct);
        }

        // Record the marker in BOTH non-skip cases (fresh seed AND preserve-existing) so this never runs again.
        await SeedMarkerStore.SetAsync(database, SeedKey, ct);
    }

    private static string GetDisplayName(ModuleCatalogDomain value)
    {
        var member = typeof(ModuleCatalogDomain).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }
}
