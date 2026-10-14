using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>
/// MOD-0173-FU02 K-02 (AC-02 code identity), K-03 (AC-03 company isolation), K-04 (AC-04 archived is terminal, no
/// DELETE) through the real pipeline, with two companies of one tenant and the W-0173-02 claim-less header path.
/// </summary>
public sealed class LocationCodeAndScopeTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public LocationCodeAndScopeTests(InventoryApiFactory factory) => _factory = factory;

    // ── K-02 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC02_code_is_unique_per_company_case_insensitively_on_create_edit_and_bulk()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();
        var code = "DUP-" + Guid.NewGuid().ToString("N")[..8];
        var first = await api.Node("Bin", tree.Zone, code);

        await api.ExpectError(api.CreateAsync(new() { ["code"] = code.ToLowerInvariant(), ["name"] = "n", ["type"] = "Bin", ["parentId"] = tree.Zone }),
            409, "LOCATION_CODE_TAKEN");

        var second = await api.Node("Bin", tree.Zone);
        await api.ExpectError(api.PatchAsync(second.Id, new { code = code.ToLowerInvariant() }, second.ETag), 409, "LOCATION_CODE_TAKEN");

        // Bulk: the existing "A-01-01-01" is skipped and listed, not duplicated.
        await api.Node("Bin", tree.Zone, "A-01-01-01");
        var bulk = await api.Expect(api.BulkAsync(tree.Zone, Range("A", "A", 1, 1, 1, 1, 2)), 201);
        Assert.Equal(["A-01-01-01"], bulk.GetProperty("skipped").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal(["A-01-01-02"], bulk.GetProperty("created").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList());

        // An archived code stays taken (ASSUMPTION-LOC-03).
        await api.Expect(api.StatusAsync(first.Id, "Archived", first.ETag), 200);
        await api.ExpectError(api.CreateAsync(new() { ["code"] = code, ["name"] = "n", ["type"] = "Bin", ["parentId"] = tree.Zone }), 409, "LOCATION_CODE_TAKEN");

        // The unique index that carries K-02 under a race is present and unique.
        var index = (await _factory.Database.GetCollection<BsonDocument>("inventory_locations").Indexes.ListAsync()).ToList()
            .Single(i => i["name"] == "ux_location_code");
        Assert.True(index["unique"].AsBoolean);
    }

    [Fact]
    public async Task AC02_the_same_code_in_another_company_is_fine()
    {
        var s = new InventoryScenario(_factory);
        var a = new LocationApi(s);
        var b = new LocationApi(s, TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.User, [.. TestTokens.AllPermissions, .. TestTokens.LocationPermissions]));
        var code = "SAME-" + Guid.NewGuid().ToString("N")[..8];

        await a.Node("Site", null, code);
        await b.Node("Site", null, code);
    }

    [Fact]
    public async Task AC02_a_code_edit_keeps_the_id_the_ledger_rows_still_resolve_and_history_records_old_to_new()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("10"), 201);
        var oldCode = (await api.Get(s.LocationA)).GetProperty("code").GetString();
        var newCode = "RENAMED-" + Guid.NewGuid().ToString("N")[..6];

        var updated = await api.Expect(api.PatchAsync(s.LocationA, new { code = newCode }, await api.ETagOf(s.LocationA)), 200);

        Assert.Equal(s.LocationA, updated.GetProperty("locationId").GetString());
        Assert.Equal(newCode, updated.GetProperty("code").GetString());
        Assert.Equal(10m, await s.OnHandAsync(s.LocationA));
        await s.PostExpectingAsync(s.Issue("4"), 201);
        Assert.Equal(6m, await s.OnHandAsync(s.LocationA));

        var change = Assert.Single(await api.History(s.LocationA));
        Assert.Equal("Updated", change.GetProperty("action").GetString());
        Assert.Equal("code", change.GetProperty("field").GetString());
        Assert.Equal(oldCode, change.GetProperty("oldValue").GetString());
        Assert.Equal(newCode, change.GetProperty("newValue").GetString());
    }

    // ── K-03 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC03_a_site_takes_the_scope_company_and_children_inherit_it()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var tree = await api.TreeAsync();

        foreach (var id in new[] { tree.Site, tree.Warehouse, tree.Storage, tree.Zone, tree.Bin })
        {
            Assert.Equal(s.LegalEntity.ToString(), (await api.Get(id)).GetProperty("legalEntityId").GetString());
        }

        var persisted = s.Persisted("inventory_locations").Where(d => d["_id"].AsString == tree.Bin).Single();
        Assert.Equal(s.LegalEntity.ToString(), persisted["LegalEntityId"].AsString);
    }

    [Fact]
    public async Task AC03_a_parent_of_another_company_is_403_in_a_write_and_unknown_everywhere_else()
    {
        var s = new InventoryScenario(_factory);
        var a = new LocationApi(s);
        var otherLe = Guid.NewGuid();
        var b = new LocationApi(s, TestTokens.Mint(s.Tenant, otherLe, s.User, [.. TestTokens.AllPermissions, .. TestTokens.LocationPermissions]));
        var treeA = await a.TreeAsync();

        // Explicit cross-company parent in a write → 403 (create and move).
        await b.ExpectError(b.CreateAsync(new() { ["code"] = LocationApi.Code("W"), ["name"] = "n", ["type"] = "Warehouse", ["parentId"] = treeA.Site }),
            403, "CROSS_LE_FORBIDDEN");
        var siteB = await b.Node("Site", null);
        var warehouseB = await b.Node("Warehouse", siteB.Id);
        await b.ExpectError(b.PatchAsync(warehouseB.Id, new { parentId = treeA.Site }, warehouseB.ETag), 403, "CROSS_LE_FORBIDDEN");

        // Company B sees none of company A's nodes, in any operation.
        foreach (var id in new[] { treeA.Site, treeA.Bin })
        {
            await b.ExpectError(b.GetAsync($"/api/locations/{id}"), 404, "UNKNOWN_LOCATION");
            await b.ExpectError(b.GetAsync($"/api/locations/{id}/path"), 404, "UNKNOWN_LOCATION");
            await b.ExpectError(b.GetAsync($"/api/locations/{id}/history"), 404, "UNKNOWN_LOCATION");
            await b.ExpectError(b.GetAsync($"/api/locations/{id}/stock-presence"), 404, "UNKNOWN_LOCATION");
            await b.ExpectError(b.PatchAsync(id, new { name = "x" }, "W/\"1\""), 404, "UNKNOWN_LOCATION");
            await b.ExpectError(b.StatusAsync(id, "Inactive", "W/\"1\""), 404, "UNKNOWN_LOCATION");
        }

        await b.ExpectError(b.BulkAsync(treeA.Zone, Range("A", "A", 1, 1, 1, 1, 1)), 404, "UNKNOWN_LOCATION");
        var validation = await b.Expect(b.ValidateAsync(treeA.Bin), 200);
        Assert.False(validation.GetProperty("results")[0].GetProperty("exists").GetBoolean());

        var listB = await b.Expect(b.GetAsync("/api/locations"), 200);
        Assert.DoesNotContain(listB.GetProperty("items").EnumerateArray(), i => i.GetProperty("legalEntityId").GetString() != otherLe.ToString());
        var searchB = await b.Expect(b.GetAsync("/api/locations/search?pageSize=200"), 200);
        Assert.Equal(2, searchB.GetProperty("total").GetInt64());

        // … and company A still sees its own (control).
        await a.Expect(a.GetAsync($"/api/locations/{treeA.Bin}"), 200);
    }

    [Fact]
    public async Task AC03_listLocations_legalEntityId_is_honoured_only_for_the_scope_company_and_refused_elsewhere()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);

        var own = await api.Expect(api.GetAsync($"/api/locations?legalEntityId={s.LegalEntity}"), 200);
        Assert.Equal(8, own.GetProperty("total").GetInt32());
        await api.ExpectError(api.GetAsync($"/api/locations?legalEntityId={Guid.NewGuid()}"), 403, "CROSS_LE_FORBIDDEN");

        // Only listLocations names it in the frozen contract; every other location operation keeps the MOD-0173 400.
        await api.ExpectError(api.GetAsync($"/api/locations/search?legalEntityId={s.LegalEntity}"), 400, "INVALID_REQUEST");
        await api.ExpectError(api.GetAsync($"/api/locations/{s.LocationA}?legalEntityId={s.LegalEntity}"), 400, "INVALID_REQUEST");
        await api.ExpectError(api.GetAsync($"/api/locations?tenantId={s.Tenant}"), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task AC03_a_claim_less_user_works_in_the_selected_company_only()
    {
        var s = new InventoryScenario(_factory);
        _factory.LegalEntities.Register(s.Tenant, s.LegalEntity);
        var claimLess = TestTokens.Mint(s.Tenant, null, s.User, [.. TestTokens.AllPermissions, .. TestTokens.LocationPermissions]);
        var api = new LocationApi(s, claimLess);
        void Company(HttpRequestMessage r) => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", s.LegalEntity.ToString());

        // Without a company: "select a company", nothing read.
        await api.ExpectError(api.GetAsync("/api/locations"), 403, null);

        var site = await api.Expect(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" }, Company), 201);
        Assert.Equal(s.LegalEntity.ToString(), site.GetProperty("legalEntityId").GetString());
        var list = await api.Expect(api.GetAsync("/api/locations?type=Site", configure: Company), 200);
        Assert.Equal(2, list.GetProperty("total").GetInt32());

        var history = s.Persisted("inventory_location_history").Single();
        Assert.Equal("header-unverified-membership", history["LeSource"].AsString);

        // Another company of the tenant that MDM does not confirm → 403 CROSS_LE_FORBIDDEN.
        await api.ExpectError(api.GetAsync("/api/locations", configure: r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", Guid.NewGuid().ToString())),
            403, "CROSS_LE_FORBIDDEN");
    }

    // ── K-04 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC04_an_archived_location_refuses_every_change_and_nothing_deletes_one()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var tree = await api.TreeAsync();
        var zone = await api.Node("Zone", tree.Storage);
        var archived = await api.Expect(api.StatusAsync(zone.Id, "Archived", zone.ETag), 200);
        var etag = $"W/\"{archived.GetProperty("version").GetInt32()}\"";

        await api.ExpectError(api.PatchAsync(zone.Id, new { name = "again" }, etag), 409, "LOCATION_ARCHIVED");
        await api.ExpectError(api.PatchAsync(zone.Id, new { parentId = tree.Storage }, etag), 409, "LOCATION_ARCHIVED");
        await api.ExpectError(api.StatusAsync(zone.Id, "Active", etag), 409, "LOCATION_ARCHIVED");
        await api.ExpectError(api.StatusAsync(zone.Id, "Inactive", etag), 409, "LOCATION_ARCHIVED");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("B"), ["name"] = "n", ["type"] = "Bin", ["parentId"] = zone.Id }),
            409, "LOCATION_ARCHIVED");
        await api.ExpectError(api.BulkAsync(zone.Id, Range("A", "A", 1, 1, 1, 1, 1)), 409, "LOCATION_ARCHIVED");

        // Still readable with its status (K-13) …
        Assert.Equal("Archived", (await api.Get(zone.Id)).GetProperty("status").GetString());

        // … and there is no DELETE route.
        using var delete = await api.Send(HttpMethod.Delete, $"/api/locations/{zone.Id}", null);
        Assert.True((int)delete.StatusCode is 404 or 405, $"DELETE answered {(int)delete.StatusCode}");
        Assert.Equal("Archived", (await api.Get(zone.Id)).GetProperty("status").GetString());
    }

    internal static Dictionary<string, object?> Range(string aisleFrom, string aisleTo, int rackFrom, int rackTo, int levelFrom, int levelTo, int positions, bool? dryRun = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["aisleFrom"] = aisleFrom, ["aisleTo"] = aisleTo, ["rackFrom"] = rackFrom, ["rackTo"] = rackTo,
            ["levelFrom"] = levelFrom, ["levelTo"] = levelTo, ["positionsPerLevel"] = positions
        };
        if (dryRun is not null) body["dryRun"] = dryRun;
        return body;
    }

    internal static List<string?> Codes(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToList();
}
