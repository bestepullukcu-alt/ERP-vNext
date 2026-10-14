using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.Locations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU02 (LOC2) location proxies driven on the REAL controllers with a Gateway handler that answers what
/// Gateway 5000 + InventoryService answer (LOCATION v1.1). Measures: the L-2 wire → searchLocations translation (one
/// call per page, search / sort / filters forwarded, a parent path per distinct parent); permission refusals that never
/// reach the Gateway; the W-0173-02 company header on /api/locations; the owner writes' Idempotency-Key, If-Match,
/// PATCH body (only contract members, never `type`, never a company) and ETag; contract refusals as localized
/// sentences with their status and code kept; the tree's lazy children (> 500 → hand over to the list); the org-unit
/// picker (same company, Active, not archived; Platform refusal → "unavailable", never a hardcoded list).
/// </summary>
public sealed class LocationProxyControllerTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid TokenCompanyId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string Site = "a1111111-0000-0000-0000-000000000001";
    private const string Warehouse = "a2222222-0000-0000-0000-000000000002";
    private const string Bin = "a3333333-0000-0000-0000-000000000003";

    private static readonly string[] ReadOnly = [LocationPermissionKeys.Read];
    private static readonly string[] Manager = [LocationPermissionKeys.Read, LocationPermissionKeys.Manage];

    private static string Ref(string id, string code, string type, string? parent, string status = "Active") =>
        $$"""{"locationId":"{{id}}","code":"{{code}}","name":"{{code}} name","type":"{{type}}",{{(parent is null ? "" : $"\"parentId\":\"{parent}\",")}}"legalEntityId":"{{TokenCompanyId}}","status":"{{status}}","isStockable":{{(type == "Bin" ? "true" : "false")}},"contractVersion":"v1"}""";

    // ── L-2 list ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_page_3_with_search_sort_and_filters_is_one_search_call()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/locations/search", 200, $$"""{"items":[{{Ref(Bin, "A-01-01-01", "Bin", Warehouse)}}],"total":51,"page":3,"pageSize":25,"contractVersion":"v1"}""");
        gateway.On($"/api/locations/{Warehouse}/path", 200, $$"""{"locationId":"{{Warehouse}}","path":[{"type":"Site","id":"{{Site}}","code":"TR"},{"type":"Warehouse","id":"{{Warehouse}}","code":"WH-01"}],"contractVersion":"v1"}""");
        var controller = Locations(gateway, ReadOnly,
            ("start", "50"), ("length", "25"), ("draw", "4"), ("search", "a-01"), ("orderBy", "name"), ("orderDir", "desc"),
            ("type", "Bin"), ("type", "Zone"), ("status", "Active"), ("isStockable", "true"), ("parentId", Warehouse.ToUpperInvariant()));

        var json = Body(await controller.List(CancellationToken.None));

        var search = Assert.Single(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/locations/search", StringComparison.Ordinal));
        Assert.Equal($"/api/locations/search?page=3&pageSize=25&search=a-01&type=Bin&type=Zone&status=Active&isStockable=true&parentId={Warehouse}&orderBy=name&orderDir=desc", search.PathAndQuery);
        Assert.Equal(TenantId.ToString(), search.TenantHeader);
        // One path call for the one distinct parent on the page.
        Assert.Single(gateway.Requests, r => r.PathAndQuery == $"/api/locations/{Warehouse}/path");
        var data = json.GetProperty("data");
        Assert.Equal(51, data.GetProperty("total").GetInt32());
        Assert.Equal(51, data.GetProperty("filteredTotal").GetInt32());
        var row = data.GetProperty("items")[0];
        Assert.Equal(Bin, row.GetProperty("id").GetString());
        Assert.Equal("TR / WH-01", row.GetProperty("parentPath").GetString());
        // K-10: no quantity on any location row.
        Assert.False(row.TryGetProperty("onHand", out _));
    }

    [Theory]
    [InlineData("orderBy", "parentPath")]
    [InlineData("orderDir", "sideways")]
    [InlineData("type", "Shelf")]
    [InlineData("status", "Deleted")]
    [InlineData("isStockable", "maybe")]
    [InlineData("parentId", "not-a-uuid")]
    [InlineData("start", "7")]
    [InlineData("length", "500")]
    public async Task A_query_outside_the_contract_is_refused_before_the_gateway(string key, string value)
    {
        var gateway = new FakeGateway();
        var query = new List<(string, string)> { ("start", "0"), ("length", "25") };
        query.RemoveAll(p => p.Item1 == key);
        query.Add((key, value));
        if (key == "orderDir") query.Add(("orderBy", "code"));

        var result = Assert.IsType<JsonResult>(await Locations(gateway, ReadOnly, [.. query]).List(CancellationToken.None));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(gateway.Requests);
        Assert.Equal("ErrInvalidRequest", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    // ── permissions (display decisions here; InventoryService [HasPermission] stays the authority) ────────────

    [Fact]
    public async Task Without_read_no_location_read_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var results = new[]
        {
            await Locations(gateway, [], ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Locations(gateway, []).Node(Site, CancellationToken.None),
            await Locations(gateway, []).History(Site, 1, CancellationToken.None),
            await Locations(gateway, []).Parents("x", ["Site"], true, CancellationToken.None),
            await Tree(gateway, []).Children(null, false, CancellationToken.None)
        };

        Assert.All(results, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.Empty(gateway.Requests);
        Assert.Equal("ErrNoPermission", Body(results[0]).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_read_only_user_cannot_write_through_the_proxy()
    {
        var gateway = new FakeGateway();
        var controller = Locations(gateway, ReadOnly);
        var results = new[]
        {
            await controller.Create(new LocationCreateRequestViewModel { IdempotencyKey = Guid.NewGuid().ToString(), Code = "S", Name = "S", Type = "Site" }, CancellationToken.None),
            await controller.Edit(Site, new LocationEditRequestViewModel { IfMatch = "W/\"1\"", Changes = Json("""{"name":"x"}""") }, CancellationToken.None),
            await controller.Status(Site, new LocationStatusRequestViewModel { Status = "Inactive", IfMatch = "W/\"1\"" }, CancellationToken.None),
            await controller.Bins(Site, Bulk(dryRun: true), CancellationToken.None),
            await controller.OrgUnits(CancellationToken.None)
        };

        Assert.All(results, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.Empty(gateway.Requests);
    }

    // ── W-0173-02: the company travels on /api/locations exactly as on /api/inventory ──────────────────────

    [Fact]
    public async Task A_claimless_user_with_a_selected_company_sends_it_on_location_calls()
    {
        var company = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var gateway = new FakeGateway();
        gateway.On("/api/locations/search", 200, """{"items":[],"total":0,"page":1,"pageSize":25}""");

        await LocationsAs(new Caller(null, company), gateway, ReadOnly, ("start", "0"), ("length", "25")).List(CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal(company.ToString(), call.LegalEntity);
    }

    [Fact]
    public async Task A_claimless_user_without_a_company_makes_no_location_call()
    {
        var gateway = new FakeGateway();

        var result = Assert.IsType<JsonResult>(await LocationsAs(new Caller(null, null), gateway, ReadOnly, ("start", "0"), ("length", "25")).List(CancellationToken.None));

        Assert.Empty(gateway.Requests);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("ErrSelectCompany", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    // ── L-3 writes ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_posts_the_contract_body_with_the_forms_key_and_no_company()
    {
        var key = Guid.NewGuid();
        var gateway = new FakeGateway();
        gateway.On("/api/locations", 201, Ref(Warehouse, "WH-01", "Warehouse", Site), etag: "W/\"1\"");

        var result = await Locations(gateway, Manager).Create(new LocationCreateRequestViewModel
        {
            IdempotencyKey = key.ToString(), Code = " WH-01 ", Name = "Central", Type = "Warehouse", ParentId = Site.ToUpperInvariant(),
            OperatingMode = "internal", CostCenterCode = "MM-1100"
        }, CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal("/api/locations", call.PathAndQuery);
        Assert.Equal(key.ToString("D"), call.IdempotencyKey);
        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal(key.ToString("D"), body.GetProperty("idempotencyKey").GetString());
        Assert.Equal("WH-01", body.GetProperty("code").GetString());
        Assert.Equal(Site, body.GetProperty("parentId").GetString());
        Assert.Equal("internal", body.GetProperty("operatingMode").GetString());
        Assert.False(body.TryGetProperty("legalEntityId", out _)); // K-03: never a body field
        Assert.False(body.TryGetProperty("isStockable", out _));   // not sent → the service's type default
        var json = Body(result);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("W/\"1\"", json.GetProperty("etag").GetString());
        Assert.Equal("LocationCreated", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_contract_refusal_keeps_status_code_and_reference_and_becomes_a_sentence()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/locations", 409, """{"error":{"code":"LOCATION_CODE_TAKEN","message":"taken","correlationId":"c-1"},"contractVersion":"v1"}""");

        var result = Assert.IsType<JsonResult>(await Locations(gateway, Manager).Create(new LocationCreateRequestViewModel
        {
            IdempotencyKey = Guid.NewGuid().ToString(), Code = "TR", Name = "TR", Type = "Site"
        }, CancellationToken.None));

        Assert.Equal(409, result.StatusCode);
        var error = Body(result).GetProperty("error");
        Assert.Equal("LOCATION_CODE_TAKEN", error.GetProperty("code").GetString());
        Assert.Equal("ErrCodeTaken", error.GetProperty("message").GetString());
        Assert.Equal("c-1", error.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Edit_patches_only_contract_members_with_if_match()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/locations/{Warehouse}", 200, Ref(Warehouse, "WH-02", "Warehouse", Site), etag: "W/\"4\"");

        var result = await Locations(gateway, Manager).Edit(Warehouse, new LocationEditRequestViewModel
        {
            IfMatch = "W/\"3\"",
            Changes = Json("""{"code":"WH-02","orgUnitRef":null,"costCenterCode":"MM-2"}""")
        }, CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("PATCH", call.Method);
        Assert.Equal("W/\"3\"", call.IfMatch);
        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal("WH-02", body.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("orgUnitRef").ValueKind); // null = clear (PATCH semantics)
        Assert.False(body.TryGetProperty("name", out _));                          // absent = unchanged
        Assert.Equal("W/\"4\"", Body(result).GetProperty("etag").GetString());
    }

    [Theory]
    [InlineData("""{"type":"Bin"}""", "W/\"1\"")]               // LOC-04: type is never accepted
    [InlineData("""{"legalEntityId":"x"}""", "W/\"1\"")]        // K-03: no company in a body
    [InlineData("""{"isStockable":"yes"}""", "W/\"1\"")]        // wrong JSON kind
    [InlineData("""{}""", "W/\"1\"")]                           // nothing to change
    [InlineData("""{"name":"x"}""", "")]                        // no If-Match
    [InlineData("""{"name":"x"}""", "\"1\"")]                   // not the contract's weak tag
    public async Task An_edit_outside_the_contract_is_refused_before_the_gateway(string changes, string ifMatch)
    {
        var gateway = new FakeGateway();

        var result = Assert.IsType<JsonResult>(await Locations(gateway, Manager).Edit(Warehouse,
            new LocationEditRequestViewModel { IfMatch = ifMatch, Changes = Json(changes) }, CancellationToken.None));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Status_change_posts_with_if_match_and_a_refusal_stays_a_sentence()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/locations/{Bin}/status", 422, """{"error":{"code":"LOCATION_HAS_STOCK","message":"stock","correlationId":"c-2"},"contractVersion":"v1"}""");

        var result = Assert.IsType<JsonResult>(await Locations(gateway, Manager).Status(Bin,
            new LocationStatusRequestViewModel { Status = "Inactive", IfMatch = "W/\"2\"" }, CancellationToken.None));

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal("W/\"2\"", call.IfMatch);
        Assert.Null(call.IdempotencyKey);
        Assert.Equal("Inactive", JsonDocument.Parse(call.Body).RootElement.GetProperty("status").GetString());
        Assert.Equal(422, result.StatusCode);
        Assert.Equal("ErrHasStock", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Bulk_preview_and_run_use_one_key_and_the_preview_writes_nothing_itself()
    {
        var key = Guid.NewGuid();
        var gateway = new FakeGateway();
        gateway.On($"/api/locations/{Warehouse}/bins/bulk", 200, """{"toCreate":["A-01-01-01","A-01-01-02"],"skipped":["A-01-01-03"],"count":2,"contractVersion":"v1"}""");
        var controller = Locations(gateway, Manager);

        var preview = Body(await controller.Bins(Warehouse, Bulk(dryRun: true, key), CancellationToken.None));

        var call = Assert.Single(gateway.Requests);
        Assert.Equal(key.ToString("D"), call.IdempotencyKey);
        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.True(body.GetProperty("dryRun").GetBoolean());
        Assert.Equal("A", body.GetProperty("aisleFrom").GetString());
        Assert.Equal(2, body.GetProperty("positionsPerLevel").GetInt32());
        Assert.True(preview.GetProperty("dryRun").GetBoolean());
        Assert.Equal(2, preview.GetProperty("count").GetInt32());
        Assert.Equal("A-01-01-03", preview.GetProperty("skipped")[0].GetString());

        gateway.Routes.Clear();
        gateway.On($"/api/locations/{Warehouse}/bins/bulk", 201, $$"""{"created":[{{Ref(Bin, "A-01-01-01", "Bin", Warehouse)}}],"skipped":[],"count":1,"contractVersion":"v1"}""");
        var run = Body(await controller.Bins(Warehouse, Bulk(dryRun: false, key), CancellationToken.None));

        Assert.Equal(2, gateway.Requests.Count);
        Assert.Equal(key.ToString("D"), gateway.Requests[1].IdempotencyKey);
        Assert.False(JsonDocument.Parse(gateway.Requests[1].Body).RootElement.GetProperty("dryRun").GetBoolean());
        Assert.False(run.GetProperty("dryRun").GetBoolean());
        Assert.Equal("A-01-01-01", run.GetProperty("created")[0].GetString());
    }

    [Theory]
    [InlineData("AB", 1)]   // aisle: one letter or 1–3 digits
    [InlineData("A", 0)]    // rack below 1
    [InlineData("A", 100)]  // rack above 99
    public async Task A_bulk_request_outside_the_contract_shape_is_refused_before_the_gateway(string aisle, int rack)
    {
        var gateway = new FakeGateway();
        var model = Bulk(dryRun: true);
        model.AisleFrom = aisle;
        model.RackFrom = rack;

        var result = Assert.IsType<JsonResult>(await Locations(gateway, Manager).Bins(Warehouse, model, CancellationToken.None));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(gateway.Requests);
    }

    // ── L-1 node / tree ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Node_returns_the_location_its_etag_path_and_presence_without_a_quantity()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/locations/{Bin}/path", 200, $$"""{"locationId":"{{Bin}}","path":[{"type":"Site","id":"{{Site}}","code":"TR"},{"type":"Bin","id":"{{Bin}}","code":"A-1"}]}""");
        gateway.On($"/api/locations/{Bin}/stock-presence", 200, $$"""{"locationId":"{{Bin}}","hasStock":true,"subtreeHasStock":false,"contractVersion":"v1"}""");
        gateway.On($"/api/locations/{Bin}", 200, Ref(Bin, "A-1", "Bin", Warehouse), etag: "W/\"7\"");

        var json = Body(await Locations(gateway, ReadOnly).Node(Bin.ToUpperInvariant(), CancellationToken.None));

        Assert.Equal("W/\"7\"", json.GetProperty("etag").GetString());
        Assert.Equal("A-1", json.GetProperty("location").GetProperty("code").GetString());
        Assert.Equal(2, json.GetProperty("path").GetArrayLength());
        Assert.True(json.GetProperty("presence").GetProperty("hasStock").GetBoolean());
        Assert.False(json.GetProperty("presence").GetProperty("subtreeHasStock").GetBoolean());
        Assert.DoesNotContain("onHand", json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        // the id goes out as the canonical lower-case UUID the service stores (LOC1-03)
        Assert.All(gateway.Requests, r => Assert.Contains(Bin, r.PathAndQuery, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_roots_are_the_companys_sites_without_archived_unless_asked()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/locations/search", 200, $$"""{"items":[{{Ref(Site, "TR", "Site", null)}}],"total":1,"page":1,"pageSize":200}""");

        var json = Body(await Tree(gateway, ReadOnly).Children(null, false, CancellationToken.None));
        await Tree(gateway, ReadOnly).Children(null, true, CancellationToken.None);

        Assert.Equal("/api/locations/search?page=1&pageSize=200&type=Site&status=Active&status=Inactive&orderBy=code&orderDir=asc", gateway.Requests[0].PathAndQuery);
        Assert.Equal("/api/locations/search?page=1&pageSize=200&type=Site&orderBy=code&orderDir=asc", gateway.Requests[1].PathAndQuery);
        var node = json.GetProperty("items")[0];
        Assert.Equal("TR", node.GetProperty("code").GetString());
        Assert.True(node.GetProperty("canHaveChildren").GetBoolean());
        Assert.False(json.GetProperty("tooMany").GetBoolean());
    }

    [Fact]
    public async Task Above_500_children_the_tree_hands_over_to_the_list()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/locations/search", 200, $$"""{"items":[{{Ref(Bin, "A-1", "Bin", Warehouse)}}],"total":501,"page":1,"pageSize":200}""");

        var json = Body(await Tree(gateway, ReadOnly).Children(Warehouse, false, CancellationToken.None));

        Assert.True(json.GetProperty("tooMany").GetBoolean());
        Assert.Equal(501, json.GetProperty("total").GetInt32());
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
        Assert.Single(gateway.Requests);
    }

    // ── org-unit picker ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Org_units_are_the_same_companys_active_units_only()
    {
        var other = Guid.NewGuid();
        var gateway = new FakeGateway();
        gateway.On("/api/platform/organization-units", 200, $$"""
            {"succeeded":true,"data":[
              {"id":"{{Guid.Parse("b1000000-0000-0000-0000-000000000001")}}","code":"LOG","name":"Logistics","legalEntityId":"{{TokenCompanyId}}","status":"Active","isArchived":false},
              {"id":"{{Guid.NewGuid()}}","code":"OLD","name":"Old","legalEntityId":"{{TokenCompanyId}}","status":"Active","isArchived":true},
              {"id":"{{Guid.NewGuid()}}","code":"DRAFT","name":"Draft","legalEntityId":"{{TokenCompanyId}}","status":"Draft","isArchived":false},
              {"id":"{{Guid.NewGuid()}}","code":"DE","name":"Other company","legalEntityId":"{{other}}","status":"Active","isArchived":false}
            ]}
            """);

        var json = Body(await Locations(gateway, Manager).OrgUnits(CancellationToken.None));

        Assert.True(json.GetProperty("available").GetBoolean());
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("b1000000-0000-0000-0000-000000000001", item.GetProperty("value").GetString());
        Assert.Equal("LOG · Logistics", item.GetProperty("text").GetString());
        Assert.Null(Assert.Single(gateway.Requests).LegalEntity); // not a company-scoped inventory path
    }

    [Fact]
    public async Task Without_platform_access_the_org_unit_list_is_unavailable_and_the_form_still_works()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/platform/organization-units", 403, """{"succeeded":false}""");

        var json = Body(await Locations(gateway, Manager).OrgUnits(CancellationToken.None));

        Assert.False(json.GetProperty("available").GetBoolean());
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
        Assert.Equal("OrgUnitsUnavailable", json.GetProperty("message").GetString());
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static BulkBinsRequestViewModel Bulk(bool dryRun, Guid? key = null) => new()
    {
        IdempotencyKey = (key ?? Guid.NewGuid()).ToString(),
        AisleFrom = "A", AisleTo = "A", RackFrom = 1, RackTo = 1, LevelFrom = 1, LevelTo = 1, PositionsPerLevel = 2,
        DryRun = dryRun
    };

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static JsonElement Body(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    /// <summary>Who calls: the token's company (claim) and/or the company in the selector cookie (W-0173-02).</summary>
    private sealed record Caller(Guid? TokenCompany, Guid? CookieCompany);

    private static LocationsController Locations(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
        => LocationsAs(new Caller(TokenCompanyId, null), gateway, permissions, query);

    private static LocationsController LocationsAs(Caller caller, FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new LocationsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<LocationsIndex>(), NullLogger<LocationsController>.Instance);
        controller.ControllerContext = Context(caller, query);
        return controller;
    }

    private static LocationTreeController Tree(FakeGateway gateway, string[] permissions)
    {
        var controller = new LocationTreeController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<LocationsIndex>(), NullLogger<LocationTreeController>.Instance);
        controller.ControllerContext = Context(new Caller(TokenCompanyId, null), []);
        return controller;
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ControllerContext Context(Caller caller, (string Key, string Value)[] query)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001") };
        if (caller.TokenCompany is not null) claims.Add(new Claim("legal_entity_id", caller.TokenCompany.Value.ToString()));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (caller.CookieCompany is not null)
            http.Request.Headers.Cookie = $"{CompanySelection.CookieName(http.User)}={caller.CookieCompany.Value}";
        http.Request.Query = new QueryCollection(query.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));
        return new ControllerContext { HttpContext = http };
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    /// <summary>Answers with the KEY, so a test can assert which resx sentence was chosen.</summary>
    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        public List<(string Prefix, int Status, string Body, string? ETag)> Routes { get; } = [];

        public List<(string Method, string PathAndQuery, string? TenantHeader, string? IdempotencyKey, string? IfMatch, string Body, string? LegalEntity)> Requests { get; } = [];

        public void On(string prefix, int status, string body, string? etag = null) => Routes.Add((prefix, status, body, etag));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.Method.Method, pathAndQuery,
                    request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null,
                    request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                    request.Headers.TryGetValues("If-Match", out var ifMatch) ? ifMatch.Single() : null,
                    body,
                    request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null));
            }

            // Longest prefix first, so /api/locations/{id}/path wins over /api/locations/{id}.
            var route = Routes.Where(r => pathAndQuery.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            if (route.Prefix is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            var response = new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
            if (route.ETag is not null) response.Headers.TryAddWithoutValidation("ETag", route.ETag);
            return response;
        }
    }
}
