using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>
/// MOD-0173-FU02 K-01 (AC-01), K-08 defaults (AC-08), K-09 shape (AC-09), field shapes (§12) — through the real
/// pipeline: every (type, parent type) pair, the parentless cases, defaults per type and the 400 shape refusals.
/// </summary>
public sealed class LocationHierarchyTests : IClassFixture<InventoryApiFactory>
{
    private static readonly string[] Types = ["Site", "Warehouse", "StorageLocation", "Zone", "Bin"];

    private static readonly HashSet<(string Type, string Parent)> Allowed =
    [
        ("Warehouse", "Site"), ("StorageLocation", "Warehouse"), ("Zone", "StorageLocation"), ("Bin", "Zone"), ("Bin", "StorageLocation")
    ];

    private readonly InventoryApiFactory _factory;

    public LocationHierarchyTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC01_every_type_parent_pair_follows_the_K01_matrix()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();
        var parents = new Dictionary<string, string>
        {
            ["Site"] = tree.Site, ["Warehouse"] = tree.Warehouse, ["StorageLocation"] = tree.Storage, ["Zone"] = tree.Zone, ["Bin"] = tree.Bin
        };

        int created = 0, refused = 0;
        foreach (var type in Types)
        {
            foreach (var (parentType, parentId) in parents)
            {
                var body = new Dictionary<string, object?> { ["code"] = LocationApi.Code(type), ["name"] = "n", ["type"] = type, ["parentId"] = parentId };
                if (Allowed.Contains((type, parentType)))
                {
                    await api.Expect(api.CreateAsync(body), 201);
                    created++;
                }
                else
                {
                    await api.ExpectError(api.CreateAsync(body), 422, "LOCATION_LEVEL_INVALID");
                    refused++;
                }
            }
        }

        Assert.Equal(5, created);
        Assert.Equal(20, refused);
    }

    [Fact]
    public async Task AC01_only_a_Site_stands_without_a_parent()
    {
        var api = new LocationApi(new InventoryScenario(_factory));

        await api.Expect(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" }), 201);
        foreach (var type in Types.Skip(1))
        {
            await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code(type), ["name"] = "n", ["type"] = type }), 422, "LOCATION_LEVEL_INVALID");
        }
    }

    [Fact]
    public async Task AC08_stockable_defaults_by_type_and_only_bin_or_storage_location_may_hold_stock()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();

        Assert.False((await api.Get(tree.Site)).GetProperty("isStockable").GetBoolean());
        Assert.False((await api.Get(tree.Warehouse)).GetProperty("isStockable").GetBoolean());
        Assert.False((await api.Get(tree.Storage)).GetProperty("isStockable").GetBoolean());
        Assert.False((await api.Get(tree.Zone)).GetProperty("isStockable").GetBoolean());
        Assert.True((await api.Get(tree.Bin)).GetProperty("isStockable").GetBoolean());

        await api.Node("StorageLocation", tree.Warehouse, extra: new() { ["isStockable"] = true });
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("Z"), ["name"] = "n", ["type"] = "Zone", ["parentId"] = tree.Storage, ["isStockable"] = true }),
            400, "INVALID_REQUEST");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("W"), ["name"] = "n", ["type"] = "Warehouse", ["parentId"] = tree.Site, ["isStockable"] = true }),
            400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task AC09_operating_mode_exists_on_site_and_warehouse_only()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();

        Assert.Equal("internal", (await api.Get(tree.Site)).GetProperty("operatingMode").GetString());
        Assert.False((await api.Get(tree.Bin)).TryGetProperty("operatingMode", out _));

        var external = await api.Node("Warehouse", tree.Site, extra: new() { ["operatingMode"] = "external-integrated" });
        Assert.Equal("external-integrated", (await api.Get(external.Id)).GetProperty("operatingMode").GetString());

        foreach (var (type, parent) in new[] { ("StorageLocation", tree.Warehouse), ("Zone", tree.Storage), ("Bin", tree.Zone) })
        {
            await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code(type), ["name"] = "n", ["type"] = type, ["parentId"] = parent, ["operatingMode"] = "internal" }),
                400, "INVALID_REQUEST");
        }

        await api.ExpectError(api.PatchAsync(tree.Bin, new { operatingMode = "internal" }, await api.ETagOf(tree.Bin)), 400, "INVALID_REQUEST");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("W"), ["name"] = "n", ["type"] = "Warehouse", ["parentId"] = tree.Site, ["operatingMode"] = "outsourced" }),
            400, "INVALID_REQUEST");
    }

    [Theory]
    [InlineData("code", "")]
    [InlineData("code", "has space")]
    [InlineData("code", "ÇODE")]
    [InlineData("code", "12345678901234567890123456789012345678901")]
    [InlineData("name", "")]
    [InlineData("type", "Shelf")]
    [InlineData("type", "site")]
    [InlineData("costCenterCode", "CC/1")]
    [InlineData("orgUnitRef", "not-a-uuid")]
    [InlineData("parentId", "not-a-uuid")]
    public async Task Malformed_fields_are_400_INVALID_REQUEST(string field, string value)
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var body = new Dictionary<string, object?> { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" };
        body[field] = value;

        await api.ExpectError(api.CreateAsync(body), 400, "INVALID_REQUEST");

        // Nothing written: only the 8 seeded scenario nodes exist, no history row.
        Assert.Equal(8, api.Scenario.Persisted("inventory_locations").Count);
        Assert.Empty(api.Scenario.Persisted("inventory_location_history"));
    }

    [Fact]
    public async Task Wrong_json_types_unknown_members_and_scope_members_are_400()
    {
        var api = new LocationApi(new InventoryScenario(_factory));

        await api.ExpectError(api.CreateAsync(new() { ["code"] = 12, ["name"] = "n", ["type"] = "Site" }), 400, "INVALID_REQUEST");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["isStockable"] = "yes" }), 400, "INVALID_REQUEST");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["colour"] = "red" }), 400, "INVALID_REQUEST");
        var message = await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["legalEntityId"] = Guid.NewGuid().ToString() }),
            400, "INVALID_REQUEST");
        Assert.Contains("scope is taken from the access token", message);

        var tree = await api.TreeAsync();
        var typeChange = await api.ExpectError(api.PatchAsync(tree.Bin, new { type = "Zone" }, await api.ETagOf(tree.Bin)), 400, "INVALID_REQUEST");
        Assert.Contains("type cannot be changed", typeChange);
    }
}
