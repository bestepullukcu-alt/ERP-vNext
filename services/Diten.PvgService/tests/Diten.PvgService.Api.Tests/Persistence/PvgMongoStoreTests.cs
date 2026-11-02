using System.Collections;
using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

public enum PvgStoreKind
{
    InMemory,
    Mongo
}

// WP-0230-DL10, AC-35 (a), (b) first test, T1, T2, AC-13 (b), (d): the Mongo store works as the store contract says,
// on the one replica set the test run started. Tests share one database and isolate by a fresh TenantId.
[Collection(PvgSharedMongoCollection.Name)]
public sealed class PvgMongoStoreTests(PvgMongoReplicaSetFixture mongo)
{
    private const string ActorA = "actor-a-fake";
    private const string ActorB = "actor-b-fake";

    private IPvgIntakeDraftStore StoreOf(PvgStoreKind kind) =>
        kind == PvgStoreKind.Mongo ? mongo.Store : new InMemoryPvgIntakeDraftRepository();

    // ---- AC-35 (a): the store contract tests run on both stores ------------------------------------------------

    [Theory]
    [InlineData(PvgStoreKind.InMemory)]
    [InlineData(PvgStoreKind.Mongo)]
    public async Task Mongo_ReplaceAsync_with_stale_version_returns_conflict_not_not_found(PvgStoreKind kind)
    {
        var store = StoreOf(kind);
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var intakeDraftId = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));
        var initialVersion = (await store.FindByIdAsync(scope, intakeDraftId))!.Version;

        var current = await store.ReplaceAsync(
            scope, intakeDraftId, initialVersion, ActorA, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.IntakeUpdated));
        var stale = await store.ReplaceAsync(
            scope, intakeDraftId, initialVersion, ActorA, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.Triaged));
        var withoutVersion = await store.ReplaceAsync(
            scope, intakeDraftId, null, ActorA, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.Triaged));
        var persisted = await store.FindByIdAsync(scope, intakeDraftId);

        Assert.Equal(1, initialVersion);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced, current.Outcome);
        Assert.Equal(initialVersion + 1, current.Version);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.VersionConflict, stale.Outcome);
        Assert.NotEqual(PvgIntakeDraftReplaceOutcome.NotFound, stale.Outcome);
        Assert.Null(stale.Version);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.VersionConflict, withoutVersion.Outcome);
        Assert.NotNull(persisted);
        Assert.Equal(PvgIntakeStatus.IntakeUpdated, persisted.Intake.Status);
        Assert.Equal(current.Version, persisted.Version);
    }

    [Theory]
    [InlineData(PvgStoreKind.InMemory)]
    [InlineData(PvgStoreKind.Mongo)]
    public async Task Mongo_list_orders_by_received_descending_then_draft_id_and_pages(PvgStoreKind kind)
    {
        var store = StoreOf(kind);
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var origin = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        // Five drafts; two share a received time, so the draft id decides their order.
        var minutes = new[] { 0, 2, 2, 1, 3 };
        var created = new List<(string Id, DateTimeOffset Received, PvgIntakeStatus Status)>();
        for (var index = 0; index < minutes.Length; index++)
        {
            var received = origin.AddMinutes(minutes[index]);
            var status = index % 2 == 0 ? PvgIntakeStatus.IntakeCreated : PvgIntakeStatus.IntakeUpdated;
            var id = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant, status, received));
            created.Add((id, received, status));
        }

        var expected = created
            .OrderByDescending(item => item.Received)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        var all = await store.ListAsync(new PvgPersistenceListScope(scope, 1, 100, null));
        Assert.Equal(expected.Select(item => item.Id), all.Select(item => item.IntakeDraftId));

        var pages = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var slice = await store.ListAsync(new PvgPersistenceListScope(scope, page, 2, null));
            pages.AddRange(slice.Select(item => item.IntakeDraftId));
            Assert.Equal(page < 3 ? 2 : 1, slice.Count);
        }

        Assert.Equal(expected.Select(item => item.Id), pages);
        Assert.Empty(await store.ListAsync(new PvgPersistenceListScope(scope, 4, 2, null)));

        var updatedOnly = await store.ListAsync(
            new PvgPersistenceListScope(scope, 1, 100, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(
            expected.Where(item => item.Status == PvgIntakeStatus.IntakeUpdated).Select(item => item.Id),
            updatedOnly.Select(item => item.IntakeDraftId));
    }

    [Theory]
    [InlineData(PvgStoreKind.InMemory)]
    [InlineData(PvgStoreKind.Mongo)]
    public async Task Mongo_count_with_and_without_status_matches_the_in_memory_store(PvgStoreKind kind)
    {
        var store = StoreOf(kind);
        var tenantA = PvgMongoTestData.NewTenant();
        var tenantB = PvgMongoTestData.NewTenant();
        var scopeA = new PvgPersistenceTenantScope(tenantA);
        var scopeB = new PvgPersistenceTenantScope(tenantB);

        await store.AddAsync(scopeA, ActorA, PvgMongoTestData.Intake(tenantA, PvgIntakeStatus.IntakeCreated));
        await store.AddAsync(scopeA, ActorA, PvgMongoTestData.Intake(tenantA, PvgIntakeStatus.IntakeCreated));
        await store.AddAsync(scopeA, ActorA, PvgMongoTestData.Intake(tenantA, PvgIntakeStatus.IntakeUpdated));
        for (var index = 0; index < 3; index++)
        {
            await store.AddAsync(scopeB, ActorA, PvgMongoTestData.Intake(tenantB, PvgIntakeStatus.IntakeUpdated));
        }

        await store.AddAsync(scopeB, ActorA, PvgMongoTestData.Intake(tenantB, PvgIntakeStatus.IntakeCreated));

        Assert.Equal(3, await store.CountAsync(scopeA, null));
        Assert.Equal(2, await store.CountAsync(scopeA, PvgIntakeStatus.IntakeCreated));
        Assert.Equal(1, await store.CountAsync(scopeA, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(0, await store.CountAsync(scopeA, PvgIntakeStatus.Triaged));
        Assert.Equal(4, await store.CountAsync(scopeB, null));
        Assert.Equal(3, await store.CountAsync(scopeB, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(0, await store.CountAsync(new PvgPersistenceTenantScope(PvgMongoTestData.NewTenant()), null));
    }

    // Behaviour the Mongo store shares with the in-memory store (pack: same failure codes, same outcomes).
    [Theory]
    [InlineData(PvgStoreKind.InMemory)]
    [InlineData(PvgStoreKind.Mongo)]
    public async Task Store_edge_cases_are_the_same_on_both_stores(PvgStoreKind kind)
    {
        var store = StoreOf(kind);
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var intake = PvgMongoTestData.Intake(tenant);

        var unknown = await store.ReplaceAsync(scope, "pvg-draft-" + Guid.NewGuid().ToString("N"), 1, ActorA, intake);
        var blank = await store.ReplaceAsync(scope, " ", 1, ActorA, intake);
        var unknownWithoutVersion = await store.ReplaceAsync(scope, "pvg-draft-" + Guid.NewGuid().ToString("N"), null, ActorA, intake);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, unknown.Outcome);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, blank.Outcome);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, unknownWithoutVersion.Outcome);
        Assert.Null(await store.FindByIdAsync(scope, " "));
        Assert.Null(await store.FindByIdAsync(scope, "pvg-draft-" + Guid.NewGuid().ToString("N")));

        var missingScope = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.AddAsync(new PvgPersistenceTenantScope(" "), ActorA, intake));
        Assert.Equal("PVG_PERSISTENCE_TENANT_SCOPE_REQUIRED", missingScope.Message);

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(PvgMongoTestData.NewTenant())));
        Assert.Equal("PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH", mismatch.Message);
        Assert.DoesNotContain(tenant, mismatch.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- T1: every SafetyCaseIntake field survives a write and a read ------------------------------------------

    [Fact]
    public async Task Mongo_store_round_trips_every_SafetyCaseIntake_field()
    {
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var written = PvgMongoTestData.FullIntake(tenant);

        // The fixture fills every property with a non-default value, so a property added to SafetyCaseIntake later
        // that the fixture does not fill breaks this test, and so does a property the mapping forgets.
        var properties = typeof(SafetyCaseIntake).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.NotEmpty(properties);
        foreach (var property in properties)
        {
            AssertFilled(property.Name, property.GetValue(written), property.PropertyType);
        }

        foreach (var property in typeof(PvgIntakeProductLine).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var line in written.ProductLines)
            {
                AssertFilled("ProductLines." + property.Name, property.GetValue(line), property.PropertyType);
            }
        }

        var intakeDraftId = await mongo.Store.AddAsync(scope, ActorA, written);
        var read = (await mongo.Store.FindByIdAsync(scope, intakeDraftId))!.Intake;

        foreach (var property in properties)
        {
            var expected = property.GetValue(written);
            var actual = property.GetValue(read);
            if (expected is IEnumerable expectedItems and not string)
            {
                Assert.Equal(
                    expectedItems.Cast<object?>().ToArray(),
                    ((IEnumerable)actual!).Cast<object?>().ToArray());
            }
            else
            {
                Assert.True(Equals(expected, actual), $"{property.Name}: expected '{expected}', read '{actual}'");
            }
        }

        // The instants come back exactly, sub-millisecond ticks included.
        Assert.Equal(written.ReceivedAtUtc.UtcTicks, read.ReceivedAtUtc.UtcTicks);
        Assert.Equal(written.AwarenessAtUtc.UtcTicks, read.AwarenessAtUtc.UtcTicks);
    }

    private static void AssertFilled(string name, object? value, Type type)
    {
        Assert.True(value is not null, $"{name} is null in the fixture");
        if (value is string text)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{name} is empty in the fixture");
        }
        else if (value is IEnumerable items)
        {
            Assert.True(items.Cast<object?>().Any(), $"{name} is empty in the fixture");
        }
        else if (type.IsValueType)
        {
            Assert.False(Equals(value, Activator.CreateInstance(type)), $"{name} is the default value in the fixture");
        }
    }

    // ---- T2: one winner on the same version ----------------------------------------------------------------------

    [Fact]
    public async Task Mongo_concurrent_replace_of_the_same_version_has_one_winner()
    {
        var store = mongo.Store;
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);

        for (var round = 0; round < 30; round++)
        {
            var intakeDraftId = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));
            var start = new TaskCompletionSource();
            var calls = new[] { ActorA, ActorB }
                .Select(actor => Task.Run(async () =>
                {
                    await start.Task;
                    return await store.ReplaceAsync(
                        scope,
                        intakeDraftId,
                        1,
                        actor,
                        PvgMongoTestData.Intake(tenant, PvgIntakeStatus.IntakeUpdated));
                }))
                .ToArray();
            start.SetResult();

            var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(60));
            var persisted = await store.FindByIdAsync(scope, intakeDraftId);

            Assert.Equal(1, results.Count(result => result.Outcome == PvgIntakeDraftReplaceOutcome.Replaced));
            Assert.Equal(1, results.Count(result => result.Outcome == PvgIntakeDraftReplaceOutcome.VersionConflict));
            Assert.Equal(2, persisted!.Version);
            Assert.Equal(2, results.Single(result => result.Outcome == PvgIntakeDraftReplaceOutcome.Replaced).Version);
        }
    }

    // ---- AC-35 (b): tenant isolation ------------------------------------------------------------------------------

    [Fact]
    public async Task Mongo_store_never_reads_counts_or_replaces_another_tenants_draft()
    {
        var store = mongo.Store;
        var tenantA = PvgMongoTestData.NewTenant();
        var tenantB = PvgMongoTestData.NewTenant();
        var scopeA = new PvgPersistenceTenantScope(tenantA);
        var scopeB = new PvgPersistenceTenantScope(tenantB);

        var idA = await store.AddAsync(scopeA, ActorA, PvgMongoTestData.Intake(tenantA, narrative: "tenant-a-narrative"));
        await store.AddAsync(scopeB, ActorB, PvgMongoTestData.Intake(tenantB, PvgIntakeStatus.IntakeUpdated));

        // Positive control: the owner reads its own draft.
        Assert.NotNull(await store.FindByIdAsync(scopeA, idA));

        Assert.Null(await store.FindByIdAsync(scopeB, idA));
        var listB = await store.ListAsync(new PvgPersistenceListScope(scopeB, 1, 100, null));
        Assert.Single(listB);
        Assert.DoesNotContain(listB, item => item.IntakeDraftId == idA);
        Assert.Equal(1, await store.CountAsync(scopeB, null));
        Assert.Equal(0, await store.CountAsync(scopeB, PvgIntakeStatus.IntakeCreated));
        Assert.Equal(1, await store.CountAsync(scopeA, null));

        var crossReplace = await store.ReplaceAsync(
            scopeB, idA, 1, ActorB, PvgMongoTestData.Intake(tenantB, PvgIntakeStatus.Triaged));
        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, crossReplace.Outcome);

        var after = await store.FindByIdAsync(scopeA, idA);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, after!.Intake.Status);
        Assert.Equal(1, after.Version);
        Assert.Equal("tenant-a-narrative", after.Intake.AdverseEventNarrative);
    }

    // ---- AC-13 (d): soft delete --------------------------------------------------------------------------------------

    [Fact]
    public async Task Mongo_store_never_reads_counts_or_replaces_a_soft_deleted_draft()
    {
        var store = mongo.Store;
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var deletedId = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant, narrative: "soft-deleted-narrative"));
        var liveId = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));

        // Positive control before the marker: both drafts are visible.
        Assert.Equal(2, await store.CountAsync(scope, null));
        Assert.NotNull(await store.FindByIdAsync(scope, deletedId));

        var drafts = RawDrafts();
        var marked = await drafts.UpdateOneAsync(
            new BsonDocument { { "TenantId", tenant }, { "IntakeDraftId", deletedId } },
            new BsonDocument("$set", new BsonDocument { { "IsDeleted", true }, { "DeletedAt", DateTime.UtcNow } }));
        Assert.Equal(1, marked.ModifiedCount);

        Assert.Null(await store.FindByIdAsync(scope, deletedId));
        var list = await store.ListAsync(new PvgPersistenceListScope(scope, 1, 100, null));
        Assert.Equal(new[] { liveId }, list.Select(item => item.IntakeDraftId));
        Assert.Equal(1, await store.CountAsync(scope, null));
        Assert.Equal(1, await store.CountAsync(scope, PvgIntakeStatus.IntakeCreated));

        var replace = await store.ReplaceAsync(
            scope, deletedId, 1, ActorB, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.Triaged));
        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, replace.Outcome);

        var raw = await mongo.RawDraftAsync(tenant, deletedId);
        Assert.Equal(1, raw!["Version"].AsInt64);
        Assert.Equal("IntakeCreated", raw["Status"].AsString);
        Assert.Equal(ActorA, raw["UpdatedBy"].AsString);
    }

    [Fact]
    public async Task Mongo_store_writes_IsDeleted_false_and_no_DeletedAt_on_create()
    {
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var before = DateTime.UtcNow.AddSeconds(-5);

        var intakeDraftId = await mongo.Store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));

        var raw = (await mongo.RawDraftAsync(tenant, intakeDraftId))!;
        Assert.True(raw.Contains("IsDeleted"), "the raw document carries no IsDeleted field");
        Assert.True(raw["IsDeleted"].IsBoolean);
        Assert.False(raw["IsDeleted"].AsBoolean);
        Assert.True(!raw.Contains("DeletedAt") || raw["DeletedAt"].IsBsonNull);

        // The identity: _id is the GUID inside the draft id, "D" format; the API id stays pvg-draft-{guid:N}.
        var guid = Guid.ParseExact(intakeDraftId["pvg-draft-".Length..], "N");
        Assert.Equal(guid.ToString("D"), raw["_id"].AsString);
        Assert.Equal(1, raw["Version"].AsInt64);
        Assert.Equal(ActorA, raw["CreatedBy"].AsString);
        Assert.Equal(ActorA, raw["UpdatedBy"].AsString);
        Assert.True(raw["CreatedAt"].IsValidDateTime && raw["CreatedAt"].ToUniversalTime() > before);
        Assert.True(raw["UpdatedAt"].IsValidDateTime);
        Assert.Equal(tenant, raw["TenantId"].AsString);
    }

    // ---- AC-13 (b): schema ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Mongo_store_creates_exactly_the_catalog_indexes_each_starting_with_TenantId()
    {
        await PvgMongoTestStores.RunBootstrapAsync(mongo.Services);

        using var cursor = await RawDrafts().Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var created = indexes.Where(index => index["name"].AsString != "_id_").ToArray();

        var catalog = PvgIntakeDraftIndexCatalog.RequiredIndexes;
        Assert.Equal(
            catalog.Select(definition => definition.Name).Order(StringComparer.Ordinal),
            created.Select(index => index["name"].AsString).Order(StringComparer.Ordinal));

        foreach (var definition in catalog)
        {
            var index = created.Single(candidate => candidate["name"].AsString == definition.Name);
            var key = index["key"].AsBsonDocument;

            Assert.Equal("TenantId", key.Names.First());
            Assert.Equal(definition.Fields, key.Names.ToArray());
            foreach (var field in definition.Fields)
            {
                var expectedDirection = definition.DescendingFields.Contains(field) ? -1 : 1;
                Assert.Equal(expectedDirection, key[field].ToInt32());
            }

            var unique = index.Contains("unique") && index["unique"].ToBoolean();
            Assert.Equal(definition.IsUnique, unique);
        }
    }

    [Fact]
    public async Task Pvg_database_holds_only_the_named_collections()
    {
        await PvgMongoTestStores.RunBootstrapAsync(mongo.Services);

        // DL-5 adds its outbox to this list.
        string[] named = [PvgIntakeDraftIndexCatalog.CollectionName];

        using var cursor = await mongo.RawDatabase.ListCollectionNamesAsync();
        var names = await cursor.ToListAsync();

        Assert.Equal(named.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
    }

    private IMongoCollection<BsonDocument> RawDrafts() =>
        mongo.RawDatabase.GetCollection<BsonDocument>(PvgIntakeDraftIndexCatalog.CollectionName);
}
