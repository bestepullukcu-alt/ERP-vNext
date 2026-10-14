using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E5B (FU-0022-E4-01) — the startup step that retires E2's per-task UNIQUE index. A database built by E2–E5W1
/// carries <c>ux_esign_records_tenant_workflow_task</c> (unique, partial); Mongo cannot make it non-unique in place, and the
/// same keys under the new name next to it is IndexOptionsConflict. Measured here, on a test-owned replica set (never 27017):
/// first startup drops the exact E2 index and the manifest builds the lookup index; second startup does nothing; an index
/// of that name with ANY other definition is left alone (K3 (c)); no record is touched; production runs the step.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class ESignatureWorkflowTaskIndexMigrationTests : IClassFixture<ESignatureWorkflowTaskIndexMigrationTests.Mongo>
{
    /// <summary>One test-owned replica set for the class; every test takes a database of its own.</summary>
    public sealed class Mongo : IAsyncLifetime
    {
        public DisposableMongoReplicaSet ReplicaSet { get; private set; } = null!;

        public async Task InitializeAsync() => ReplicaSet = await DisposableMongoReplicaSet.StartAsync();

        public async Task DisposeAsync() => await ReplicaSet.DisposeAsync();
    }

    private readonly DisposableMongoReplicaSet _mongo;

    public ESignatureWorkflowTaskIndexMigrationTests(Mongo mongo) => _mongo = mongo.ReplicaSet;

    private static IMongoCollection<BsonDocument> Records(IMongoDatabase db) =>
        db.GetCollection<BsonDocument>(PlatformCollections.ESignatureRecords);

    private static BsonDocument Key => new() { { "TenantId", 1 }, { "Context.workflowTaskId", 1 } };

    private static BsonDocument Partial => new("Context.workflowTaskId", new BsonDocument("$exists", true));

    /// <summary>The index exactly as E2 built it (raw command, so the test does not depend on the new manifest).</summary>
    private static Task CreateRawIndexAsync(IMongoDatabase db, BsonDocument spec) =>
        db.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "createIndexes", PlatformCollections.ESignatureRecords },
            { "indexes", new BsonArray { spec } }
        });

    private static BsonDocument E2Spec() => new()
    {
        { "key", Key }, { "name", ESignatureSchemaMigrations.LegacyWorkflowTaskIndex }, { "unique", true }, { "partialFilterExpression", Partial }
    };

    private static async Task<List<BsonDocument>> IndexesAsync(IMongoDatabase db)
    {
        using var cursor = await Records(db).Indexes.ListAsync();
        return (await cursor.ToListAsync()).OrderBy(i => i["name"].AsString, StringComparer.Ordinal).ToList();
    }

    /// <summary>A minimal record with the fields the other unique indexes key on (number, signer + idempotency key).</summary>
    private static BsonDocument RecordFor(string taskId)
    {
        var id = Guid.NewGuid().ToString("N");
        return new BsonDocument
        {
            { "_id", id }, { "TenantId", "t" }, { "SignatureNumber", "ESIG-2026-" + id[..6] }, { "SignerUserId", "u" },
            { "IdempotencyKey", "k-" + id }, { "Context", new BsonDocument("workflowTaskId", taskId) }
        };
    }

    private static BsonDocument? Named(IEnumerable<BsonDocument> indexes, string name) =>
        indexes.SingleOrDefault(i => i["name"].AsString == name);

    [Fact]
    public async Task First_startup_drops_the_E2_unique_index_and_builds_the_lookup_index_second_startup_does_nothing()
    {
        var db = _mongo.CreateDatabase();
        await CreateRawIndexAsync(db, E2Spec());
        var record = RecordFor("task-1");
        await Records(db).InsertOneAsync(record);
        // before: the E2 index refuses a second signature for the same task
        var refused = await Assert.ThrowsAsync<MongoWriteException>(() => Records(db).InsertOneAsync(RecordFor("task-1")));
        Assert.Contains(ESignatureSchemaMigrations.LegacyWorkflowTaskIndex, refused.Message, StringComparison.Ordinal);
        var log = new List<string>();

        Assert.Equal(LegacyIndexRetirement.Dropped, await ESignatureSchemaMigrations.RetireWorkflowTaskUniqueIndexAsync(db, log.Add));
        await PlatformSchemaManifest.ApplyAsync(db, new[] { SchemaProfile.ESignature });
        var afterFirst = await IndexesAsync(db);

        Assert.Null(Named(afterFirst, ESignatureSchemaMigrations.LegacyWorkflowTaskIndex));
        var lookup = Named(afterFirst, ESignatureSchemaMigrations.WorkflowTaskLookupIndex);
        Assert.NotNull(lookup);
        Assert.False(lookup!.GetValue("unique", false).ToBoolean());
        Assert.Equal(Key, lookup["key"].AsBsonDocument);
        Assert.Equal(Partial, lookup["partialFilterExpression"].AsBsonDocument);
        Assert.Contains(log, l => l.Contains("dropped", StringComparison.Ordinal));

        // the task can now carry a second signature
        await Records(db).InsertOneAsync(RecordFor("task-1"));

        log.Clear();
        Assert.Equal(LegacyIndexRetirement.Absent, await ESignatureSchemaMigrations.RetireWorkflowTaskUniqueIndexAsync(db, log.Add));
        await PlatformSchemaManifest.ApplyAsync(db, new[] { SchemaProfile.ESignature });
        Assert.Equal(afterFirst, await IndexesAsync(db));
        Assert.DoesNotContain(log, l => l.Contains("dropped", StringComparison.Ordinal));

        // the record that existed before the step is byte-identical
        Assert.Equal(record, await Records(db).Find(new BsonDocument("_id", record["_id"])).SingleAsync());
    }

    [Fact]
    public async Task A_fresh_database_is_absent_and_gets_only_the_lookup_index()
    {
        var db = _mongo.CreateDatabase();
        Assert.Equal(LegacyIndexRetirement.Absent, await ESignatureSchemaMigrations.RetireWorkflowTaskUniqueIndexAsync(db, _ => { }));
        await PlatformSchemaManifest.ApplyAsync(db, new[] { SchemaProfile.ESignature });
        var indexes = await IndexesAsync(db);
        Assert.Null(Named(indexes, ESignatureSchemaMigrations.LegacyWorkflowTaskIndex));
        Assert.NotNull(Named(indexes, ESignatureSchemaMigrations.WorkflowTaskLookupIndex));
    }

    /// <summary>K3 (c): only the exact E2 definition is ours to drop. Any index of that name that differs stays.</summary>
    [Theory]
    [InlineData("not-unique")]
    [InlineData("no-partial")]
    [InlineData("other-partial")]
    [InlineData("other-keys")]
    [InlineData("extra-option")]
    public async Task An_index_of_that_name_with_another_definition_is_left_untouched(string variant)
    {
        var db = _mongo.CreateDatabase();
        var spec = E2Spec();
        switch (variant)
        {
            case "not-unique": spec.Remove("unique"); break;
            case "no-partial": spec.Remove("partialFilterExpression"); break;
            case "other-partial": spec["partialFilterExpression"] = new BsonDocument("Context.workflowInstanceId", new BsonDocument("$exists", true)); break;
            case "other-keys": spec["key"] = new BsonDocument { { "TenantId", 1 }, { "Context.workflowTaskId", -1 } }; break;
            case "extra-option": spec["collation"] = new BsonDocument("locale", "en"); break;
        }

        await CreateRawIndexAsync(db, spec);
        var before = await IndexesAsync(db);
        var log = new List<string>();

        Assert.Equal(LegacyIndexRetirement.LeftUntouched, await ESignatureSchemaMigrations.RetireWorkflowTaskUniqueIndexAsync(db, log.Add));
        Assert.Equal(before, await IndexesAsync(db));
        Assert.Contains(log, l => l.Contains("WARNING", StringComparison.Ordinal));
    }

    /// <summary>The production startup path (PlatformSchemaMigrations.RunAsync, before the manifest) runs the step.</summary>
    [Fact]
    public async Task The_production_migration_path_retires_the_E2_index()
    {
        var db = _mongo.CreateDatabase();
        await CreateRawIndexAsync(db, E2Spec());

        await PlatformSchemaMigrations.RunAsync(db);

        Assert.Null(Named(await IndexesAsync(db), ESignatureSchemaMigrations.LegacyWorkflowTaskIndex));
    }
}
