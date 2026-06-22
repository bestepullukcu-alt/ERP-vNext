using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// A persisted "this seed already ran once" marker. Document in <c>platform_seed_markers</c>, keyed by a unique
/// <see cref="Key"/>. Lets bootstrap-only seeds be truly one-time: once the marker exists the seed never runs again,
/// even if the operator later deletes every record (closing the "0 live = fresh install" false assumption).
/// </summary>
public sealed class SeedMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; set; }
}

public static class SeedMarkerStore
{
    public const string CollectionName = "platform_seed_markers";

    public static async Task<bool> ExistsAsync(IMongoDatabase database, string key, CancellationToken ct = default)
    {
        var collection = database.GetCollection<SeedMarker>(CollectionName);
        return await collection.Find(x => x.Key == key).AnyAsync(ct);
    }

    /// <summary>Idempotent: inserts the marker once; a concurrent duplicate insert is swallowed (Key is unique).</summary>
    public static async Task SetAsync(IMongoDatabase database, string key, CancellationToken ct = default)
    {
        var collection = database.GetCollection<SeedMarker>(CollectionName);
        if (await collection.Find(x => x.Key == key).AnyAsync(ct))
        {
            return;
        }

        try
        {
            await collection.InsertOneAsync(
                new SeedMarker { Key = key, AppliedAtUtc = DateTimeOffset.UtcNow },
                cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance set the marker concurrently — fine, it exists now.
        }
    }
}
