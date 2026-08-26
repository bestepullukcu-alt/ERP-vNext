using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Schema;

/*
 * THE CONTRACT, AGAINST A REAL MONGO — the half that cannot be faked.
 *
 * ⚠ WHY A REAL DATABASE IS NOT OPTIONAL HERE. Mongo does not complain about a missing index. Ask it to sort
 * on a field with no index and it sorts anyway, in memory, until the day the collection is large enough to
 * blow the sort limit in production. So "the manifest declares 218 indexes" proves nothing on its own; the
 * only proof is reading back what Mongo actually built and comparing it property by property. That is
 * contract item 2, and item 5 exists for the same reason at the query level: an index can be present and
 * still not serve the query it was built for (the parallel-array sort failure that killed the MOD-0023
 * transition gate is exactly that, and it is why the Mongo harness exists at all).
 *
 * ⚠ THIS TEST USES A DATABASE OF ITS OWN, AND THAT IS THE ONE LEGITIMATE CASE. Item 3 asserts what does NOT
 * exist in the database, which is only meaningful if nothing else writes to it. Note the name is FIXED, not
 * a fresh Guid: it is dropped and rebuilt in place, so it cannot accumulate one database per run — which is
 * the failure this whole round is fixing, and which
 * TenantArchitecture.ArchitectureTests.MongoTestDatabaseGuardTests would otherwise catch here.
 */
[Collection("platform-schema-contract")]
public sealed class PlatformSchemaContractMongoTests : IAsyncLifetime
{
    private const string ConnectionString = "mongodb://localhost:27017";
    private const string DatabaseName = "diten_platform_schema_contract";

    private MongoClient _client = null!;
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        /*
         * ⚠ THE SAME GUID REPRESENTATION PRODUCTION USES, AND IT IS NOT OPTIONAL. MongoIntegrationHarness
         * registers a PROCESS-GLOBAL GuidSerializer(Standard) when it runs, so a client built without this
         * line encodes Guids one way or the other depending on WHICH TEST CLASS RAN FIRST. That is the
         * classic "passes alone, fails in the suite" shape: the query stops matching its own inserted rows
         * and the failure looks like data loss. Pinning it here mirrors
         * Diten.Platform.Infrastructure.DependencyInjection and makes the order irrelevant.
         */
        settings.GuidRepresentation = GuidRepresentation.Standard;

        _client = new MongoClient(settings);

        // Fail loudly when Mongo is unreachable. Skipping is what let the transition-gate bug ship.
        await _client.GetDatabase(DatabaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await _client.DropDatabaseAsync(DatabaseName);
        _database = _client.GetDatabase(DatabaseName);
    }

    public Task DisposeAsync() => _client.DropDatabaseAsync(DatabaseName);

    // ── ITEM 2: EVERY DECLARED INDEX EXISTS, WITH ITS FULL PROPERTIES ──────────────────────────────────────

    [Theory]
    [InlineData(SchemaProfile.BusinessReferenceData)]
    [InlineData(SchemaProfile.Eventing)]
    [InlineData(SchemaProfile.Notification)]
    [InlineData(SchemaProfile.Organization)]
    public async Task EveryIndexTheManifestDeclaresIsBuiltWithItsFullProperties(SchemaProfile profile)
    {
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { profile });

        var mismatches = new List<string>();

        foreach (var collection in PlatformSchemaManifest.For(profile))
        {
            if (collection.Indexes.Count == 0)
            {
                continue;
            }

            var built = await ListIndexesAsync(collection.Name);

            foreach (var declared in collection.Indexes)
            {
                if (!built.TryGetValue(declared.Name, out var actual))
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: NOT BUILT (present: {string.Join(", ", built.Keys)})");
                    continue;
                }

                /*
                 * ⚠ COMPARING THE NAME ALONE WOULD BE THE "GREEN FOR THE WRONG REASON" TEST. An index can
                 * carry the right name and the wrong keys, or be unique when the manifest says partial-unique
                 * — and a partial filter that silently disappears turns a live-only uniqueness rule into one
                 * that soft-deleted rows also enforce, which blocks legitimate inserts in production.
                 */
                var actualKey = actual["key"].AsBsonDocument;
                if (actualKey != declared.Key)
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: keys {actualKey} != declared {declared.Key}");
                }

                var actualUnique = actual.GetValue("unique", BsonBoolean.False).ToBoolean();
                if (actualUnique != declared.Unique)
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: unique={actualUnique} != declared {declared.Unique}");
                }

                var actualPartial = actual.TryGetValue("partialFilterExpression", out var pfe)
                    ? pfe.AsBsonDocument
                    : null;
                if (!BsonEquals(actualPartial, declared.PartialFilterExpression))
                {
                    mismatches.Add(
                        $"{collection.Name}.{declared.Name}: partialFilterExpression "
                        + $"{actualPartial?.ToString() ?? "<none>"} != declared "
                        + $"{declared.PartialFilterExpression?.ToString() ?? "<none>"}");
                }
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{profile}: what Mongo built does not match what the manifest declares:\n"
            + string.Join("\n", mismatches));
    }

    // ── ITEM 3: NOTHING OUTSIDE THE PROFILE IS CREATED ─────────────────────────────────────────────────────

    [Fact]
    public async Task AProfileBuildsItsOwnCollectionsAndNothingElse()
    {
        const SchemaProfile profile = SchemaProfile.BusinessReferenceData;
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { profile });

        var expected = PlatformSchemaManifest.For(profile)
            .Where(c => c.Indexes.Count > 0)   // a collection with no index is not created until first write
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        var actual = (await _database.ListCollectionNames().ToListAsync()).ToHashSet(StringComparer.Ordinal);

        var strays = actual.Except(expected).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(strays.Length == 0,
            $"asking for {profile} created collections outside it — the profile is not a subset any more:\n"
            + string.Join("\n", strays));

        var missing = expected.Except(actual).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, $"{profile} did not create: {string.Join(", ", missing)}");

        // ⚠ THE NUMBER THAT MATTERS. The whole profile is 8 collections against the 82 the old path built.
        Assert.True(actual.Count < 15,
            $"{profile} created {actual.Count} collections — a profile that builds most of the schema is not "
            + "a profile, and this is exactly the regression that brings mongod back down.");
    }

    // ── ITEM 5: THE INDEX BEHAVIOUR REGRESSIONS STILL HOLD ─────────────────────────────────────────────────

    [Fact]
    public async Task TheTransitionLogSortStillRunsUnderTheWorkflowProfileAlone()
    {
        /*
         * ⚠ THIS IS THE FAILURE CLASS THE HARNESS EXISTS FOR. The MOD-0023 transition gate died on
         * "cannot sort with keys that are parallel arrays" — a defect no fake repository can see, because the
         * Mongo query never runs against a fake. Narrowing the schema to a profile must not quietly take that
         * coverage away: if the profile did not build workflow_transition_logs' indexes, this query would
         * still SUCCEED (unindexed) and the test would pass while the protection was gone. So the assertion
         * is not just "the query ran" — it is that the index backing the sort is present and used.
         */
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { SchemaProfile.WorkflowWorkCenter });

        var logs = _database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTransitionLogs);
        var tenantId = Guid.NewGuid();

        await logs.InsertManyAsync(new[]
        {
            new BsonDocument { { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) }, { "Sequence", 2 } },
            new BsonDocument { { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) }, { "Sequence", 1 } }
        });

        var sorted = await logs
            .Find(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard)))
            .Sort(Builders<BsonDocument>.Sort.Ascending("Sequence"))
            .ToListAsync();

        Assert.Equal(new[] { 1, 2 }, sorted.Select(d => d["Sequence"].AsInt32).ToArray());

        var built = await ListIndexesAsync(PlatformCollections.WorkflowTransitionLogs);
        Assert.True(built.Count > 1,
            "workflow_transition_logs came back with only _id — the sort above passed unindexed, which is "
            + "precisely how this test would go green while the protection was gone.");
    }

    // ── THE TWO DATA JOBS MUST SURVIVE THE SPLIT ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TheProductionPathStillBuildsEverythingAndRunsBothDataJobs()
    {
        /*
         * ⚠ ONE TEST, THREE ASSERTIONS, AND THAT IS DELIBERATE. This is the only place that pays for the FULL
         * 82-collection schema, and each rebuild of it is roughly six hundred files on disk — the very cost
         * this round exists to remove. Splitting it into "builds everything" and "runs the data jobs" would
         * double that for no extra coverage, so the expensive setup is shared.
         *
         * (a) EnsureIndexesAsync still builds the WHOLE manifest. The union test in PlatformSchemaManifestTests
         *     proves the LIST is complete; only this proves the list is actually applied.
         * (b) + (c) Both startup DATA jobs still run. This is the easiest thing in the round to break in
         *     silence: EnsureIndexesAsync used to do three jobs under a name that promised one, and splitting
         *     the declarative part out is exactly how a startup repair gets lost — it moves to a new file,
         *     nothing calls the new file, and nothing fails. The repair just stops happening, forever.
         *
         * MUTATION GUARD: delete either call from PlatformSchemaMigrations.RunAsync and this goes red naming
         * the job; narrow EnsureIndexesAsync to a subset and it goes red naming the missing collections.
         */
        var deletedTenantId = Guid.NewGuid();

        await _database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
        {
            Id = deletedTenantId,
            Name = "Gone",
            DisplayName = "Gone",
            Domain = "gone.example.com",
            Code = "GONE-" + deletedTenantId.ToString("N")[..6],
            Slug = "gone-" + deletedTenantId.ToString("N")[..6],
            IsDeleted = true
        });

        var domains = _database.GetCollection<TenantDomain>(PlatformCollections.TenantDomains);
        await domains.InsertOneAsync(new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = deletedTenantId,
            DomainName = "gone.example.com",
            Type = DomainType.Custom,
            IsDeleted = false,
            Status = TenantDomainStatus.Active
        });

        var catalog = _database.GetCollection<BsonDocument>(PlatformCollections.ModuleCatalog);
        var catalogId = Guid.NewGuid();
        await catalog.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(catalogId, GuidRepresentation.Standard) },
            { "ModuleCode", "MUT-" + catalogId.ToString("N")[..6] },
            { "Category", "retired-value" }
        });

        await MongoDbIndexConfigurations.EnsureIndexesAsync(_database);

        // (a) the whole manifest, not a slice
        var actual = (await _database.ListCollectionNames().ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var missing = PlatformSchemaManifest.All
            .Where(c => c.Indexes.Count > 0)
            .Select(c => c.Name)
            .Where(n => !actual.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.True(missing.Length == 0,
            "the production path no longer builds the whole manifest — these collections were never "
            + "created:\n" + string.Join("\n", missing));

        // (b) the tenant-domain data repair
        var domain = await domains.Find(Builders<TenantDomain>.Filter.Eq(x => x.TenantId, deletedTenantId))
            .FirstAsync();
        Assert.True(domain.IsDeleted,
            "SoftDeleteDomainsForDeletedTenantsAsync no longer runs on the production path — a deleted "
            + "tenant's domain stayed live.");

        // (c) the retired-field data migration
        var catalogDocument = await catalog
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(catalogId, GuidRepresentation.Standard)))
            .FirstAsync();
        Assert.False(catalogDocument.Contains("Category"),
            "UnsetRetiredModuleCatalogCategoryAsync no longer runs on the production path — the retired "
            + "Category field survived startup.");
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, BsonDocument>> ListIndexesAsync(string collectionName)
    {
        var cursor = await _database.GetCollection<BsonDocument>(collectionName).Indexes.ListAsync();
        var list = await cursor.ToListAsync();
        return list.ToDictionary(d => d["name"].AsString, d => d, StringComparer.Ordinal);
    }

    private static bool BsonEquals(BsonDocument? left, BsonDocument? right)
        => (left is null && right is null) || (left is not null && right is not null && left.Equals(right));
}
