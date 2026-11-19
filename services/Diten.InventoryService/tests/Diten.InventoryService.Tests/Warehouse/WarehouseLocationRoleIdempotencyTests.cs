using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Persistence.Warehouse;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — AC-4 for the four role commands (pack §8.1, §13.24; independent reviews 10 F-1, 11 F-2 / F-3): the key is looked up BEFORE
/// If-Match and every business rule; same key + same body → 200 replay of the record's current state (version unchanged, no new history row);
/// same key + other body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>, nothing written; the key is scoped to user + operation (+ target); the loser of
/// two identical concurrent requests re-reads the key and answers 200, whichever unique index refused it.
/// </summary>
public sealed class WarehouseLocationRoleIdempotencyTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseLocationRoleIdempotencyTests(WarehouseApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_repeated_create_is_a_200_replay_not_409_and_another_body_under_the_key_is_400()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        const string key = "create-key-0001";

        var first = await Expect(s.CreateAsync(bin.Id, ["Receiving"], key: key), 201);
        var replay = await Expect(s.CreateAsync(bin.Id, ["Receiving"], key: key), 200);
        Assert.Equal(Id(first), Id(replay));
        Assert.Equal(1, replay.GetProperty("version").GetInt32());

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(bin.Id, ["Staging"], key: key), 400));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving"], key: key), 400));
        Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
        Assert.Single(s.Events(Id(first)));
    }

    /// <summary>K3 sabotage (bs): drop CreatedBy from the create key's index and lookup → B replays A's record (or 400) → red.</summary>
    [Fact]
    public async Task Two_users_with_the_same_create_key_make_two_records()
    {
        var s = new WarehouseScenario(_factory);
        const string key = "shared-key-0001";
        var a = await Expect(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving"], key: key), 201);
        var b = await Expect(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving"], key: key, token: s.TokenFor(Guid.NewGuid(), Read, Manage)), 201);
        Assert.NotEqual(Id(a), Id(b));
        Assert.Equal(2, s.Stored(WarehouseCollections.LocationRoles).Count);
    }

    /// <summary>K3 sabotage (bl): no key lookup (or after If-Match) → the repeat with the first If-Match is 409 → red. (bm): no fingerprint compare → red.</summary>
    [Fact]
    public async Task A_repeated_update_with_the_first_If_Match_is_a_200_replay_and_another_body_is_400()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        const string key = "update-key-0001";

        var first = await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "ilk" }, ETag(role), key), 200);
        var replay = await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "ilk" }, ETag(role), key), 200);
        Assert.Equal(2, first.GetProperty("version").GetInt32());
        Assert.Equal(2, replay.GetProperty("version").GetInt32());
        Assert.Equal("ilk", replay.GetProperty("note").GetString());

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.UpdateAsync(Id(role), new() { ["note"] = "başka" }, "W/\"2\"", key), 400));
        Assert.Equal(2, s.Events(Id(role)).Count); // Created + one Updated
        Assert.Equal(2, Assert.Single(s.Stored(WarehouseCollections.LocationRoles))["Version"].AsInt32);
    }

    [Fact]
    public async Task The_same_update_key_on_another_role_is_another_request()
    {
        var s = new WarehouseScenario(_factory);
        var a = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var b = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        const string key = "update-key-0002";
        await Expect(s.UpdateAsync(Id(a), new() { ["note"] = "aynı" }, ETag(a), key), 200);
        var other = await Expect(s.UpdateAsync(Id(b), new() { ["note"] = "aynı" }, ETag(b), key), 200);
        Assert.Equal(2, other.GetProperty("version").GetInt32());
        Assert.Equal(2, s.Events(Id(b)).Count);
    }

    [Fact]
    public async Task A_repeated_delete_is_a_200_replay_of_the_deleted_record()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        const string key = "delete-key-0001";

        var first = await Expect(s.DeleteAsync(Id(role), ETag(role), key), 200);
        var replay = await Expect(s.DeleteAsync(Id(role), ETag(role), key), 200);
        Assert.Equal(2, first.GetProperty("version").GetInt32());
        Assert.Equal(2, replay.GetProperty("version").GetInt32());
        Assert.Equal(2, s.Events(Id(role)).Count);
    }

    [Fact]
    public async Task A_repeated_bulk_delete_answers_its_written_rows_deleted_and_the_others_conflict_or_not_found()
    {
        var s = new WarehouseScenario(_factory);
        var a = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var stale = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var unknown = Guid.NewGuid().ToString("D");
        const string key = "bulk-key-00001";
        (string, int)[] items = [(Id(a), 1), (Id(stale), 9), (unknown, 1)];

        await Expect(s.BulkDeleteAsync(items, key), 200);
        var replay = (await Expect(s.BulkDeleteAsync(items, key), 200)).GetProperty("items").EnumerateArray()
            .ToDictionary(r => r.GetProperty("roleId").GetString()!, r => r);
        Assert.Equal("Deleted", replay[Id(a)].GetProperty("result").GetString());
        Assert.Equal(2, replay[Id(a)].GetProperty("version").GetInt32());
        Assert.Equal("Conflict", replay[Id(stale)].GetProperty("result").GetString());
        Assert.Equal("NotFound", replay[unknown].GetProperty("result").GetString());
        Assert.Equal(2, s.Events(Id(a)).Count);

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.BulkDeleteAsync([(Id(stale), 1)], key), 400));
        Assert.False(s.Stored(WarehouseCollections.LocationRoles).Single(r => r["_id"].AsString == Id(stale))["IsDeleted"].AsBoolean);
    }

    /// <summary>
    /// Independent review 11 F-2 — K3 sabotage (br): drop the operation filter of the bulk lookup → the bulk delete finds the single delete's key
    /// event, its fingerprint differs → 400 and nothing is written → red.
    /// </summary>
    [Fact]
    public async Task A_single_delete_key_is_another_request_for_a_bulk_delete()
    {
        var s = new WarehouseScenario(_factory);
        var single = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var bulk = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        const string key = "same-key-00001";

        await Expect(s.DeleteAsync(Id(single), ETag(single), key), 200);
        var rows = (await Expect(s.BulkDeleteAsync([(Id(bulk), 1)], key), 200)).GetProperty("items");
        Assert.Equal("Deleted", rows[0].GetProperty("result").GetString());
        Assert.True(s.Stored(WarehouseCollections.LocationRoles).Single(r => r["_id"].AsString == Id(bulk))["IsDeleted"].AsBoolean);
    }

    /// <summary>
    /// Independent review 11 F-3 — two identical creates meet at a barrier before their transactions: one 201, the other 200 replay (never
    /// 409 <c>WMS_LOCATION_ROLE_EXISTS</c>). K3 sabotage (bn): no key re-read after a duplicate → red.
    /// </summary>
    [Fact]
    public async Task Two_identical_concurrent_creates_answer_201_and_200()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        var barrier = new AsyncBarrier(2);
        _factory.AroundTransaction[s.Tenant] = async run =>
        {
            await barrier.SignalAndWaitAsync();
            return await run();
        };
        try
        {
            const string key = "concurrent-create-1";
            var calls = await Task.WhenAll(s.CreateAsync(bin.Id, ["Receiving"], key: key), s.CreateAsync(bin.Id, ["Receiving"], key: key));
            var statuses = calls.Select(c => (int)c.StatusCode).OrderBy(x => x).ToArray();
            foreach (var c in calls) c.Dispose();
            Assert.Equal([200, 201], statuses);
            Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
        }
        finally
        {
            _factory.AroundTransaction.TryRemove(s.Tenant, out _);
        }
    }

    /// <summary>
    /// Independent review 11 F-3, the LocationId branch measured at the repository level: the request's twin (same user, key and body) has
    /// committed, and THIS request's insert is refused by the node index (<c>ux_wms_location_role_location</c>) → the key is re-read →
    /// 200 replay. K3 sabotage (bp): answer the node duplicate without re-reading the key → 409 <c>WMS_LOCATION_ROLE_EXISTS</c> → red.
    /// </summary>
    [Fact]
    public async Task A_node_index_duplicate_after_the_twin_committed_is_a_replay()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        _factory.AroundTransaction[s.Tenant] = async run =>
        {
            await run(); // the twin commits
            throw new WarehouseLocationRoleDuplicateException(WarehouseLocationRoleUniqueIndex.Location);
        };
        try
        {
            var replay = await Expect(s.CreateAsync(bin.Id, ["Receiving"], key: "twin-key-00001"), 200);
            Assert.Equal(bin.Id.ToString("D"), replay.GetProperty("locationId").GetString());
        }
        finally
        {
            _factory.AroundTransaction.TryRemove(s.Tenant, out _);
        }

        // another key on the same node is the real 409
        Assert.Equal("WMS_LOCATION_ROLE_EXISTS", await ErrorCode(s.CreateAsync(bin.Id, ["Receiving"], key: "other-key-0001"), 409));
    }

    [Fact]
    public async Task A_node_index_duplicate_under_another_key_is_409_exists()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        var barrier = new AsyncBarrier(2);
        _factory.AroundTransaction[s.Tenant] = async run =>
        {
            await barrier.SignalAndWaitAsync();
            return await run();
        };
        try
        {
            var calls = await Task.WhenAll(s.CreateAsync(bin.Id, ["Receiving"], key: "race-key-a-001"), s.CreateAsync(bin.Id, ["Receiving"], key: "race-key-b-001"));
            var statuses = calls.Select(c => (int)c.StatusCode).OrderBy(x => x).ToArray();
            var loser = calls.Single(c => (int)c.StatusCode == 409);
            Assert.Contains("WMS_LOCATION_ROLE_EXISTS", await loser.Content.ReadAsStringAsync());
            foreach (var c in calls) c.Dispose();
            Assert.Equal([201, 409], statuses);
        }
        finally
        {
            _factory.AroundTransaction.TryRemove(s.Tenant, out _);
        }
    }

    /// <summary>Two identical updates at a barrier: one writes, the other loses the compare-and-set, re-reads its key → 200 replay (K3 (bn)).</summary>
    [Fact]
    public async Task Two_identical_concurrent_updates_answer_200_twice_and_write_once()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var barrier = new AsyncBarrier(2);
        _factory.AroundTransaction[s.Tenant] = async run =>
        {
            await barrier.SignalAndWaitAsync();
            return await run();
        };
        try
        {
            const string key = "concurrent-update-1";
            var calls = await Task.WhenAll(
                s.UpdateAsync(Id(role), new() { ["note"] = "aynı anda" }, ETag(role), key),
                s.UpdateAsync(Id(role), new() { ["note"] = "aynı anda" }, ETag(role), key));
            var statuses = calls.Select(c => (int)c.StatusCode).ToArray();
            foreach (var c in calls) c.Dispose();
            Assert.Equal([200, 200], statuses);
            Assert.Equal(2, s.Events(Id(role)).Count);
            Assert.Equal(2, Assert.Single(s.Stored(WarehouseCollections.LocationRoles))["Version"].AsInt32);
        }
        finally
        {
            _factory.AroundTransaction.TryRemove(s.Tenant, out _);
        }
    }
}
