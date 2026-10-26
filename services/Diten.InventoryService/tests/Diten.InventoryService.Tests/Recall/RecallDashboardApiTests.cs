using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R6 — getDashboard (pack §8.9) on the real host, every answer checked against RECALL-BUNDLE 1.5.0: each KPI equals its formula on
/// seeded data, and EVERY KPI is the caller's company only (another company's case, return or movement is never counted — R6 sabotage (b)).
/// The drill KPIs (last drill, chart, frequency) are <see cref="RecallDashboardDrillTests"/> (they need the server clock).
/// </summary>
public sealed class RecallDashboardApiTests : IClassFixture<RecallApiFactory>
{
    private static readonly string[] PanelKeys = ["recall.dashboard.read"];

    private readonly RecallApiFactory _factory;

    public RecallDashboardApiTests(RecallApiFactory factory) => _factory = factory;

    private static Task<JsonElement> PanelAsync(RecallScenario s, string token) => s.ExpectAsync(HttpMethod.Get, "/api/recall/dashboard", token, null, 200);

    private static int ByClass(JsonElement panel, string @class) => panel.GetProperty("openCases").GetProperty("byClass").GetProperty(@class).GetInt32();

    [Fact]
    public async Task Open_real_cases_are_counted_by_class_in_the_callers_company_only()
    {
        var s = new RecallScenario(_factory);
        var c1 = await s.OpenAsync(await s.LotAsync(), @class: 1);
        var c2a = await s.OpenAsync(await s.LotAsync(), @class: 2);
        var c2b = await s.OpenAsync(await s.LotAsync(), @class: 2);
        var c3 = await s.OpenAsync(await s.LotAsync(), @class: 3);
        var (cn, cancelled, _) = await s.CommandAsync(RecallScenario.Id(c3), "cancel", s.Qa1Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("cn"), ["reason"] = "Yanlis lot ile acildi, iptal" }, RecallScenario.ETag(c3));
        Assert.True(cn == 200, cancelled.ToString());
        var (closedId, _) = await RecallR3.ApproveAsync(s, await s.LotAsync());
        await RecallR3.CloseAsync(s, closedId); // Closed — not open

        // another company of the SAME tenant opens a class-1 case and a class-3 case
        var other = Guid.NewGuid();
        var o1 = RecallScenario.Open(await s.LotAsync(), 1);
        Assert.Equal(201, (await s.SendAsync(HttpMethod.Post, "/api/recall/cases", s.Mint(s.Qa1, RecallScenario.QaKeys, other), o1, (string)o1["idempotencyKey"]!)).Status);
        var o3 = RecallScenario.Open(await s.LotAsync(), 3);
        Assert.Equal(201, (await s.SendAsync(HttpMethod.Post, "/api/recall/cases", s.Mint(s.Qa1, RecallScenario.QaKeys, other), o3, (string)o3["idempotencyKey"]!)).Status);

        var panel = await PanelAsync(s, s.Mint(s.Management, PanelKeys));
        Assert.Equal(3, panel.GetProperty("openCases").GetProperty("total").GetInt32());
        Assert.Equal((1, 2, 0), (ByClass(panel, "1"), ByClass(panel, "2"), ByClass(panel, "3")));
        Assert.Equal(s.Ledger.LegalEntity.ToString("D"), panel.GetProperty("legalEntityId").GetString());
        var rows = panel.GetProperty("openCaseRows").EnumerateArray().Select(RecallScenario.Id).ToList();
        Assert.Equal(new[] { RecallScenario.Id(c1), RecallScenario.Id(c2a), RecallScenario.Id(c2b) }.Order(), rows.Order());
        Assert.All(panel.GetProperty("openCaseRows").EnumerateArray(), r => Assert.Equal("Real", r.GetProperty("type").GetString()));

        // the other company sees its own two
        var otherPanel = await PanelAsync(s, s.Mint(s.Management, PanelKeys, other));
        Assert.Equal((2, 1, 0, 1), (otherPanel.GetProperty("openCases").GetProperty("total").GetInt32(), ByClass(otherPanel, "1"), ByClass(otherPanel, "2"), ByClass(otherPanel, "3")));
    }

    [Fact]
    public async Task The_panel_needs_recall_dashboard_read_and_no_other_key_opens_it()
    {
        var s = new RecallScenario(_factory);
        var (status, body, _) = await s.SendAsync(HttpMethod.Get, "/api/recall/dashboard", s.Mint(s.Qa1, [.. RecallScenario.QaKeys, "recall.trace.run", "recall.drills.read"]));
        Assert.Equal(403, status);
        Assert.Equal(200, (await s.SendAsync(HttpMethod.Get, "/api/recall/dashboard", s.Mint(s.Management, PanelKeys))).Status);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await s.Client.GetAsync("/api/recall/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Returned_quantity_of_open_cases_is_per_case_per_SKU_and_unit_and_a_closed_case_drops_out()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var customer = ShipmentBundleDouble.ShipTo(w.Shipment);
        var (caseId, approved) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        foreach (var quantity in new[] { "2", "1" })
        {
            var held = await w.Lots.PostOkAsync(w.Lots.Receipt(quantity, w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
            var body = new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("ln"), ["transactionId"] = held, ["customerRef"] = customer };
            Assert.Equal(201, (await w.S.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/return-links", depo, body, (string)body["idempotencyKey"]!)).Status);
        }

        var panel = await PanelAsync(w.S, w.S.Mint(w.S.Management, PanelKeys));
        var returned = Assert.Single(panel.GetProperty("returnedOpenCases").EnumerateArray());
        Assert.Equal(caseId, returned.GetProperty("caseId").GetString());
        Assert.Equal(approved.GetProperty("number").GetString(), returned.GetProperty("number").GetString());
        Assert.Equal(approved.GetProperty("item").GetProperty("skuId").GetString(), returned.GetProperty("skuId").GetString());
        Assert.Equal(3m, returned.GetProperty("quantity").GetDecimal());

        // another company sees none of it
        var other = await PanelAsync(w.S, w.S.Mint(w.S.Management, PanelKeys, Guid.NewGuid()));
        Assert.Empty(other.GetProperty("returnedOpenCases").EnumerateArray());

        await RecallR3.CloseAsync(w.S, caseId);
        Assert.Empty((await PanelAsync(w.S, w.S.Mint(w.S.Management, PanelKeys))).GetProperty("returnedOpenCases").EnumerateArray());
    }

    [Fact]
    public async Task Lot_coverage_and_trace_gaps_count_the_lot_controlled_outbound_of_the_window_in_the_company()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory); // GOODS_ISSUE_PROD of Rm (lot) + GOODS_ISSUE_SALES of Fg (lot)
        var lotless = Guid.NewGuid().ToString("D");
        w.CloneTransaction(w.Shipment, d => { d["LotId"] = BsonNull.Value; d["SourceDocumentId"] = lotless; });                       // in window, no lot
        w.CloneTransaction(w.Shipment, d => { d["LotId"] = BsonNull.Value; d["SourceDocumentId"] = Guid.NewGuid().ToString("D");
            Assert.True(d["PostingDate"].IsBsonDateTime); d["PostingDate"] = new BsonDateTime(DateTime.UtcNow.AddDays(-120)); });                  // outside the window
        w.CloneTransaction(w.Shipment, d => { d["LotId"] = BsonNull.Value; d["SourceDocumentId"] = Guid.NewGuid().ToString("D");
            d["LegalEntityId"] = RecallTraceWorld.SameShape(d["LegalEntityId"], Guid.NewGuid()); });                                 // another company

        var panel = await PanelAsync(w.S, w.S.Mint(w.S.Management, PanelKeys));
        var coverage = panel.GetProperty("lotCoverage");
        Assert.Equal((90, 2, 3), (coverage.GetProperty("windowDays").GetInt32(), coverage.GetProperty("withLot").GetInt32(), coverage.GetProperty("total").GetInt32()));
        Assert.Equal(66.7m, coverage.GetProperty("percent").GetDecimal());
        var gaps = panel.GetProperty("traceGaps");
        Assert.Equal(1, gaps.GetProperty("count").GetInt32());
        var movement = Assert.Single(gaps.GetProperty("movements").EnumerateArray());
        var clone = Assert.Single(w.S.Persisted("inventory_transactions"), t => t.Contains("SourceDocumentId") && t["SourceDocumentId"] == lotless);
        Assert.Equal(clone["TransactionNumber"].AsString, movement.GetProperty("number").GetString());
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2, 3, "66.7")]
    [InlineData(1522, 1524, "99.9")]
    [InlineData(9999, 10000, "99.9")]
    [InlineData(5, 5, "100")]
    public void Coverage_percent_is_one_decimal_and_never_100_while_a_gap_exists(int withLot, int total, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Diten.InventoryService.Domain.Recall.Rules.RecallDashboardRules.CoveragePercent(withLot, total));
}
