using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Services;
using Diten.Web.Tests.Traceability;
using Diten.Web.Views.Inventory.StockLedger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Contracts;

/// <summary>
/// WP-MVP1-FIX-LIVE1 — the 2026-10-14 live findings, measured with Gateway answers that are VALID CONTRACT BODIES
/// (<see cref="ContractGateway"/>; a fake body that breaks its contract cannot even be registered):
/// <list type="bullet">
/// <item>(a) FEFO / Expiry <c>lookups/warehouses</c> gives every option a value — LOCATION <c>LocationRef.locationId</c>
/// (location.openapi.yaml:208), the live answer was <c>{"value":null}</c>, so FEFO Sırası could not be computed;</item>
/// <item>(b) Karantina Ayarları sample warehouse / location options carry a value (same contract line); the Quarantine lot picker
/// carries TRACE <c>LotRef.lotId</c> (trace.openapi.yaml:291);</item>
/// <item>(c) Stok Defteri shows the LOT NUMBER (TRACE listLots, one call per SKU on the page — not per lot), with the id and an
/// "unresolved" hint when the lot cannot be read;</item>
/// <item>(d) the contract check itself: a LOCATION row carrying <c>id</c> (the shape the old tests fed) is refused.</item>
/// </list>
/// </summary>
public sealed class LiveLookupContractTests
{
    private const string Gateway = "http://gateway.test";
    private const string Warehouse = "11111111-0000-4000-8000-000000000001";
    private const string Bin = "11111111-0000-4000-8000-000000000002";
    private const string Company = "22222222-0000-4000-8000-000000000001";
    private const string Item = "33333333-0000-4000-8000-000000000001";
    private const string SkuA = "44444444-0000-4000-8000-00000000000a";
    private const string SkuB = "44444444-0000-4000-8000-00000000000b";
    private const string LotA1 = "55555555-0000-4000-8000-0000000000a1";
    private const string LotA2 = "55555555-0000-4000-8000-0000000000a2";
    private const string LotB1 = "55555555-0000-4000-8000-0000000000b1";

    private static string LocationRef(string id, string code, string name, string type, string? parent, bool stockable) => JsonSerializer.Serialize(new
    {
        locationId = id, code, name, type, parentId = parent, legalEntityId = Company, status = "Active", isStockable = stockable, contractVersion = "v1"
    });

    private static readonly string WarehouseList = $$"""{"items":[{{LocationRef(Warehouse, "D01", "İstanbul Merkez Depo", "Warehouse", "99999999-0000-4000-8000-000000000001", false)}}],"total":1,"contractVersion":"v1"}""";

    private static readonly string StockableSearch = $$"""{"items":[{{LocationRef(Bin, "A-01-01-01", "A-01-01-01", "Bin", Warehouse, true)}}],"total":1,"page":1,"pageSize":200,"contractVersion":"v1"}""";

    // ── (a) Expiry / FEFO warehouse picker ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_the_FEFO_warehouse_picker_values_are_the_contract_locationId()
    {
        var gateway = new ContractGateway().OnContract("/api/locations?type=Warehouse", WebContractSchemas.Location, WebContractSchemas.LocationList, WarehouseList);
        var items = Body(await Expiry(gateway).Lookups("warehouses", null, CancellationToken.None)).GetProperty("items");
        var option = Assert.Single(items.EnumerateArray());
        Assert.Equal(Warehouse, option.GetProperty("value").GetString());
        Assert.Equal("D01 · İstanbul Merkez Depo", option.GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_FEFO_order_with_the_picked_warehouse_reaches_the_service_and_answers_its_contract_body()
    {
        var order = JsonSerializer.Serialize(new
        {
            legalEntityId = Company, warehouseId = Warehouse, itemId = Item, skuId = SkuA, skuLevel = "Lsku", asOf = "2026-10-14", basis = "onHandAvailable",
            skuAvailable = 30, eligible = new[] { new { rank = 1, lotId = LotA1, lotNumber = "L-2026-001", expiryDate = "2027-01-31", available = 30, severity = "Ok" } },
            excluded = Array.Empty<object>()
        });
        var gateway = new ContractGateway()
            .OnContract("/api/locations?type=Warehouse", WebContractSchemas.Location, WebContractSchemas.LocationList, WarehouseList)
            .OnContract("/api/expiry/fefo-order", WebContractSchemas.Fefo, WebContractSchemas.FefoOrder, order);
        var controller = Expiry(gateway);
        var picked = Body(await controller.Lookups("warehouses", null, CancellationToken.None)).GetProperty("items")[0].GetProperty("value").GetString();

        controller = Expiry(gateway, ("warehouseId", picked!), ("itemId", Item), ("skuId", SkuA), ("skuLevel", "Lsku"));
        var answer = Body(await controller.FefoOrderData(CancellationToken.None)).GetProperty("order");
        Assert.Equal("L-2026-001", answer.GetProperty("eligible")[0].GetProperty("lotNumber").GetString());
        Assert.Contains(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/expiry/fefo-order?", StringComparison.Ordinal)
                                               && r.PathAndQuery.Contains($"warehouseId={Warehouse}", StringComparison.Ordinal));
    }

    // ── (b) Karantina Ayarları sample warehouse / location + the lot picker ───────────────────────────────────────────

    [Theory]
    [InlineData("warehouses", Warehouse, "D01 · İstanbul Merkez Depo")]
    [InlineData("locations", Bin, "A-01-01-01")] // a bin whose name repeats its code shows the code once (LocationOptions.Label)
    public async Task B_the_quarantine_sample_warehouse_and_location_options_carry_the_contract_locationId(string kind, string expected, string text)
    {
        var gateway = new ContractGateway()
            .OnContract("/api/locations?type=Warehouse", WebContractSchemas.Location, WebContractSchemas.LocationList, WarehouseList)
            .OnContract("/api/locations/search", WebContractSchemas.Location, WebContractSchemas.LocationSearchPage, StockableSearch);
        var option = Assert.Single(Body(await Quarantine(gateway).Lookups(kind, null, null, null, null, CancellationToken.None)).GetProperty("items").EnumerateArray());
        Assert.Equal(expected, option.GetProperty("value").GetString());
        Assert.Equal(text, option.GetProperty("text").GetString());
    }

    [Fact]
    public async Task B_the_quarantine_lot_picker_values_are_the_contract_lotId()
    {
        var lots = $$"""{"items":[{"lotId":"{{LotA1}}","lotNumber":"L-2026-001","itemId":"{{Item}}","status":"Released","skuId":"{{SkuA}}","skuLevel":"Lsku"}],"total":1,"page":1,"pageSize":20,"contractVersion":"v1"}""";
        var gateway = new ContractGateway().OnContract("/api/trace/lots", WebContractSchemas.Trace, WebContractSchemas.LotList, lots);
        var option = Assert.Single(Body(await Quarantine(gateway).Lookups("lots", "L-2026", null, null, null, CancellationToken.None)).GetProperty("items").EnumerateArray());
        Assert.Equal(LotA1, option.GetProperty("value").GetString());
        Assert.Equal("L-2026-001", option.GetProperty("text").GetString());
    }

    // ── (c) Stok Defteri: the lot number, one TRACE read per SKU ──────────────────────────────────────────────────────

    private static string Balance(params (string Sku, string Lot)[] rows) => JsonSerializer.Serialize(new
    {
        rows = rows.Select(r => new
        {
            itemId = Item, skuId = r.Sku, skuLevel = "Lsku", legalEntityId = Company, warehouseId = Warehouse, locationId = Bin, lotId = r.Lot,
            stockStatus = "AVAILABLE", onHand = "10", reserved = "0", available = "10", inTransit = "0", blocked = "0", qualityInspection = "0", baseUomId = "EA"
        }),
        total = rows.Length, contractVersion = "v1"
    });

    private static string LotsOf(string sku, params (string Id, string Number)[] lots) => JsonSerializer.Serialize(new
    {
        items = lots.Select(l => new { lotId = l.Id, lotNumber = l.Number, itemId = Item, status = "Released", skuId = sku, skuLevel = "Lsku" }),
        total = lots.Length, page = 1, pageSize = 200, contractVersion = "v1"
    });

    [Fact]
    public async Task C_the_ledger_shows_lot_numbers_with_one_TRACE_read_per_SKU_and_an_unresolved_lot_keeps_its_id_with_a_hint()
    {
        var gateway = new ContractGateway()
            .OnContract("/api/inventory/balance", WebContractSchemas.Inventory, WebContractSchemas.BalancePage, Balance((SkuA, LotA1), (SkuA, LotA2), (SkuB, LotB1)))
            .OnContract($"/api/trace/lots?skuId={SkuA}", WebContractSchemas.Trace, WebContractSchemas.LotList, LotsOf(SkuA, (LotA1, "L-2026-001"), (LotA2, "L-2026-002")))
            .OnContract($"/api/trace/lots?skuId={SkuB}", WebContractSchemas.Trace, WebContractSchemas.LotList, LotsOf(SkuB)); // B1 not in its SKU's list
        var rows = Body(await Ledger(gateway, ["inventory.stock.read", "trace.lots.read"]).List(CancellationToken.None)).GetProperty("data").GetProperty("items")
            .EnumerateArray().ToDictionary(r => r.GetProperty("lotId").GetString()!);

        Assert.Equal("L-2026-001", rows[LotA1].GetProperty("lotNumber").GetString());
        Assert.Equal("L-2026-002", rows[LotA2].GetProperty("lotNumber").GetString());
        Assert.False(rows[LotA1].GetProperty("lotUnresolved").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rows[LotB1].GetProperty("lotNumber").ValueKind);
        Assert.True(rows[LotB1].GetProperty("lotUnresolved").GetBoolean());

        var traceCalls = gateway.Requests.Where(r => r.PathAndQuery.StartsWith("/api/trace/lots", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, traceCalls.Count); // two SKUs on the page → two reads, not one per lot
        Assert.All(traceCalls, c => Assert.Contains("&skuLevel=Lsku&page=1&pageSize=200", c.PathAndQuery, StringComparison.Ordinal));
    }

    [Fact]
    public async Task C_without_trace_lots_read_the_ledger_asks_TRACE_nothing_and_marks_the_lot_unresolved()
    {
        var gateway = new ContractGateway()
            .OnContract("/api/inventory/balance", WebContractSchemas.Inventory, WebContractSchemas.BalancePage, Balance((SkuA, LotA1)));
        var row = Body(await Ledger(gateway, ["inventory.stock.read"]).List(CancellationToken.None)).GetProperty("data").GetProperty("items")[0];
        Assert.Equal(LotA1, row.GetProperty("lotId").GetString());
        Assert.True(row.GetProperty("lotUnresolved").GetBoolean());
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/trace", StringComparison.Ordinal));
    }

    [Fact]
    public void C_the_ledger_lot_cell_draws_the_number_and_the_unresolved_hint_in_seven_languages()
    {
        var web = TraceViewRenderer.WebRoot;
        var list = File.ReadAllText(Path.Combine(web, "wwwroot", "assets", "js", "Inventory", "StockLedger", "index.js"));
        Assert.Contains("{ targets: 3, render: (data, type, full) => lotCell(data, full) },", list, StringComparison.Ordinal);
        Assert.Contains("full.lotNumber", list, StringComparison.Ordinal);
        Assert.Contains("L().LotUnresolvedHint", list, StringComparison.Ordinal);
        Assert.Contains("'LotUnresolvedHint'", File.ReadAllText(Path.Combine(web, "wwwroot", "assets", "js", "Inventory", "StockLedger", "index.l10n.js")), StringComparison.Ordinal);
        Assert.Contains("LotUnresolvedHint = Localizer[\"LotUnresolvedHint\"].Value", File.ReadAllText(Path.Combine(web, "Views", "Inventory", "StockLedger", "_IndexL10n.cshtml")), StringComparison.Ordinal);
        foreach (var lang in new[] { "ar", "en", "es", "fr", "ru", "tr", "zh" })
        {
            var value = System.Xml.Linq.XDocument.Load(Path.Combine(web, "Resources", "Views", "Inventory", "StockLedger", $"StockLedgerIndex.{lang}.resx")).Root!
                .Elements("data").SingleOrDefault(d => (string)d.Attribute("name")! == "LotUnresolvedHint")?.Element("value")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(value), $"{lang}: LotUnresolvedHint");
        }
    }

    // ── (d) the contract check is real ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void D_a_fake_location_row_with_id_instead_of_locationId_is_refused_by_the_contract()
    {
        var wrong = """{"items":[{"id":"11111111-0000-4000-8000-000000000001","code":"D01","name":"İstanbul Merkez Depo","type":"Warehouse"}],"total":1}""";
        var refused = Assert.Throws<ContractViolationException>(() => new ContractGateway()
            .OnContract("/api/locations?type=Warehouse", WebContractSchemas.Location, WebContractSchemas.LocationList, wrong));
        Assert.Contains(refused.Errors, e => e.StartsWith("/items/0/id:", StringComparison.Ordinal)); // a member the contract does not name
        // and the shape the service really sends passes
        Assert.Empty(WebContractSchemas.Location.Validate(WebContractSchemas.LocationList, WarehouseList));
    }

    [Fact]
    public void D_a_fake_lot_row_with_id_instead_of_lotId_is_refused_by_the_contract()
    {
        var wrong = """{"items":[{"id":"55555555-0000-4000-8000-0000000000a1","lotNumber":"L-2026-001"}],"total":1}""";
        Assert.Throws<ContractViolationException>(() => new ContractGateway().OnContract("/api/trace/lots", WebContractSchemas.Trace, WebContractSchemas.LotList, wrong));
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Body(IActionResult result) => result switch
    {
        ContentResult content => JsonDocument.Parse(content.Content!).RootElement,
        JsonResult json => JsonDocument.Parse(JsonSerializer.Serialize(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement,
        _ => throw new Xunit.Sdk.XunitException("unexpected result " + result.GetType().Name)
    };

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ControllerContext Context(params (string Key, string Value)[] query)
    {
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()), new("sub", Guid.NewGuid().ToString()), new("legal_entity_id", Company) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers.Cookie = "access_token=caller-token";
        http.Request.QueryString = QueryString.Create(query.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)));
        return new ControllerContext { HttpContext = http };
    }

    private static ExpiryController Expiry(ContractGateway gateway, params (string Key, string Value)[] query)
    {
        var controller = new ExpiryController(new Factory(gateway), new HttpClient(gateway), Configuration(),
            new Permissions(["expiry.dashboard.read", "expiry.fefo-order.read"]), new KeyLocalizer<Diten.Web.Views.Expiry.Expiry>(), NullLogger<ExpiryController>.Instance);
        controller.ControllerContext = Context(query);
        return controller;
    }

    private static QuarantineController Quarantine(ContractGateway gateway)
    {
        var controller = new QuarantineController(new Factory(gateway), new HttpClient(gateway), Configuration(),
            new Permissions(["quarantine.holds.read", "quarantine.settings.manage"]), new KeyLocalizer<Diten.Web.Views.Quarantine.Quarantine>(), NullLogger<QuarantineController>.Instance);
        controller.ControllerContext = Context();
        return controller;
    }

    private static StockLedgerController Ledger(ContractGateway gateway, string[] permissions)
    {
        var controller = new StockLedgerController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<StockLedgerIndex>(), NullLogger<StockLedgerController>.Instance);
        controller.ControllerContext = Context();
        return controller;
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
