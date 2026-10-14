using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>MOD-0173-FU02 K-11 history (AC-11), K-12 bulk bins (AC-12), idempotency of create and bulk (AC-17).</summary>
public sealed class LocationHistoryBulkIdempotencyTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public LocationHistoryBulkIdempotencyTests(InventoryApiFactory factory) => _factory = factory;

    // ── K-11 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC11_create_and_every_changed_field_write_one_history_row_with_actor_correlation_and_le_source()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var correlation = Guid.NewGuid();
        void Correlated(HttpRequestMessage r) => r.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation.ToString());

        var site = await api.Node("Site", null);
        var created = Assert.Single(await api.History(site.Id));
        Assert.Equal("Created", created.GetProperty("action").GetString());
        Assert.False(created.TryGetProperty("field", out _));

        var newCode = LocationApi.Code("S");
        var patched = await api.Expect(api.PatchAsync(site.Id, new { code = newCode, name = "Renamed site", costCenterCode = "CC-01" }, site.ETag, Correlated), 200);
        Assert.Equal(2, patched.GetProperty("version").GetInt32());
        await api.Expect(api.StatusAsync(site.Id, "Inactive", "W/\"2\""), 200);

        var history = await api.History(site.Id);
        Assert.Equal(5, history.Count);
        Assert.Equal("StatusChanged", history[0].GetProperty("action").GetString()); // newest first
        Assert.Equal("Active", history[0].GetProperty("oldValue").GetString());
        Assert.Equal("Inactive", history[0].GetProperty("newValue").GetString());

        var updates = history.Where(h => h.GetProperty("action").GetString() == "Updated").ToList();
        Assert.Equal(["code", "costCenterCode", "name"], updates.Select(h => h.GetProperty("field").GetString()).OrderBy(f => f).ToList());
        Assert.All(updates, h =>
        {
            Assert.Equal(correlation.ToString(), h.GetProperty("correlationId").GetString());
            Assert.Equal(s.User.ToString(), h.GetProperty("actorUserId").GetString());
        });
        Assert.False(updates.Single(h => h.GetProperty("field").GetString() == "costCenterCode").TryGetProperty("oldValue", out _));

        // Persisted rows carry the W-0173-02 LE source (token path here).
        Assert.All(s.Persisted("inventory_location_history"), row => Assert.Equal("token", row["LeSource"].AsString));
    }

    [Fact]
    public async Task AC11_an_unchanged_patch_writes_no_history_and_keeps_the_version_and_a_refused_change_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var site = await api.Node("Site", null);
        var name = (await api.Get(site.Id)).GetProperty("name").GetString();

        var same = await api.Expect(api.PatchAsync(site.Id, new { name }, site.ETag), 200);
        Assert.Equal(1, same.GetProperty("version").GetInt32());

        var other = await api.Node("Site", null);
        var otherCode = (await api.Get(other.Id)).GetProperty("code").GetString();
        await api.ExpectError(api.PatchAsync(site.Id, new { name = "x", code = otherCode }, site.ETag), 409, "LOCATION_CODE_TAKEN");

        Assert.Single(await api.History(site.Id));
        Assert.Equal("W/\"1\"", await api.ETagOf(site.Id));
        Assert.NotEqual("x", (await api.Get(site.Id)).GetProperty("name").GetString());
    }

    // ── K-12 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC12_a_dry_run_previews_without_writing_and_the_real_run_creates_everything_in_one_go()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var tree = await api.TreeAsync();
        var before = s.Persisted("inventory_locations").Count;
        var historyBefore = s.Persisted("inventory_location_history").Count;

        var preview = await api.Expect(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "B", 1, 2, 1, 1, 3, dryRun: true)), 200);
        Assert.Equal(12, preview.GetProperty("count").GetInt32());
        Assert.Equal("A-01-01-01", preview.GetProperty("toCreate")[0].GetString());
        Assert.Equal("B-02-01-03", preview.GetProperty("toCreate")[11].GetString());
        Assert.Equal(before, s.Persisted("inventory_locations").Count);
        Assert.Equal(historyBefore, s.Persisted("inventory_location_history").Count);

        var result = await api.Expect(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "B", 1, 2, 1, 1, 3)), 201);
        Assert.Equal(12, result.GetProperty("count").GetInt32());
        var created = result.GetProperty("created").EnumerateArray().ToList();
        Assert.All(created, b =>
        {
            Assert.Equal("Bin", b.GetProperty("type").GetString());
            Assert.Equal(tree.Zone, b.GetProperty("parentId").GetString());
            Assert.True(b.GetProperty("isStockable").GetBoolean());
        });
        Assert.Equal(before + 12, s.Persisted("inventory_locations").Count);
        var rows = s.Persisted("inventory_location_history").Where(h => h["Action"].AsString == "BulkCreated").ToList();
        Assert.Equal(12, rows.Count);
        Assert.Equal(12, rows.Select(r => r["LocationId"].AsString).Distinct().Count());

        // Digit aisles keep the typed width; a StorageLocation is a valid target too.
        var digits = await api.Expect(api.BulkAsync(tree.Storage, LocationCodeAndScopeTests.Range("08", "10", 1, 1, 1, 1, 1, dryRun: true)), 200);
        Assert.Equal(["08-01-01-01", "09-01-01-01", "10-01-01-01"], LocationCodeAndScopeTests.Codes(digits.GetProperty("toCreate")));
    }

    [Fact]
    public async Task AC12_limits_targets_and_shapes()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();

        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "K", 1, 10, 1, 10, 1, dryRun: true)), 400, "LOCATION_BULK_LIMIT");
        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "J", 1, 10, 1, 10, 2)), 400, "LOCATION_BULK_LIMIT");
        await api.Expect(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "J", 1, 10, 1, 10, 1, dryRun: true)), 200); // exactly 1000

        await api.ExpectError(api.BulkAsync(tree.Bin, LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 1, 1)), 422, "LOCATION_LEVEL_INVALID");
        await api.ExpectError(api.BulkAsync(tree.Warehouse, LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 1, 1)), 422, "LOCATION_LEVEL_INVALID");
        await api.ExpectError(api.BulkAsync(Guid.NewGuid().ToString(), LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 1, 1)), 404, "UNKNOWN_LOCATION");

        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("B", "A", 1, 1, 1, 1, 1)), 400, "INVALID_REQUEST");
        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "1", 1, 1, 1, 1, 1)), 400, "INVALID_REQUEST");
        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "A", 0, 1, 1, 1, 1)), 400, "INVALID_REQUEST");
        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 100, 1)), 400, "INVALID_REQUEST");
        await api.ExpectError(api.BulkAsync(tree.Zone, new() { ["aisleFrom"] = "A", ["aisleTo"] = "A" }), 400, "INVALID_REQUEST");
    }

    // ── AC-17 ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC17_create_replays_with_the_same_key_and_refuses_the_key_for_another_body()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var key = "create-" + Guid.NewGuid().ToString("N");
        var code = LocationApi.Code("S");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["code"] = code, ["name"] = "Site", ["type"] = "Site" };

        var first = await api.Expect(api.CreateAsync(new(body)), 201);
        var replay = await api.Expect(api.CreateAsync(new(body)), 200);
        Assert.Equal(first.GetProperty("locationId").GetString(), replay.GetProperty("locationId").GetString());
        Assert.Single(s.Persisted("inventory_locations"), d => d["Code"].AsString == code);
        Assert.Single(await api.History(first.GetProperty("locationId").GetString()!));

        var other = new Dictionary<string, object?>(body) { ["name"] = "Another name" };
        await api.ExpectError(api.CreateAsync(other), 400, "IDEMPOTENCY_KEY_REUSED");

        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" },
            r => { r.Headers.Remove("Idempotency-Key"); }), 400, "INVALID_REQUEST");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" },
            r => { r.Headers.Remove("Idempotency-Key"); r.Headers.TryAddWithoutValidation("Idempotency-Key", "different"); }), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task AC17_concurrent_creates_with_one_key_make_one_location()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var key = "race-" + Guid.NewGuid().ToString("N");
        var code = LocationApi.Code("S");

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            api.CreateAsync(new() { ["idempotencyKey"] = key, ["code"] = code, ["name"] = "Site", ["type"] = "Site" })));
        var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(x => x).ToList();
        var ids = new HashSet<string?>();
        foreach (var response in responses)
        {
            ids.Add((await LocationApi.Read(response)).GetProperty("locationId").GetString());
            response.Dispose();
        }

        Assert.Equal(1, statuses.Count(x => x == 201));
        Assert.Equal(5, statuses.Count(x => x == 200));
        Assert.Single(ids);
        Assert.Single(s.Persisted("inventory_locations"), d => d["Code"].AsString == code);
    }

    [Fact]
    public async Task AC17_bulk_replays_without_duplicates_and_refuses_the_key_for_another_range()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var tree = await api.TreeAsync();
        var key = "bulk-" + Guid.NewGuid().ToString("N");
        Dictionary<string, object?> Body(int positions)
            => new(LocationCodeAndScopeTests.Range("C", "C", 1, 1, 1, 1, positions)) { ["idempotencyKey"] = key };

        var first = await api.Expect(api.BulkAsync(tree.Zone, Body(3)), 201);
        var replay = await api.Expect(api.BulkAsync(tree.Zone, Body(3)), 200);
        Assert.Equal(
            first.GetProperty("created").EnumerateArray().Select(b => b.GetProperty("locationId").GetString()).ToList(),
            replay.GetProperty("created").EnumerateArray().Select(b => b.GetProperty("locationId").GetString()).ToList());
        Assert.Equal(3, s.Persisted("inventory_locations").Count(d => d["Code"].AsString.StartsWith("C-01-01-", StringComparison.Ordinal)));
        Assert.Equal(3, s.Persisted("inventory_location_history").Count(h => h["Action"].AsString == "BulkCreated"));

        await api.ExpectError(api.BulkAsync(tree.Zone, Body(4)), 400, "IDEMPOTENCY_KEY_REUSED");
        await api.ExpectError(api.BulkAsync(tree.Zone, Body(3), headerKey: "other"), 400, "INVALID_REQUEST");
    }
}
