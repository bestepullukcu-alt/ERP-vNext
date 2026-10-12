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
    ///
    /// Round 2: the CT's version used <c>ux_platform_module_domains_code_key</c>, whose key has NO TenantId — the heal
    /// now refuses such an index (see the next test, which keeps that exact case). The partial-filter property is
    /// measured here on a TENANT-keyed partial unique index instead: <c>ux_organization_units_tenant_code_active</c>.
    /// </summary>
    [Fact]
    public async Task A_partial_unique_index_heals_only_the_rows_inside_its_filter()
    {
        const string index = "ux_organization_units_tenant_code_active";
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_partial_heal", SchemaProfile.Organization);
        var units = harness.Database.GetCollection<BsonDocument>(PlatformCollections.OrganizationUnits);
        await units.Indexes.DropOneAsync(index);

        var finishedTenant = Guid.NewGuid();
        BsonDocument Unit(bool isDeleted) => new()
        {
            { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(finishedTenant, GuidRepresentation.Standard) },
            { "Code", "BL482-PARTIAL" },
            { "IsDeleted", isDeleted }
        };
        await units.InsertManyAsync(new[] { Unit(false), Unit(false), Unit(true), Unit(true) });

        var report = await MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Organization);

        Assert.NotNull(report);
        Assert.Null(report!.Refusal);
        var healed = Assert.Single(report.Healed);
        Assert.Equal(index, healed.Index);
        Assert.Equal(2, healed.RowsRemoved);                                                 // the two live duplicates
        Assert.Equal(2, await units.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("IsDeleted", true))); // untouched
        await UniqueIndexPrecondition.RequireAsync(units, index);
    }

    /// <summary>
    /// Round 2 (M2): a unique index whose key has NO TenantId groups rows ACROSS tenants — tenant code, catalog code,
    /// event id, seed marker. Nothing proves such a group is residue, so the heal refuses, deletes nothing, and throws
    /// with the reason: a human decides. (The CT's original partial-index case, kept as the refusal it now is.)
    /// </summary>
    [Fact]
    public async Task A_duplicate_on_a_global_unique_key_is_refused_and_kept()
    {
        const string index = "ux_platform_module_domains_code_key";
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_partial_heal", SchemaProfile.Core);
        var domains = harness.Database.GetCollection<BsonDocument>(PlatformCollections.ModuleDomains);
        var mine = Builders<BsonDocument>.Filter.Eq("CodeKey", "BL482-GLOBAL");
        await domains.Indexes.DropOneAsync(index);
        try
        {
            BsonDocument Domain() => new()
            {
                { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "CodeKey", "BL482-GLOBAL" },
                { "IsDeleted", false }
            };
            await domains.InsertManyAsync(new[] { Domain(), Domain() });

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Core));

            Assert.Contains("E11000", refused.Message);
            Assert.Contains("not healed", refused.Message);
            Assert.Contains($"'{PlatformCollections.ModuleDomains}.{index}' has no TenantId in its key", refused.Message);
            Assert.Equal(2, await domains.CountDocumentsAsync(mine));
        }
        finally
        {
            await domains.DeleteManyAsync(mine);
            await PlatformSchemaManifest.ApplyAsync(harness.Database, new[] { SchemaProfile.Core });
        }
    }

    /// <summary>
    /// Round 2 (M2): a tenant a harness of THIS process handed out is a LIVE test's tenant — possibly being written
    /// right now by a class that never asked for this profile. Its rows are never residue, duplicate or not.
    /// </summary>
    [Fact]
    public async Task A_duplicate_group_of_a_live_tenant_of_this_process_is_refused_and_kept()
    {
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_schema_heal", SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
        await series.Indexes.DropOneAsync(SeriesIndex);
        try
        {
            await series.InsertManyAsync(new[] { Series(harness.TenantId, isDeleted: false), Series(harness.TenantId, isDeleted: false) });

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Meetings));

            Assert.Contains("not healed", refused.Message);
            Assert.Contains($"belongs to tenant {harness.TenantId}, which a harness of THIS process handed out", refused.Message);
            Assert.Equal(2, await CountAsync(series, harness.TenantId, isDeleted: false));
        }
        finally
        {
            await RestoreMeetingsAsync(harness);
        }
    }

    /// <summary>
    /// Round 2 (M2): the healer touches ONLY the index the failure names. Residue on a second index of the same profile
    /// is left for the failure that names IT — the first version deleted duplicates on every unique index it could see.
    /// </summary>
    [Fact]
    public async Task Only_the_index_the_failure_names_is_healed()
    {
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_schema_heal", SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
        var minutes = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingMinutesVersions);
        await series.Indexes.DropOneAsync(SeriesIndex);
        await minutes.Indexes.DropOneAsync(MinutesIndex);
        try
        {
            var finishedTenant = Guid.NewGuid();
            await series.InsertManyAsync(new[] { Series(finishedTenant, isDeleted: false), Series(finishedTenant, isDeleted: false) });
            await minutes.InsertManyAsync(new[] { Minutes(finishedTenant), Minutes(finishedTenant) });

            var report = await MongoSchemaResidueHealer.HealAsync(
                harness.Database,
                SchemaProfile.Meetings,
                new DuplicateKeyTarget(PlatformCollections.MeetingSeries, SeriesIndex),
                MongoIntegrationHarness.CurrentRunId,
                PlatformMongoTestLock.HoldsRealLock,
                MongoIntegrationHarness.IsLiveTenant);

            Assert.Null(report.Refusal);
            Assert.Equal(SeriesIndex, Assert.Single(report.Healed).Index);
            Assert.Equal(0, await CountAsync(series, finishedTenant, isDeleted: false));
            Assert.Equal(2, await minutes.CountDocumentsAsync(TenantIs(finishedTenant)));   // not named: untouched
        }
        finally
        {
            await RestoreMeetingsAsync(harness);
        }
    }

    /// <summary>
    /// Round 2: residue on TWO indexes of one profile is healed in one call — each index only once the build NAMES it
    /// (minutes first, then series, in manifest order) — instead of one red test class per residue index.
    /// </summary>
    [Fact]
    public async Task Two_failing_indexes_are_each_healed_as_the_build_names_them()
    {
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_schema_heal", SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
        var minutes = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingMinutesVersions);
        await series.Indexes.DropOneAsync(SeriesIndex);
        await minutes.Indexes.DropOneAsync(MinutesIndex);
        try
        {
            var finishedTenant = Guid.NewGuid();
            await series.InsertManyAsync(new[] { Series(finishedTenant, isDeleted: false), Series(finishedTenant, isDeleted: false) });
            await minutes.InsertManyAsync(new[] { Minutes(finishedTenant), Minutes(finishedTenant) });

            var report = await MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Meetings);

            Assert.NotNull(report);
            Assert.Equal(new[] { MinutesIndex, SeriesIndex }, report!.Healed.Select(h => h.Index));
            Assert.Equal(4, report.RowsRemoved);
            await UniqueIndexPrecondition.RequireAsync(series, SeriesIndex);
            await UniqueIndexPrecondition.RequireAsync(minutes, MinutesIndex);
        }
        finally
        {
            await RestoreMeetingsAsync(harness);
        }
    }

    /// <summary>
    /// Round 2 (L3): each index is logged the moment it is healed, and a heal that stops midway — here the second index
    /// belongs to a live tenant — says, with BL-482 context, what it had ALREADY removed.
    /// </summary>
    [Fact]
    public async Task A_heal_that_stops_midway_names_what_it_already_removed_and_logged_it_at_once()
    {
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync("bl482_schema_heal", SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
        var minutes = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingMinutesVersions);
        await series.Indexes.DropOneAsync(SeriesIndex);
        await minutes.Indexes.DropOneAsync(MinutesIndex);
        try
        {
            var finishedTenant = Guid.NewGuid();
            await minutes.InsertManyAsync(new[] { Minutes(finishedTenant), Minutes(finishedTenant) });                   // healable
            await series.InsertManyAsync(new[] { Series(harness.TenantId, isDeleted: false), Series(harness.TenantId, isDeleted: false) }); // live
            var log = new StringWriter();

            var stopped = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Meetings, log));

            Assert.Contains("not healed", stopped.Message);
            Assert.Contains(
                $"BL-482 had already removed 2 row(s) from '{PlatformCollections.MeetingMinutesVersions}.{MinutesIndex}'",
                stopped.Message);
            Assert.Contains($"BL-482 removed 2 residue row(s) (1 duplicated key(s)) blocking unique index '{MinutesIndex}'", log.ToString());
            Assert.Equal(0, await minutes.CountDocumentsAsync(TenantIs(finishedTenant)));
            Assert.Equal(2, await CountAsync(series, harness.TenantId, isDeleted: false));
        }
        finally
        {
            await RestoreMeetingsAsync(harness);
        }
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

    private const string MinutesIndex = "ux_meeting_minutes_versions_tenant_meeting_version";
    private static readonly Guid MinutesMeeting = Guid.Parse("48200000-0000-0000-0000-000000000482");

    private static BsonDocument Minutes(Guid tenantId) => new()
    {
        { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
        { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) },
        { "MeetingId", new BsonBinaryData(MinutesMeeting, GuidRepresentation.Standard) },
        { "VersionNumber", 1 },
        { "IsDeleted", false }
    };

    private static FilterDefinition<BsonDocument> TenantIs(Guid tenantId)
        => Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard));

    /// <summary>
    /// Leaves the shared-by-this-class scoped database as a test found it: seeded rows gone, every Meetings index back.
    /// Without this a refusal test would leave the database unbuildable for the next harness that opens it.
    /// </summary>
    private static async Task RestoreMeetingsAsync(MongoIntegrationHarness harness)
    {
        await harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingMinutesVersions).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await PlatformSchemaManifest.ApplyAsync(harness.Database, new[] { SchemaProfile.Meetings });
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
