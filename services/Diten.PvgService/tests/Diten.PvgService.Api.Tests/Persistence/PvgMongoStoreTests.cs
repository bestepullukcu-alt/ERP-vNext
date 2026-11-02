using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
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

        // The same two refusals on a replace (WP-0230-DL10-R1, H6): an existing draft, an intake of another tenant. The
        // refusal comes before any write, so the draft stays where it was, in its own tenant, at its own version.
        var ownId = await store.AddAsync(scope, ActorA, intake);
        var replaceMismatch = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.ReplaceAsync(scope, ownId, 1, ActorB, PvgMongoTestData.Intake(PvgMongoTestData.NewTenant())));
        Assert.Equal("PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH", replaceMismatch.Message);
        Assert.DoesNotContain(tenant, replaceMismatch.Message, StringComparison.OrdinalIgnoreCase);

        var replaceMissingScope = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.ReplaceAsync(new PvgPersistenceTenantScope(" "), ownId, 1, ActorB, intake));
        Assert.Equal("PVG_PERSISTENCE_TENANT_SCOPE_REQUIRED", replaceMissingScope.Message);

        var stayed = await store.FindByIdAsync(scope, ownId);
        Assert.NotNull(stayed);
        Assert.Equal(1, stayed.Version);
        Assert.Equal(tenant, stayed.Intake.TenantId);
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

        AssertEveryPropertyEqual(properties, written, read);

        // The instants come back exactly, sub-millisecond ticks included.
        Assert.Equal(written.ReceivedAtUtc.UtcTicks, read.ReceivedAtUtc.UtcTicks);
        Assert.Equal(written.AwarenessAtUtc.UtcTicks, read.AwarenessAtUtc.UtcTicks);

        // The replace turn (WP-0230-DL10-R1, N1): the replacement changes every property but the tenant, so a field that
        // the replace body leaves out keeps its old value and is found. The difference is checked by reflection too.
        var changed = PvgMongoTestData.FullIntakeChanged(tenant);
        foreach (var property in properties)
        {
            AssertFilled(property.Name, property.GetValue(changed), property.PropertyType);
            if (property.Name == nameof(SafetyCaseIntake.TenantId))
            {
                continue;
            }

            Assert.False(
                SameValue(property.GetValue(written), property.GetValue(changed)),
                $"{property.Name} is the same in both fixtures, so a replace that skips it would go unnoticed");
        }

        var replaced = await mongo.Store.ReplaceAsync(scope, intakeDraftId, 1, ActorB, changed);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced, replaced.Outcome);
        var readBack = (await mongo.Store.FindByIdAsync(scope, intakeDraftId))!;
        Assert.Equal(2, readBack.Version);
        AssertEveryPropertyEqual(properties, changed, readBack.Intake);
        Assert.Equal(changed.ReceivedAtUtc.UtcTicks, readBack.Intake.ReceivedAtUtc.UtcTicks);
        Assert.Equal(changed.AwarenessAtUtc.UtcTicks, readBack.Intake.AwarenessAtUtc.UtcTicks);
    }

    private static void AssertEveryPropertyEqual(PropertyInfo[] properties, SafetyCaseIntake expected, SafetyCaseIntake actual)
    {
        foreach (var property in properties)
        {
            Assert.True(
                SameValue(property.GetValue(expected), property.GetValue(actual)),
                $"{property.Name}: expected '{property.GetValue(expected)}', read '{property.GetValue(actual)}'");
        }
    }

    private static bool SameValue(object? left, object? right) =>
        left is IEnumerable leftItems and not string && right is IEnumerable rightItems and not string
            ? leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>())
            : Equals(left, right);

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

    // ---- WP-0230-DL10-R1: gaps the independent verification left unmeasured ------------------------------------

    // J11, J12: a replace moves UpdatedAt forward and leaves CreatedAt where it was.
    [Fact]
    public async Task Mongo_replace_advances_UpdatedAt_and_keeps_CreatedAt()
    {
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var intakeDraftId = await mongo.Store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));
        var created = (await mongo.RawDraftAsync(tenant, intakeDraftId))!;
        var createdAt = created["CreatedAt"].ToUniversalTime();
        Assert.Equal(createdAt, created["UpdatedAt"].ToUniversalTime());

        // Mongo dates have millisecond resolution: the pause makes "later" a measurable difference.
        await Task.Delay(100);
        var replaced = await mongo.Store.ReplaceAsync(
            scope, intakeDraftId, 1, ActorB, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced, replaced.Outcome);

        var after = (await mongo.RawDraftAsync(tenant, intakeDraftId))!;
        Assert.Equal(createdAt, after["CreatedAt"].ToUniversalTime());
        Assert.Equal(ActorA, after["CreatedBy"].AsString);
        Assert.True(
            after["UpdatedAt"].ToUniversalTime() > createdAt,
            $"UpdatedAt {after["UpdatedAt"].ToUniversalTime():O} did not move past CreatedAt {createdAt:O}");
        Assert.True(after["UpdatedAt"].ToUniversalTime() >= createdAt.AddMilliseconds(50));
    }

    // G7: the draft id decides between equal received times, with and without a status filter. Draft ids are random, so
    // the fixture carries enough drafts that the order in which they were written cannot match the id order by chance.
    [Theory]
    [InlineData(PvgStoreKind.InMemory)]
    [InlineData(PvgStoreKind.Mongo)]
    public async Task Mongo_list_breaks_equal_received_times_by_draft_id_with_and_without_a_status_filter(PvgStoreKind kind)
    {
        var store = StoreOf(kind);
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);
        var received = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        var updated = new List<string>();
        var everything = new List<string>();
        for (var index = 0; index < 16; index++)
        {
            var status = index % 4 == 3 ? PvgIntakeStatus.IntakeCreated : PvgIntakeStatus.IntakeUpdated;
            var id = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant, status, received));
            everything.Add(id);
            if (status == PvgIntakeStatus.IntakeUpdated)
            {
                updated.Add(id);
            }
        }

        var filtered = await store.ListAsync(new PvgPersistenceListScope(scope, 1, 100, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(12, filtered.Count);
        Assert.Equal(updated.Order(StringComparer.Ordinal), filtered.Select(item => item.IntakeDraftId));

        var unfiltered = await store.ListAsync(new PvgPersistenceListScope(scope, 1, 100, null));
        Assert.Equal(16, unfiltered.Count);
        Assert.Equal(everything.Order(StringComparer.Ordinal), unfiltered.Select(item => item.IntakeDraftId));
    }

    // N3: the first command of every write transaction carries the snapshot read concern. The store's own client reports
    // the commands it sends, and the first command of a transaction is the one that carries startTransaction.
    [Fact]
    public async Task Mongo_write_transactions_start_with_the_snapshot_read_concern()
    {
        var firstCommands = new ConcurrentQueue<(string Command, string? Level)>();
        await using var services = PvgMongoTestStores.BuildServices(
            mongo.ConnectionString,
            settings => settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(started =>
            {
                if (started.Command.TryGetValue("startTransaction", out var start) && start.ToBoolean())
                {
                    string? level = null;
                    if (started.Command.TryGetValue("readConcern", out var concern) &&
                        concern is BsonDocument concernDocument &&
                        concernDocument.TryGetValue("level", out var levelValue))
                    {
                        level = levelValue.AsString;
                    }

                    firstCommands.Enqueue((started.CommandName, level));
                }
            }));
        var store = services.GetRequiredService<IPvgIntakeDraftStore>();
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);

        var intakeDraftId = await store.AddAsync(scope, ActorA, PvgMongoTestData.Intake(tenant));
        var replaced = await store.ReplaceAsync(
            scope, intakeDraftId, 1, ActorA, PvgMongoTestData.Intake(tenant, PvgIntakeStatus.IntakeUpdated));
        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced, replaced.Outcome);

        // Positive control: both writes started a transaction, and the commands are the ones the store sends first.
        var commands = firstCommands.ToArray();
        Assert.Equal(new[] { "insert", "update" }, commands.Select(command => command.Command).ToArray());
        Assert.All(commands, command => Assert.Equal("snapshot", command.Level));
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
