using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

// Talks to a REAL MongoDB (localhost:27017 in dev), not a fake. Repository-level defects such as the
// "cannot sort with keys that are parallel arrays" failure that killed the MOD-0023 transition gate are
// invisible to fake repositories — the Mongo query never runs, so the whole suite stays green while the
// feature is dead in production. Tests built on this harness deliberately have no skip-if-unavailable
// escape hatch: a missing Mongo is a broken dev environment, and silently skipping is what let the bug ship.
//
// Each harness instance gets a throwaway database that is dropped on disposal, so nothing touches
// diten_personalization_dev.
public sealed class MongoIntegrationHarness : IAsyncDisposable
{
    public const string ConnectionString = "mongodb://localhost:27017";

    private readonly IMongoClient _client;

    private MongoIntegrationHarness(IMongoClient client, IMongoDatabase database, string databaseName)
    {
        _client = client;
        Database = database;
        DatabaseName = databaseName;
        TenantContext = new TenantContext();
        TenantContext.SetTenant(TenantId);
        DbContext = new PlatformDbContext(client, database);
    }

    public IMongoDatabase Database { get; }
    public string DatabaseName { get; }
    public Guid TenantId { get; } = Guid.NewGuid();
    public TenantContext TenantContext { get; }
    public IPlatformDbContext DbContext { get; }

    public static async Task<MongoIntegrationHarness> CreateAsync()
    {
        RegisterProductionSerializers();

        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);

        var databaseName = "diten_platform_itest_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);

        // Fail fast and loudly when Mongo is not reachable, instead of skipping the test.
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        return new MongoIntegrationHarness(client, database, databaseName);
    }

    // Mirrors Diten.Platform.Infrastructure.DependencyInjection so these tests see exactly the BSON
    // representation production writes. Note what is deliberately ABSENT: no DateTimeOffsetSerializer.
    // Production does not register one either, which is why every DateTimeOffset lands on disk as a BSON
    // array [ticks, offsetMinutes]. Registering one here would make these tests pass against a
    // representation that does not exist in production — see BL-030.
    private static void RegisterProductionSerializers()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DropDatabaseAsync(DatabaseName);
    }
}
