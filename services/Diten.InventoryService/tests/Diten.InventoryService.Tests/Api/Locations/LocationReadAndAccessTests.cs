using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>
/// MOD-0173-FU02: the frozen v1 reads served by the REAL service (AC-15 — each response is validated against
/// location.openapi.yaml by the contract-validating client), searchLocations, permissions (§14) and the org-unit
/// soft link (§7, §12 row 7).
/// </summary>
public sealed class LocationReadAndAccessTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public LocationReadAndAccessTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC15_every_v1_operation_answers_inside_the_contract_on_success_and_on_404()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var unknown = Guid.NewGuid().ToString();

        var location = await api.Get(s.LocationA);
        Assert.Equal(s.LocationA, location.GetProperty("locationId").GetString());
        Assert.Equal("Bin", location.GetProperty("type").GetString());
        Assert.Equal("v1", location.GetProperty("contractVersion").GetString());
        await api.ExpectError(api.GetAsync($"/api/locations/{unknown}"), 404, "UNKNOWN_LOCATION");
        await api.ExpectError(api.GetAsync("/api/locations/not-a-uuid"), 404, "UNKNOWN_LOCATION");

        var list = await api.Expect(api.GetAsync("/api/locations?type=Bin"), 200);
        Assert.Equal(3, list.GetProperty("total").GetInt32());
        var children = await api.Expect(api.GetAsync($"/api/locations?parentId={s.StorageNode.Id:D}"), 200);
        Assert.Equal(new string?[] { s.LocationA, s.LocationB }.OrderBy(x => x).ToList(),
            children.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("locationId").GetString()).OrderBy(x => x).ToList());
        await api.ExpectError(api.GetAsync("/api/locations?type=Shelf"), 400, "INVALID_REQUEST");

        var path = await api.Expect(api.GetAsync($"/api/locations/{s.LocationA}/path"), 200);
        Assert.Equal(["Site", "Warehouse", "StorageLocation", "Bin"],
            path.GetProperty("path").EnumerateArray().Select(p => p.GetProperty("type").GetString()).ToList());
        await api.ExpectError(api.GetAsync($"/api/locations/{unknown}/path"), 404, "UNKNOWN_LOCATION");

        var validation = await api.Expect(api.ValidateAsync(s.LocationA, unknown, "loc-legacy-id", s.StorageNode.Id.ToString("D")), 200);
        var results = validation.GetProperty("results").EnumerateArray().ToList();
        Assert.True(results[0].GetProperty("exists").GetBoolean());
        Assert.True(results[0].GetProperty("isStockable").GetBoolean());
        Assert.False(results[1].GetProperty("exists").GetBoolean());
        Assert.False(results[1].TryGetProperty("isStockable", out _));
        Assert.Equal("loc-legacy-id", results[2].GetProperty("locationId").GetString());
        Assert.False(results[2].GetProperty("exists").GetBoolean());
        Assert.False(results[3].GetProperty("isStockable").GetBoolean());
        await api.ExpectError(api.Send(HttpMethod.Post, "/api/locations/validate", new { ids = new[] { s.LocationA } }), 400, "INVALID_REQUEST");

        // Every v1 operation was seen by the validating handler on success.
        foreach (var operation in new[] { "GET /api/locations/{id} 200", "GET /api/locations 200", "GET /api/locations/{id}/path 200", "POST /api/locations/validate 200" })
        {
            Assert.Contains(ContractValidatingHandler.Validated.Keys, k => k.StartsWith(operation, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Search_pages_filters_and_orders_and_refuses_malformed_parameters()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await api.Expect(api.StatusAsync(s.LocationB, "Inactive", "W/\"1\""), 200);

        var page = await api.Expect(api.GetAsync("/api/locations/search?page=2&pageSize=3&orderBy=code"), 200);
        Assert.Equal(8, page.GetProperty("total").GetInt64());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(3, page.GetProperty("items").GetArrayLength());

        var bins = await api.Expect(api.GetAsync("/api/locations/search?type=Bin&type=Zone&status=Active&isStockable=true"), 200);
        Assert.Equal(new string?[] { s.LocationA, s.LocationC }.OrderBy(x => x).ToList(),
            bins.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("locationId").GetString()).OrderBy(x => x).ToList());

        var code = (await api.Get(s.LocationC)).GetProperty("code").GetString()!;
        var found = await api.Expect(api.GetAsync($"/api/locations/search?search={code.ToLowerInvariant()}"), 200);
        Assert.Equal(s.LocationC, Assert.Single(found.GetProperty("items").EnumerateArray()).GetProperty("locationId").GetString());

        var desc = await api.Expect(api.GetAsync("/api/locations/search?orderBy=type&orderDir=desc&pageSize=1"), 200);
        // Type sorts by its wire name (Bin < Site < StorageLocation < Warehouse < Zone) — ASSUMPTION-LOC1-11.
        Assert.Equal("Warehouse", desc.GetProperty("items")[0].GetProperty("type").GetString());
        var underParent = await api.Expect(api.GetAsync($"/api/locations/search?parentId={s.SiteNode.Id:D}"), 200);
        Assert.Equal(2, underParent.GetProperty("total").GetInt64());

        foreach (var bad in new[] { "page=0", "pageSize=201", "type=Shelf", "status=Gone", "isStockable=yes", "orderBy=parent", "orderDir=up", "parentId=x", "search=" + new string('x', 101) })
        {
            await api.ExpectError(api.GetAsync("/api/locations/search?" + bad), 400, "INVALID_REQUEST");
        }
    }

    [Fact]
    public async Task Reads_need_locations_read_and_writes_need_locations_manage()
    {
        var s = new InventoryScenario(_factory);
        var none = new LocationApi(s, TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions));
        var readOnly = new LocationApi(s, TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.LocationsReadPermission]));
        var anonymous = new LocationApi(s, string.Empty);

        await none.Expect(none.GetAsync($"/api/locations/{s.LocationA}"), 403);
        await none.Expect(none.GetAsync("/api/locations/search"), 403);
        await none.Expect(none.ValidateAsync(s.LocationA), 403);
        await anonymous.Expect(anonymous.GetAsync("/api/locations"), 401);

        await readOnly.Expect(readOnly.GetAsync($"/api/locations/{s.LocationA}/history"), 200);
        await readOnly.Expect(readOnly.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" }), 403);
        await readOnly.Expect(readOnly.PatchAsync(s.LocationA, new { name = "x" }, "W/\"1\""), 403);
        await readOnly.Expect(readOnly.StatusAsync(s.LocationA, "Inactive", "W/\"1\""), 403);
        await readOnly.Expect(readOnly.BulkAsync(s.StorageNode.Id.ToString("D"), LocationCodeAndScopeTests.Range("A", "A", 1, 1, 1, 1, 1)), 403);
        Assert.Empty(s.Persisted("inventory_location_history"));
    }

    [Fact]
    public async Task Org_unit_must_be_an_active_unit_of_the_same_company_and_is_checked_only_when_it_changes()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var unit = _factory.OrgUnits.AddUnit(s.LegalEntity);
        var foreignUnit = _factory.OrgUnits.AddUnit(Guid.NewGuid());

        var site = await api.Expect(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["orgUnitRef"] = unit.ToString() }), 201);
        Assert.Equal(unit.ToString(), site.GetProperty("orgUnitRef").GetString());

        var before = s.Persisted("inventory_locations").Count;
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["orgUnitRef"] = foreignUnit.ToString() }),
            422, "ORG_UNIT_INVALID");
        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["orgUnitRef"] = Guid.NewGuid().ToString() }),
            422, "ORG_UNIT_INVALID");
        Assert.Equal(before, s.Persisted("inventory_locations").Count);

        var id = site.GetProperty("locationId").GetString()!;
        var calls = _factory.OrgUnits.Calls;
        await api.Expect(api.PatchAsync(id, new { orgUnitRef = unit.ToString(), name = "renamed" }, "W/\"1\""), 200);
        Assert.Equal(calls, _factory.OrgUnits.Calls);
        await api.ExpectError(api.PatchAsync(id, new { orgUnitRef = foreignUnit.ToString() }, "W/\"2\""), 422, "ORG_UNIT_INVALID");
        var cleared = await api.Expect(api.PatchAsync(id, new { orgUnitRef = (string?)null }, "W/\"2\""), 200);
        Assert.False(cleared.TryGetProperty("orgUnitRef", out _));
    }
}

/// <summary>§12 row 7 with the REAL Platform org-unit HTTP client pointed at a closed port: 503, nothing written.</summary>
public sealed class LocationOrgUnitDirectoryDownTests : IClassFixture<RealOrgUnitClientApiFactory>
{
    private readonly RealOrgUnitClientApiFactory _factory;

    public LocationOrgUnitDirectoryDownTests(RealOrgUnitClientApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_unreachable_org_unit_directory_is_503_ORG_UNIT_DIRECTORY_UNAVAILABLE_and_the_field_can_be_left_empty()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var before = s.Persisted("inventory_locations").Count;

        await api.ExpectError(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site", ["orgUnitRef"] = Guid.NewGuid().ToString() }),
            503, "ORG_UNIT_DIRECTORY_UNAVAILABLE");
        await api.ExpectError(api.PatchAsync(s.LocationA, new { orgUnitRef = Guid.NewGuid().ToString() }, "W/\"1\""), 503, "ORG_UNIT_DIRECTORY_UNAVAILABLE");
        Assert.Equal(before, s.Persisted("inventory_locations").Count);
        Assert.Empty(s.Persisted("inventory_location_history"));

        await api.Expect(api.CreateAsync(new() { ["code"] = LocationApi.Code("S"), ["name"] = "n", ["type"] = "Site" }), 201);
    }
}
