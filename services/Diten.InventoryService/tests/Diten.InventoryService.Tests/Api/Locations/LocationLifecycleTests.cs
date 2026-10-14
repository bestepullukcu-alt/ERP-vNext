using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>
/// MOD-0173-FU02 lifecycle against REAL ledger postings: K-05 (AC-05), K-06 (AC-06), K-07 (AC-07), K-08 stock part
/// (AC-08), K-09 stock part (AC-09), K-10 stock presence (AC-10, API part). Every refusal leaves the node, its version
/// and its history untouched.
/// </summary>
public sealed class LocationLifecycleTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public LocationLifecycleTests(InventoryApiFactory factory) => _factory = factory;

    // ── K-05 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC05_deactivate_or_archive_with_stock_on_the_node_or_below_is_422_LOCATION_HAS_STOCK_and_changes_nothing()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("5"), 201);

        foreach (var id in new[] { s.LocationA, s.StorageNode.Id.ToString("D"), s.Warehouse, s.SiteNode.Id.ToString("D") })
        {
            foreach (var target in new[] { "Inactive", "Archived" })
            {
                await api.ExpectError(api.StatusAsync(id, target, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
            }

            var node = await api.Get(id);
            Assert.Equal("Active", node.GetProperty("status").GetString());
            Assert.Equal("W/\"1\"", await api.ETagOf(id));
        }

        Assert.Empty(s.Persisted("inventory_location_history"));

        // Any stock status counts (LOC-09): QUARANTINE stock in bin B pins it too.
        await s.PostExpectingAsync(s.Receipt("1", "GOODS_RECEIPT_PO", "QUARANTINE", location: s.LocationB), 201);
        await api.ExpectError(api.StatusAsync(s.LocationB, "Inactive", "W/\"1\""), 422, "LOCATION_HAS_STOCK");
    }

    [Fact]
    public async Task AC05_deactivate_with_an_active_child_is_422_LOCATION_HAS_ACTIVE_CHILDREN_and_an_emptied_bin_may_go()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var tree = await api.TreeAsync();

        await api.ExpectError(api.StatusAsync(tree.Zone, "Inactive", await api.ETagOf(tree.Zone)), 422, "LOCATION_HAS_ACTIVE_CHILDREN");
        await api.ExpectError(api.StatusAsync(tree.Zone, "Archived", await api.ETagOf(tree.Zone)), 422, "LOCATION_HAS_ACTIVE_CHILDREN");

        // Stock received and fully issued: OnHand 0 rows do not count.
        await s.PostExpectingAsync(s.Receipt("3"), 201);
        await s.PostExpectingAsync(s.Issue("3"), 201);
        Assert.Equal((false, false), await api.Presence(s.LocationA));
        await api.Expect(api.StatusAsync(s.LocationA, "Inactive", "W/\"1\""), 200);

        // Bottom-up deactivation of the API tree works.
        await api.Expect(api.StatusAsync(tree.Bin, "Inactive", await api.ETagOf(tree.Bin)), 200);
        await api.Expect(api.StatusAsync(tree.Zone, "Inactive", await api.ETagOf(tree.Zone)), 200);
    }

    // ── K-06 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC06_activating_under_an_inactive_parent_is_422_LOCATION_PARENT_INACTIVE()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();
        await api.Expect(api.StatusAsync(tree.Bin, "Inactive", await api.ETagOf(tree.Bin)), 200);
        await api.Expect(api.StatusAsync(tree.Zone, "Inactive", await api.ETagOf(tree.Zone)), 200);

        await api.ExpectError(api.StatusAsync(tree.Bin, "Active", await api.ETagOf(tree.Bin)), 422, "LOCATION_PARENT_INACTIVE");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("B"), ["name"] = "n", ["type"] = "Bin", ["parentId"] = tree.Zone }),
            422, "LOCATION_PARENT_INACTIVE");
        await api.ExpectError(api.BulkAsync(tree.Zone, LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 1, 1)), 422, "LOCATION_PARENT_INACTIVE");
        Assert.Equal("Inactive", (await api.Get(tree.Bin)).GetProperty("status").GetString());

        // Parent first, then the child (control).
        await api.Expect(api.StatusAsync(tree.Zone, "Active", await api.ETagOf(tree.Zone)), 200);
        await api.Expect(api.StatusAsync(tree.Bin, "Active", await api.ETagOf(tree.Bin)), 200);
    }

    // ── K-07 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC07_a_move_with_stock_on_the_node_or_below_is_422_LOCATION_HAS_STOCK()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("2"), 201);

        await api.ExpectError(api.PatchAsync(s.LocationA, new { parentId = s.Storage2Node.Id.ToString("D") }, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
        await api.ExpectError(api.PatchAsync(s.StorageNode.Id.ToString("D"), new { parentId = s.Warehouse2 }, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
        Assert.Equal(s.StorageNode.Id.ToString("D"), (await api.Get(s.LocationA)).GetProperty("parentId").GetString());
    }

    [Fact]
    public async Task AC07_a_move_into_the_own_subtree_or_against_K01_is_422()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();

        await api.ExpectError(api.PatchAsync(tree.Zone, new { parentId = tree.Bin }, await api.ETagOf(tree.Zone)), 422, "LOCATION_MOVE_INTO_SUBTREE");
        await api.ExpectError(api.PatchAsync(tree.Zone, new { parentId = tree.Zone }, await api.ETagOf(tree.Zone)), 422, "LOCATION_MOVE_INTO_SUBTREE");
        await api.ExpectError(api.PatchAsync(tree.Warehouse, new { parentId = tree.Storage }, await api.ETagOf(tree.Warehouse)), 422, "LOCATION_MOVE_INTO_SUBTREE");
        await api.ExpectError(api.PatchAsync(tree.Bin, new { parentId = tree.Warehouse }, await api.ETagOf(tree.Bin)), 422, "LOCATION_LEVEL_INVALID");
        await api.ExpectError(api.PatchAsync(tree.Site, new { parentId = tree.Site }, await api.ETagOf(tree.Site)), 422, "LOCATION_LEVEL_INVALID");
        await api.ExpectError(api.PatchAsync(tree.Bin, new { parentId = (string?)null }, await api.ETagOf(tree.Bin)), 422, "LOCATION_LEVEL_INVALID");
    }

    [Fact]
    public async Task AC07_a_valid_move_rewrites_the_subtree_paths_and_the_ledger_follows_the_new_warehouse()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var tree = await api.TreeAsync();
        var otherWarehouse = await api.Node("Warehouse", tree.Site);

        var moved = await api.Expect(api.PatchAsync(tree.Storage, new { parentId = otherWarehouse.Id }, await api.ETagOf(tree.Storage)), 200);
        Assert.Equal(otherWarehouse.Id, moved.GetProperty("parentId").GetString());

        var path = await api.Expect(api.GetAsync($"/api/locations/{tree.Bin}/path"), 200);
        Assert.Equal([tree.Site, otherWarehouse.Id, tree.Storage, tree.Zone, tree.Bin],
            path.GetProperty("path").EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList());

        var bin = s.Persisted("inventory_locations").Single(d => d["_id"].AsString == tree.Bin);
        Assert.Equal([tree.Site, otherWarehouse.Id, tree.Storage, tree.Zone], bin["AncestorIds"].AsBsonArray.Select(v => v.AsString).ToList());

        var receipt = s.Receipt("1", location: tree.Bin);
        receipt["toWarehouseId"] = tree.Warehouse;
        var refused = await s.PostExpectingAsync(receipt, 422);
        Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());
        receipt = s.Receipt("1", location: tree.Bin);
        receipt["toWarehouseId"] = otherWarehouse.Id;
        await s.PostExpectingAsync(receipt, 201);

        var moves = (await api.History(tree.Storage)).Where(h => h.GetProperty("action").GetString() == "Moved").ToList();
        Assert.Equal(tree.Warehouse, Assert.Single(moves).GetProperty("oldValue").GetString());
    }

    // ── K-08 / K-09 against stock ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC08_stockable_off_with_stock_is_422_and_a_stockable_storage_location_receives_postings()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("1"), 201);

        await api.ExpectError(api.PatchAsync(s.LocationA, new { isStockable = false }, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
        Assert.True((await api.Get(s.LocationA)).GetProperty("isStockable").GetBoolean());
        await api.Expect(api.PatchAsync(s.LocationB, new { isStockable = false }, "W/\"1\""), 200);
        var refused = await s.PostExpectingAsync(s.Receipt("1", location: s.LocationB), 422);
        Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());

        var storage = await api.Node("StorageLocation", s.Warehouse, extra: new() { ["isStockable"] = true });
        await s.PostExpectingAsync(s.Receipt("4", location: storage.Id), 201);
        Assert.Equal(4m, await s.OnHandAsync(storage.Id));
        Assert.Equal((true, false), await api.Presence(storage.Id));
    }

    [Fact]
    public async Task AC09_operating_mode_is_locked_while_stock_is_held_below()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("1"), 201);

        await api.ExpectError(api.PatchAsync(s.Warehouse, new { operatingMode = "external-integrated" }, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
        await api.ExpectError(api.PatchAsync(s.SiteNode.Id.ToString("D"), new { operatingMode = "external-integrated" }, "W/\"1\""), 422, "LOCATION_HAS_STOCK");
        var changed = await api.Expect(api.PatchAsync(s.Warehouse2, new { operatingMode = "external-integrated" }, "W/\"1\""), 200);
        Assert.Equal("external-integrated", changed.GetProperty("operatingMode").GetString());
    }

    // ── K-10 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC10_stock_presence_comes_from_ledger_rows_for_self_subtree_and_warehouse_and_no_response_carries_a_quantity()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        Assert.Equal((false, false), await api.Presence(s.Warehouse));

        await s.PostExpectingAsync(s.Receipt("7"), 201);

        Assert.Equal((true, false), await api.Presence(s.LocationA));
        Assert.Equal((false, false), await api.Presence(s.LocationB));
        Assert.Equal((false, true), await api.Presence(s.StorageNode.Id.ToString("D")));
        Assert.Equal((false, true), await api.Presence(s.Warehouse));
        Assert.Equal((false, true), await api.Presence(s.SiteNode.Id.ToString("D")));
        Assert.Equal((false, false), await api.Presence(s.Warehouse2));

        await s.PostExpectingAsync(s.Issue("7"), 201);
        Assert.Equal((false, false), await api.Presence(s.LocationA));
        Assert.Equal((false, false), await api.Presence(s.Warehouse));

        // No location response carries a quantity (K-10): scan every member name.
        var bodies = new List<JsonElement>
        {
            await api.Get(s.LocationA),
            await api.Expect(api.GetAsync("/api/locations"), 200),
            await api.Expect(api.GetAsync("/api/locations/search"), 200),
            await api.Expect(api.GetAsync($"/api/locations/{s.LocationA}/path"), 200),
            await api.Expect(api.GetAsync($"/api/locations/{s.LocationA}/stock-presence"), 200),
            await api.Expect(api.ValidateAsync(s.LocationA), 200)
        };
        var names = bodies.SelectMany(Names).Distinct().ToList();
        Assert.DoesNotContain(names, n => n.Contains("quantity", StringComparison.OrdinalIgnoreCase)
                                          || n.Contains("onHand", StringComparison.OrdinalIgnoreCase)
                                          || n.Contains("available", StringComparison.OrdinalIgnoreCase)
                                          || n.Contains("reserved", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Names(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Names(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Names),
        _ => []
    };
}
