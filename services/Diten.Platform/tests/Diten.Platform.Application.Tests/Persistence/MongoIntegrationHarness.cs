using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
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
// ⚠ WHAT CHANGED, AND WHY (2026-08-26). This used to open a database named with a fresh Guid for every test
// class. One run therefore created a database per class; combined with the old EnsureIndexesAsync, which
// built ALL 82 collections and 218 indexes whatever the test touched, a single run walked past the macOS
// 10,240 open-files-per-process limit and mongod killed itself with fassert — and once it died, the
// disposal that drops those databases never ran, so the wreckage was still there for the next run.
//
// Two things fix it, and both are visible in this file:
//   • the harness asks for the SCHEMA PROFILES the test actually uses, not the whole platform;
//   • isolation comes from a fresh TenantId inside ONE shared database, not from a new database name.
//
// ⚠ AND ONE HONEST EXCEPTION, see CreateIsolatedAsync: a test whose SUBJECT is database-global state — a
// platform-default row, an idempotent seed that must produce exactly one row in the whole database — cannot
// be isolated by tenant, because the thing under test is not tenant-scoped. Those get a database of their
// own, under a FIXED name that is dropped and reused rather than a new one per run. Fixed is the property
// that matters: it cannot accumulate.
public sealed class MongoIntegrationHarness : IAsyncDisposable
{
    public const string ConnectionString = "mongodb://localhost:27017";

    /// <summary>The one database every tenant-isolated test shares.</summary>
    public const string SharedDatabaseName = "diten_platform_itest";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);
    private static readonly HashSet<string> AppliedSchemas = new(StringComparer.Ordinal);

    private readonly IMongoClient _client;
    private readonly string? _databaseToDropOnDispose;

    private MongoIntegrationHarness(
        IMongoClient client,
        IMongoDatabase database,
        string databaseName,
        string? databaseToDropOnDispose)
    {
        _client = client;
        Database = database;
        DatabaseName = databaseName;
        _databaseToDropOnDispose = databaseToDropOnDispose;
        TenantContext = new TenantContext();
        TenantContext.SetTenant(TenantId);
        DbContext = new PlatformDbContext(client, database);
    }

    public IMongoDatabase Database { get; }
    public string DatabaseName { get; }
    public Guid TenantId { get; } = Guid.NewGuid();
    public TenantContext TenantContext { get; }
    public IPlatformDbContext DbContext { get; }

    /// <summary>
    /// The default: ONE shared database, a fresh <see cref="TenantId"/> per harness, and only the schema
    /// profiles this test needs. Nothing is dropped — isolation is the tenant, so there is nothing to drop.
    /// </summary>
    public static Task<MongoIntegrationHarness> CreateAsync(params SchemaProfile[] profiles)
        => CreateCoreAsync(SharedDatabaseName, profiles, emptyFirst: false, dropOnDispose: false);

    /// <summary>
    /// For a test whose subject is DATABASE-GLOBAL rather than tenant-scoped — a platform-default row, or a
    /// seed that must be idempotent across the whole database. <paramref name="scope"/> is a fixed suffix,
    /// never a Guid, so it cannot pile up run after run.
    ///
    /// ⚠ IT EMPTIES THE COLLECTIONS; IT DOES NOT DROP THE DATABASE. Both give the test the blank slate it
    /// needs, and they are not remotely the same cost. xUnit runs IAsyncLifetime per TEST, so dropping meant
    /// rebuilding this profile's collections and indexes once per test method — measured at 2,227 files for
    /// one pass over the Persistence tests, which is the same file-count problem this round is fixing, just
    /// moved. Deleting the documents leaves the collections and indexes in place: measured at a handful.
    /// </summary>
    public static Task<MongoIntegrationHarness> CreateIsolatedAsync(string scope, params SchemaProfile[] profiles)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("An isolated harness needs a stable scope name.", nameof(scope));
        }

        return CreateCoreAsync($"{SharedDatabaseName}_{scope}", profiles, emptyFirst: true, dropOnDispose: false);
    }

    private static async Task<MongoIntegrationHarness> CreateCoreAsync(
        string databaseName,
        SchemaProfile[] profiles,
        bool emptyFirst,
        bool dropOnDispose)
    {
        RegisterProductionSerializers();

        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);

        // Fail fast and loudly when Mongo is not reachable, instead of skipping the test.
        await client.GetDatabase(databaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var database = client.GetDatabase(databaseName);
        await EnsureSchemaAsync(database, databaseName, profiles);

        if (emptyFirst)
        {
            await EmptyAsync(database);
        }

        return new MongoIntegrationHarness(
            client,
            database,
            databaseName,
            dropOnDispose ? databaseName : null);
    }

    /*
     * Builds each requested profile at most once per database per process. Test classes run in parallel, so
     * the gate is not decoration — two classes asking for the same profile at the same moment would issue
     * overlapping createIndexes calls.
     *
     * ⚠ PlatformSchemaManifest.For REJECTS an empty request, and that rejection is wanted here: a harness
     * built with no profile would hand the test a database with no indexes, and the test would fail later as
     * a puzzling query result rather than here as "name the profiles you need".
     */
    private static async Task EnsureSchemaAsync(
        IMongoDatabase database,
        string databaseName,
        SchemaProfile[] profiles)
    {
        await SchemaGate.WaitAsync();
        try
        {
            foreach (var profile in profiles.Distinct())
            {
                if (!AppliedSchemas.Add($"{databaseName}:{profile}"))
                {
                    continue;
                }

                await PlatformSchemaManifest.ApplyAsync(database, new[] { profile });
            }
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    /// <summary>
    /// Removes every document from this profile's collections, leaving the collections and their indexes
    /// intact. This is the blank slate — not a dropped database.
    /// </summary>
    private static async Task EmptyAsync(IMongoDatabase database)
    {
        /*
         * ⚠ EVERY COLLECTION IN THE DATABASE, NOT JUST THE PROFILE'S. Repositories built on the generic
         * convention-based base class create collections the manifest never names — task_comments is one —
         * so clearing only the manifest's list would leave rows behind, and a test that seeds a fixed _id
         * would collide with the previous run instead of starting blank.
         */
        foreach (var name in await database.ListCollectionNames().ToListAsync())
        {
            await database.GetCollection<BsonDocument>(name)
                .DeleteManyAsync(Builders<BsonDocument>.Filter.Empty);
        }
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
        if (_databaseToDropOnDispose is not null)
        {
            await _client.DropDatabaseAsync(_databaseToDropOnDispose);
        }
    }
}
