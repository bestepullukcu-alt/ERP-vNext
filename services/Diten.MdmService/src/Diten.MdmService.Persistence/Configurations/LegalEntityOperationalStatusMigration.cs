using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Configurations;

// MOD-0220 FULL — backfill. The legacy slice persisted LegalEntityLifecycleStatus (Draft=1/Active=2/Archived=3)
// in the field "LifecycleStatus". The rich entity replaces it with OperationalStatus (Draft=1/InReview=2/
// Approved=3/Active=4/Suspended=5/Archived=6). This maps existing rows onto the new field and removes the old
// one. Idempotent: once migrated, no document carries "LifecycleStatus", so re-runs match nothing.
public static class LegalEntityOperationalStatusMigration
{
    private static readonly (int Legacy, int Operational)[] Map =
    {
        (1, 1), // Draft → Draft
        (2, 4), // Active → Active
        (3, 6), // Archived → Archived
    };

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>("mdm_legal_entities");

        foreach (var (legacy, operational) in Map)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("LifecycleStatus", legacy);
            var update = Builders<BsonDocument>.Update
                .Set("OperationalStatus", operational)
                .Unset("LifecycleStatus");
            await collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        }

        // Safety net: any legacy row whose LifecycleStatus value is outside the known map still loses the stale
        // field and defaults to Draft, rather than lingering as an undeserializable legacy shape.
        var leftover = Builders<BsonDocument>.Filter.Exists("LifecycleStatus");
        var defaultUpdate = Builders<BsonDocument>.Update
            .Set("OperationalStatus", 1)
            .Unset("LifecycleStatus");
        await collection.UpdateManyAsync(leftover, defaultUpdate, cancellationToken: cancellationToken);
    }
}
