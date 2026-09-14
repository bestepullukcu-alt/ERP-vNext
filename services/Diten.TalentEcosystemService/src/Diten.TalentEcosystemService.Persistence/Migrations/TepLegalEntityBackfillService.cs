using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Migrations;

/// <summary>
/// One-shot startup migration for the legal-entity scoping rollout across every scoped TEP collection.
/// For each collection it (1) reconciles the stale unique index that keyed on (TenantId, Code) so the
/// repository can (re)create the scoped (TenantId, LegalEntityId, Code) index, and (2) stamps the tenant
/// root holding onto records created before scoping (LegalEntityId absent or empty). Both steps are
/// idempotent and safe to run on every boot. Collection/index names are discovered by reflection over
/// the repository constants, so new scoped repositories are covered automatically. Mirrors the HCM
/// migration service.
/// </summary>
public sealed class TepLegalEntityBackfillService : IHostedService
{
    // GRAND HOLDING (tenant root) — the backfill target for records that predate legal-entity scoping.
    private static readonly Guid RootHoldingLegalEntityId = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly BsonBinaryData RootHoldingBinary = new(RootHoldingLegalEntityId, GuidRepresentation.Standard);
    private static readonly BsonBinaryData EmptyGuidBinary = new(Guid.Empty, GuidRepresentation.Standard);

    private readonly IMongoClient _mongoClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TepLegalEntityBackfillService> _logger;

    public TepLegalEntityBackfillService(
        IMongoClient mongoClient,
        IConfiguration configuration,
        ILogger<TepLegalEntityBackfillService> logger)
    {
        _mongoClient = mongoClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var databaseName = _configuration["Mongo:DatabaseName"]
                ?? _configuration["MongoDbSettings:DatabaseName"]
                ?? "DitenTalentEcosystem";
            var database = _mongoClient.GetDatabase(databaseName);
            var totalBackfilled = 0L;

            foreach (var (collectionName, uniqueIndexName) in DiscoverScopedCollections())
            {
                var collection = database.GetCollection<BsonDocument>(collectionName);
                await ReconcileStaleUniqueIndexAsync(collection, uniqueIndexName, cancellationToken);
                totalBackfilled += await BackfillLegalEntityAsync(collection, collectionName, cancellationToken);
            }

            if (totalBackfilled > 0)
            {
                _logger.LogInformation(
                    "TEP legal-entity backfill stamped {Count} record(s) to the tenant root holding {LegalEntityId}.",
                    totalBackfilled,
                    RootHoldingLegalEntityId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never block startup on the migration; repositories re-ensure indexes lazily and the
            // backfill can be re-run on the next boot.
            _logger.LogError(ex, "TEP legal-entity backfill migration failed; continuing startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Reads (CollectionName, ActiveCodeUniqueIndexName) const pairs from every Mongo repository.</summary>
    private static IEnumerable<(string CollectionName, string UniqueIndexName)> DiscoverScopedCollections()
    {
        var assembly = typeof(TepLegalEntityBackfillService).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.Namespace is not { } ns || !ns.EndsWith(".Repositories", StringComparison.Ordinal))
            {
                continue;
            }

            var collectionField = type.GetField("CollectionName", BindingFlags.Public | BindingFlags.Static);
            var indexField = type.GetField("ActiveCodeUniqueIndexName", BindingFlags.Public | BindingFlags.Static);
            if (collectionField?.GetRawConstantValue() is string collectionName
                && indexField?.GetRawConstantValue() is string uniqueIndexName)
            {
                yield return (collectionName, uniqueIndexName);
            }
        }
    }

    private async Task<long> BackfillLegalEntityAsync(
        IMongoCollection<BsonDocument> collection,
        string collectionName,
        CancellationToken ct)
    {
        // Records created before scoping have no LegalEntityId field at all; an absent field does not
        // equal the empty-guid binary, so match both the absent-field and empty-value cases.
        var missing = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("LegalEntityId", false),
            Builders<BsonDocument>.Filter.Eq("LegalEntityId", BsonNull.Value),
            Builders<BsonDocument>.Filter.Eq("LegalEntityId", EmptyGuidBinary));
        var update = Builders<BsonDocument>.Update.Set("LegalEntityId", RootHoldingBinary);

        var result = await collection.UpdateManyAsync(missing, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
        {
            _logger.LogInformation(
                "Backfilled {Count} record(s) in {Collection} to the tenant root holding.",
                result.ModifiedCount,
                collectionName);
        }

        return result.ModifiedCount;
    }

    private async Task ReconcileStaleUniqueIndexAsync(
        IMongoCollection<BsonDocument> collection,
        string uniqueIndexName,
        CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(ct);
        var indexes = await cursor.ToListAsync(ct);

        var existing = indexes.FirstOrDefault(index =>
            index.TryGetValue("name", out var name) && name.AsString == uniqueIndexName);
        if (existing is null)
        {
            return;
        }

        // Drop the index only when its key spec no longer matches the scoped (TenantId, LegalEntityId, Code)
        // shape, so the repository's lazy EnsureIndexes can recreate it without an IndexKeySpecsConflict.
        var key = existing.GetValue("key", null)?.AsBsonDocument;
        var isScopedKey = key is not null
            && key.ElementCount == 3
            && key.Contains("TenantId")
            && key.Contains("LegalEntityId")
            && key.Contains("Code");

        if (!isScopedKey)
        {
            await collection.Indexes.DropOneAsync(uniqueIndexName, ct);
            _logger.LogInformation(
                "Dropped stale unique index {IndexName} for legal-entity re-scoping.",
                uniqueIndexName);
        }
    }
}
