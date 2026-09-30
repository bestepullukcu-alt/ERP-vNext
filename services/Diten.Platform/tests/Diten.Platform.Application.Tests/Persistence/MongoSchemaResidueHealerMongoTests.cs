using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Sdk;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * BL-482 — THE HEAL, PROVED AGAINST A REAL MONGOD.
 *
 * Measured 2026-09-30 / reproduced 2026-10-01: a unique index went missing in the shared test database, a "the real
 * unique index refuses a duplicate" test inserted its duplicate, the duplicate was ACCEPTED and stayed, and from then
 * on every build of that collection's indexes failed with E11000 — one red test class per run, rotating, never
 * healing. These tests rebuild that exact state in FIXED-NAME databases the harness owns (never a database per run,
 * never the shared database itself — dropping an index there would break whichever class is running beside us).
 */
public sealed class MongoSchemaResidueHealerMongoTests
{
    private const string SeriesIndex = "ux_meeting_series_tenant_name";
    private const string SeriesName = "Haftalık Kalite Toplantısı";

    /// <summary>A database the harness never stamps. Fixed name; dropped by the test that uses it.</summary>
    private const string UnmarkedDatabase = MongoIntegrationHarness.SharedDatabaseName + "_bl482_unmarked";

    [Fact]
    public async Task A_duplicate_left_by_a_run_without_the_index_is_removed_and_the_unique_index_is_built()
    {
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_schema_heal", SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);

        // 1. The measured state: the unique index is gone...
        await series.Indexes.DropOneAsync(SeriesIndex);

        // ...so the precondition every index-refusal test now runs refuses to let the test write its duplicate.
        await Assert.ThrowsAsync<FailException>(() => UniqueIndexPrecondition.RequireAsync(series, SeriesIndex));

        // ...and a duplicate got in while it was gone (written raw, exactly as an unprotected test used to leave it).
        var deadTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        await series.InsertManyAsync(new[]
        {
            Series(deadTenant, isDeleted: false),
            Series(deadTenant, isDeleted: false),       // the duplicate
            Series(deadTenant, isDeleted: true),        // IsDeleted is part of the key: NOT a duplicate, must survive
            Series(otherTenant, isDeleted: false)       // same name, another tenant: NOT a duplicate, must survive
        });

        // 2. The defect reproduces: the production build refuses, on duplicate keys.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PlatformSchemaManifest.ApplyAsync(harness.Database, new[] { SchemaProfile.Meetings }));
        Assert.True(MongoSchemaResidueHealer.IsDuplicateKeyFailure(failure), failure.ToString());

        // 3. The harness heals it: removes exactly the colliding rows, retries once, and the index exists.
        var report = await MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Meetings);

        Assert.NotNull(report);
        Assert.Null(report!.Refusal);
        var healed = Assert.Single(report.Healed);
        Assert.Equal(PlatformCollections.MeetingSeries, healed.Collection);
        Assert.Equal(SeriesIndex, healed.Index);
        Assert.Equal(1, healed.DuplicateKeys);
        Assert.Equal(2, healed.RowsRemoved);

        await UniqueIndexPrecondition.RequireAsync(series, SeriesIndex);
        Assert.Equal(0, await CountAsync(series, deadTenant, isDeleted: false));
        Assert.Equal(1, await CountAsync(series, deadTenant, isDeleted: true));
        Assert.Equal(1, await CountAsync(series, otherTenant, isDeleted: false));

        // 4. And the index now does its job: the same duplicate is refused.
        await series.InsertOneAsync(Series(deadTenant, isDeleted: false));
        await Assert.ThrowsAsync<MongoWriteException>(() => series.InsertOneAsync(Series(deadTenant, isDeleted: false)));
    }

    /// <summary>
    /// CT acceptance (BL-482): most of the platform's unique indexes are PARTIAL (<c>IsDeleted == false</c>), and the heal
    /// test above uses one whose key contains IsDeleted, so the partial filter was never measured. A soft-deleted row
    /// with the same code sits OUTSIDE the index: it is no duplicate and must survive the heal.
    /// </summary>
    [Fact]
    public async Task A_partial_unique_index_heals_only_the_rows_inside_its_filter()
    {
        const string index = "ux_platform_module_domains_code_key";
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_partial_heal", SchemaProfile.Core);
        var domains = harness.Database.GetCollection<BsonDocument>(PlatformCollections.ModuleDomains);
        await domains.Indexes.DropOneAsync(index);

        BsonDocument Domain(bool isDeleted) => new()
        {
            { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "CodeKey", "BL482-PARTIAL" },
            { "IsDeleted", isDeleted }
        };
        await domains.InsertManyAsync(new[] { Domain(false), Domain(false), Domain(true), Domain(true) });

        var report = await MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Core);

        Assert.NotNull(report);
        Assert.Null(report!.Refusal);
        var healed = Assert.Single(report.Healed);
        Assert.Equal(index, healed.Index);
        Assert.Equal(2, healed.RowsRemoved);                                                   // the two live duplicates
        Assert.Equal(2, await domains.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("IsDeleted", true))); // untouched
        await UniqueIndexPrecondition.RequireAsync(domains, index);
    }

    [Fact]
    public async Task A_database_this_run_did_not_stamp_is_never_healed_and_keeps_its_rows()
    {
        // A harness only for its client and the machine-wide lock; the database under test is never stamped.
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        var client = harness.Database.Client;
        await client.DropDatabaseAsync(UnmarkedDatabase); // blank slate if a crashed run left it behind

        try
        {
            var unmarked = client.GetDatabase(UnmarkedDatabase);
            var series = unmarked.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
            var tenant = Guid.NewGuid();
            await series.InsertManyAsync(new[] { Series(tenant, isDeleted: false), Series(tenant, isDeleted: false) });

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MongoIntegrationHarness.ApplyProfileHealingResidueAsync(unmarked, SchemaProfile.Meetings));

            Assert.Contains("E11000", failure.Message);            // the original failure, not swallowed...
            Assert.Contains("not healed", failure.Message);        // ...explicitly NOT healed...
            Assert.Contains("no harness marker", failure.Message); // ...for the right reason...
            Assert.Equal(2, await series.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)); // ...and nothing deleted.
        }
        finally
        {
            await client.DropDatabaseAsync(UnmarkedDatabase);
        }
    }

    [Fact]
    public async Task A_failed_schema_build_is_not_remembered_as_applied_and_the_next_harness_builds_it()
    {
        /*
         * The harness used to record a profile as applied BEFORE building it, so one failed build handed every later
         * class in the run a database with no unique indexes — the state in which index-refusal tests left accepted
         * duplicates behind. A conflicting definition under the manifest's own index name is used to fail the build,
         * because it is NOT residue: it must fail loudly, not be healed.
         */
        const string scope = "bl482_retry";
        var target = MongoIntegrationHarness.SharedDatabaseName + "_" + scope;

        await using (var shared = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings))
        {
            var series = shared.Database.Client.GetDatabase(target).GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
            await DropIndexIfPresentAsync(series, SeriesIndex);
            await series.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Name"),
                new CreateIndexOptions { Name = SeriesIndex }));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => MongoIntegrationHarness.CreateIsolatedAsync(scope, SchemaProfile.Meetings));

            await series.Indexes.DropOneAsync(SeriesIndex);
        }

        await using var second = await MongoIntegrationHarness.CreateIsolatedAsync(scope, SchemaProfile.Meetings);

        await UniqueIndexPrecondition.RequireAsync(
            second.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries), SeriesIndex);
    }

    private static BsonDocument Series(Guid tenantId, bool isDeleted) => new()
    {
        { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
        { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) },
        { "Name", SeriesName },
        { "IsDeleted", isDeleted },
        { "IsActive", true }
    };

    private static Task<long> CountAsync(IMongoCollection<BsonDocument> series, Guid tenantId, bool isDeleted)
        => series.CountDocumentsAsync(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Filter.Eq("IsDeleted", isDeleted)));

    private static async Task DropIndexIfPresentAsync(IMongoCollection<BsonDocument> collection, string indexName)
    {
        var names = (await (await collection.Indexes.ListAsync()).ToListAsync()).Select(i => i["name"].AsString);
        if (names.Contains(indexName))
        {
            await collection.Indexes.DropOneAsync(indexName);
        }
    }
}
