using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// MC-2 — one-shot cleanup of duplicate live module-service rows. The Code is the HARD identity and must be
/// unique among live (IsDeleted=false) rows, but a non-idempotent seed (legacy InsertMany) created duplicate
/// 'DITENDOCUMENTSERVICE' rows. For each Code with more than one live row this keeps the oldest (by CreatedAt)
/// and soft-deletes the rest, so the unique partial index (ux_platform_module_services_code) can apply cleanly.
/// Idempotent: once deduplicated no Code has &gt;1 live row, so re-runs are a no-op. Runs BEFORE index creation.
/// </summary>
public static class ModuleServiceDeduplicationMigration
{
    private const string Actor = "system:module-service-dedup";

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<ModuleService>("platform_module_services");

        var live = await collection
            .Find(Builders<ModuleService>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        var duplicateGroups = live
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            // Keep the oldest live row; soft-delete the rest.
            var survivors = group.OrderBy(x => x.CreatedAt).Skip(1).Select(x => x.Id).ToList();
            if (survivors.Count == 0)
            {
                continue;
            }

            var update = Builders<ModuleService>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                .Set(x => x.UpdatedBy, Actor);

            await collection.UpdateManyAsync(
                Builders<ModuleService>.Filter.In(x => x.Id, survivors),
                update,
                cancellationToken: ct);
        }
    }
}
