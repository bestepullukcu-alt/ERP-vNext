using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F4 — the reads (FEFO-BUNDLE 1.1.0 getDashboard / listApproaching / listExpired / getFefoOrder) through the real HTTP pipeline on a
/// SEEDED data set; every answer is validated against fefo.openapi.yaml (ExpiryContractValidatingHandler). Policy defaults: Yaklaşıyor 90,
/// Kritik 30 days. Lot dates are relative to the host clock's today; postings are dated ten days earlier. The seed creates a lot with a
/// past expiry (L4) and receives it, so the class is in <see cref="Trace.LotExpiryCollection"/>: the lot sweeper of those classes reads
/// every tenant and would expire L4 between its creation and its receipt (LOT_EXPIRED, measured in a full run).
/// </summary>
[Collection(Trace.LotExpiryCollection.Name)]
public sealed class ExpiryReadTests : IClassFixture<ExpiryApiFactory>
{
    private static readonly string[] ReaderKeys = ["expiry.dashboard.read", "expiry.fefo-order.read", "expiry.exceptions.read"];
    private const string Reason = "Musteri talebi: uzun raf omurlu lot istendi";

    private readonly ExpiryApiFactory _factory;

    public ExpiryReadTests(ExpiryApiFactory factory) => _factory = factory;

    private DateOnly Today => DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime);

    private string Day(int offset) => Today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z";

    private string Posting => Today.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T10:00:00Z";

    /// <summary>Now − 5 min: L4 (−5) is already expired on this day, so it is excluded from the FEFO order of the seeded issues.</summary>
    private string IssueDate => _factory.Clock.GetUtcNow().AddMinutes(-5).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static Task<JsonElement> GetAsync(ExpiryScenario s, string path, string token, int expected = 200)
        => s.ExpectAsync(HttpMethod.Get, path, token, null, expected);

    private sealed record Seeded(ExpiryScenario S, string Reader, string L1, string L2, string L3, string L4, string L5, string ExpiredCaseId);

    /// <summary>
    /// L1 expiry +10 (10 AVAILABLE + 4 QUARANTINE), L2 +60 (20), L3 +200 (5), L4 −5 (7, expired), L5 +20 (6 QUARANTINE only); a compliant
    /// issue of 1 from L1 and a reasoned bypass of 1 from L2 (→ FEFO_BYPASS, SLA 24 h); a manual EXPIRED_ON_HAND case on L4 (SLA 120 h); a
    /// manual case on L3 resolved after 2 h. Then the clock moves +25 h → the FEFO_BYPASS case is late.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var s = new ExpiryScenario(_factory);
        var l1 = await s.LotAsync(Day(10));
        var l2 = await s.LotAsync(Day(60));
        var l3 = await s.LotAsync(Day(200));
        var l4 = await s.LotAsync(Day(-5));
        var l5 = await s.LotAsync(Day(20));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", l1, postingDate: Posting));
        await s.Lots.PostOkAsync(s.Lots.Receipt("4", l1, toStatus: "QUARANTINE", postingDate: Posting));
        await s.Lots.PostOkAsync(s.Lots.Receipt("20", l2, postingDate: Posting));
        await s.Lots.PostOkAsync(s.Lots.Receipt("5", l3, postingDate: Posting));
        await s.Lots.PostOkAsync(s.Lots.Receipt("7", l4, postingDate: Posting));
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", l5, toStatus: "QUARANTINE", postingDate: Posting));

        await s.Lots.PostOkAsync(s.Lots.Issue("1", l1, postingDate: IssueDate)); // compliant (L1 is first)
        var bypass = s.Lots.Issue("1", l2, postingDate: IssueDate);
        bypass["fefoBypassReason"] = Reason;
        await s.Lots.PostOkAsync(bypass); // FEFO_BYPASS

        var expiredCase = await s.OpenAsync(l4, "EXPIRED_ON_HAND");
        var resolvedCase = await s.OpenAsync(l3);
        _factory.Clock.Advance(TimeSpan.FromHours(2));
        await s.ResolveSignedAsync(resolvedCase.GetProperty("exceptionId").GetString()!, "AcceptedWithReason", ExpiryScenario.ETag(await s.GetAsync(resolvedCase.GetProperty("exceptionId").GetString()!)));
        _factory.Clock.Advance(TimeSpan.FromHours(23));
        return new Seeded(s, s.Mint(s.Qa, ReaderKeys), l1, l2, l3, l4, l5, expiredCase.GetProperty("exceptionId").GetString()!);
    }

    private static JsonElement Row(JsonElement page, string lotId) => page.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("lotId").GetString() == lotId);

    [Fact]
    public async Task The_panel_KPIs_are_the_pack_8_8_formulas_on_the_seeded_set()
    {
        var w = await SeedAsync();
        try
        {
            var panel = await GetAsync(w.S, "/api/expiry/dashboard", w.Reader);
            var k = panel.GetProperty("kpis");

            // FEFO compliance = compliant ÷ issue snapshots in the period: 1 of 2
            Assert.Equal(2, k.GetProperty("fefoIssues").GetInt32());
            Assert.Equal(1, k.GetProperty("fefoBypasses").GetInt32());
            Assert.Equal(50.0m, k.GetProperty("fefoCompliancePct").GetDecimal());

            // Kritik / Yaklaşıyor: rows with AVAILABLE > 0 and 0 ≤ days ≤ horizon; AVAILABLE and held apart (K3 sabotage (e): add held to available → red)
            var critical = k.GetProperty("critical");
            Assert.Equal(1, critical.GetProperty("lots").GetInt32());            // L1 (L5 has no AVAILABLE)
            Assert.Equal(9m, critical.GetProperty("available").GetDecimal());
            Assert.Equal(4m, critical.GetProperty("held").GetDecimal());
            var approaching = k.GetProperty("approaching");
            Assert.Equal(2, approaching.GetProperty("lots").GetInt32());         // L1 + L2 (includes Kritik)
            Assert.Equal(28m, approaching.GetProperty("available").GetDecimal()); // 9 + 19
            Assert.Equal(4m, approaching.GetProperty("held").GetDecimal());

            // expired on hand: expiry < today, on-hand > 0 in any status
            Assert.Equal(1, k.GetProperty("expiredOnHand").GetProperty("lots").GetInt32());
            Assert.Equal(7m, k.GetProperty("expiredOnHand").GetProperty("quantity").GetDecimal());

            // cases: active = FEFO_BYPASS + EXPIRED_ON_HAND; late = FEFO_BYPASS (24 h passed); mean resolution = 2 h
            Assert.Equal(2, k.GetProperty("openExceptions").GetInt32());
            Assert.Equal(1, k.GetProperty("lateExceptions").GetInt32());
            Assert.Equal(2.0m, k.GetProperty("avgResolutionHours").GetDecimal());

            // 12 months from the current month, non-expired on hand by expiry month, AVAILABLE vs held
            var months = panel.GetProperty("expiryByMonth").EnumerateArray().ToList();
            Assert.Equal(12, months.Count);
            Assert.Equal(Today.ToString("yyyy-MM", CultureInfo.InvariantCulture), months[0].GetProperty("month").GetString());
            Assert.Equal(9m + 19m + 5m, months.Sum(m => m.GetProperty("available").GetDecimal()));
            Assert.Equal(4m + 6m, months.Sum(m => m.GetProperty("held").GetDecimal()));

            // top risk: the nearest non-expired rows within ApproachingDays — L1, L5, L2
            Assert.Equal([w.L1, w.L5, w.L2], panel.GetProperty("topRisk").EnumerateArray().Select(r => r.GetProperty("lotId").GetString()));
            Assert.False(panel.ToString().Contains("value", StringComparison.OrdinalIgnoreCase)); // no money values (D-13)

            // the warehouse filter narrows; another warehouse sees nothing of this one
            var other = await GetAsync(w.S, $"/api/expiry/dashboard?warehouseId={w.S.Ledger.Warehouse2}", w.Reader);
            Assert.Equal(0, other.GetProperty("kpis").GetProperty("approaching").GetProperty("lots").GetInt32());
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }
    }

    [Fact]
    public async Task Another_company_and_another_tenant_see_none_of_it()
    {
        var w = await SeedAsync();
        try
        {
            // K3 sabotage (b): drop the company scope of the panel / list read → the other company sees this company's lots
            var otherCompany = w.S.Mint(w.S.Qa, ReaderKeys, Guid.NewGuid());
            var panel = await GetAsync(w.S, "/api/expiry/dashboard", otherCompany);
            Assert.Equal(0, panel.GetProperty("kpis").GetProperty("approaching").GetProperty("lots").GetInt32());
            Assert.Equal(0, panel.GetProperty("kpis").GetProperty("expiredOnHand").GetProperty("lots").GetInt32());
            Assert.Equal(0, panel.GetProperty("kpis").GetProperty("openExceptions").GetInt32());
            Assert.Empty(panel.GetProperty("topRisk").EnumerateArray());
            Assert.Equal(0, (await GetAsync(w.S, "/api/expiry/lots/approaching", otherCompany)).GetProperty("total").GetInt32());
            Assert.Equal(0, (await GetAsync(w.S, "/api/expiry/lots/expired", otherCompany)).GetProperty("total").GetInt32());

            var otherTenant = w.S.Mint(Guid.NewGuid(), ReaderKeys, Guid.NewGuid(), Guid.NewGuid());
            Assert.Equal(0, (await GetAsync(w.S, "/api/expiry/lots/approaching", otherTenant)).GetProperty("total").GetInt32());

            // keys: the panel and lists need expiry.dashboard.read, the order expiry.fefo-order.read
            Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, "/api/expiry/dashboard", w.S.Mint(w.S.Qa, ["expiry.fefo-order.read"]))).Status);
            Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, "/api/expiry/lots/expired", w.S.Mint(w.S.Qa, ["expiry.fefo-order.read"]))).Status);
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }
    }

    [Fact]
    public async Task Approaching_and_expired_lists_follow_the_company_horizons()
    {
        var w = await SeedAsync();
        try
        {
            var all = await GetAsync(w.S, "/api/expiry/lots/approaching", w.Reader);
            Assert.Equal([w.L1, w.L5, w.L2], all.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("lotId").GetString()));
            var l1 = Row(all, w.L1);
            Assert.Equal("Critical", l1.GetProperty("severity").GetString());
            Assert.Equal(9m, l1.GetProperty("onHand").GetProperty("available").GetDecimal());
            Assert.Equal(4m, l1.GetProperty("onHand").GetProperty("held").GetDecimal());
            Assert.Equal("EA", l1.GetProperty("onHand").GetProperty("baseUomId").GetString());
            Assert.Contains("QUARANTINED", l1.GetProperty("heldReasons").EnumerateArray().Select(r => r.GetString()));
            Assert.Equal(w.S.Ledger.Warehouse, l1.GetProperty("warehouse").GetProperty("id").GetString());
            Assert.Equal("Approaching", Row(all, w.L2).GetProperty("severity").GetString());

            Assert.Equal(2, (await GetAsync(w.S, "/api/expiry/lots/approaching?includeHeld=false", w.Reader)).GetProperty("total").GetInt32());
            Assert.Equal(2, (await GetAsync(w.S, "/api/expiry/lots/approaching?severity=Critical", w.Reader)).GetProperty("total").GetInt32());
            // KF-A: a horizon beyond the company's ApproachingDays (90) is capped — L3 (+200) never appears
            Assert.Equal(3, (await GetAsync(w.S, "/api/expiry/lots/approaching?horizonDays=500", w.Reader)).GetProperty("total").GetInt32());
            Assert.Equal(1, (await GetAsync(w.S, "/api/expiry/lots/approaching?horizonDays=15", w.Reader)).GetProperty("total").GetInt32());
            var lot2Number = Row(all, w.L2).GetProperty("lotNumber").GetString()!;
            Assert.Equal(w.L2, Assert.Single((await GetAsync(w.S, $"/api/expiry/lots/approaching?q={lot2Number}", w.Reader)).GetProperty("items").EnumerateArray()).GetProperty("lotId").GetString());
            Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, "/api/expiry/lots/approaching?pageSize=201", w.Reader)).Status);
            Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, "/api/expiry/lots/approaching?severity=Expired", w.Reader)).Status);

            var expired = await GetAsync(w.S, "/api/expiry/lots/expired", w.Reader);
            var l4 = Assert.Single(expired.GetProperty("items").EnumerateArray());
            Assert.Equal(w.L4, l4.GetProperty("lotId").GetString());
            Assert.Equal("Expired", l4.GetProperty("severity").GetString());
            Assert.True(l4.GetProperty("daysSinceExpiry").GetInt32() >= 5); // "Dolduğundan beri"
            Assert.False(l4.TryGetProperty("daysToExpiry", out _));
            Assert.Equal(w.ExpiredCaseId, l4.GetProperty("openExceptionId").GetString());
            Assert.Equal(0, (await GetAsync(w.S, "/api/expiry/lots/expired?minDaysSinceExpiry=60", w.Reader)).GetProperty("total").GetInt32());
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }
    }

    [Fact]
    public async Task The_FEFO_order_gives_the_same_first_lot_as_the_F2_issue_check()
    {
        // A expires first but was made LAST; B and C later; D (earliest) is blocked → excluded with its reason.
        var s = new ExpiryScenario(_factory);
        var reader = s.Mint(s.Qa, ReaderKeys);
        async Task<string> Lot(int expiry, string mfg)
        {
            var lot = await s.Lots.CreateLotAsync(expiryDate: Day(expiry), manufacturingDate: mfg + "T00:00:00Z");
            await s.Lots.ReleaseAsync(lot);
            await s.Lots.PostOkAsync(s.Lots.Receipt("10", lot, postingDate: Posting));
            return lot;
        }

        var a = await Lot(100, "2026-05-01");
        var b = await Lot(150, "2026-01-01");
        var c = await Lot(200, "2026-02-01");
        var d = await Lot(50, "2026-03-01");
        await s.Lots.BlockAsync(d);

        var refused = await s.Lots.PostAsync(s.Lots.Issue("1", c, postingDate: Posting), 422);
        var firstByIssueCheck = refused.GetProperty("error").GetProperty("details").GetProperty("firstEligibleLotId").GetString();

        var asOf = Posting[..10];
        var query = $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}&itemId={s.Ledger.Item}&skuId={s.Ledger.Sku}&skuLevel=Lsku&asOf={asOf}";
        var order = await GetAsync(s, query, reader);
        var eligible = order.GetProperty("eligible").EnumerateArray().ToList();

        // K3 sabotage (c): order by a secondary key first (manufacturing date) → B first → red
        Assert.Equal(firstByIssueCheck, eligible[0].GetProperty("lotId").GetString());
        Assert.Equal([a, b, c], eligible.Select(e => e.GetProperty("lotId").GetString()));
        Assert.Equal([1, 2, 3], eligible.Select(e => e.GetProperty("rank").GetInt32()));
        var excluded = Assert.Single(order.GetProperty("excluded").EnumerateArray());
        Assert.Equal(d, excluded.GetProperty("lotId").GetString());
        Assert.Equal("BLOCKED", excluded.GetProperty("reason").GetString());
        Assert.Equal("onHandAvailable", order.GetProperty("basis").GetString());
        Assert.Equal(30m, order.GetProperty("skuAvailable").GetDecimal()); // the blocked lot's AVAILABLE rows are not free (getAvailability)

        var allocated = await GetAsync(s, query + "&quantity=15", reader);
        Assert.Equal([10m, 5m, 0m], allocated.GetProperty("eligible").EnumerateArray().Select(e => e.GetProperty("allocated").GetDecimal()));
        Assert.Equal(15m, allocated.GetProperty("allocatedQuantity").GetDecimal());
        Assert.Equal(0m, allocated.GetProperty("shortfall").GetDecimal());
        Assert.Equal(10m, (await GetAsync(s, query + "&quantity=40", reader)).GetProperty("shortfall").GetDecimal());

        // read-only (KF-F): nothing is reserved
        Assert.Empty(s.Persisted("inventory_reservations"));
        Assert.Equal(404, (await s.SendAsync(HttpMethod.Get, $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}&itemId={s.Ledger.Item}&skuId={Guid.NewGuid()}&skuLevel=Lsku", reader)).Status);
        Assert.Equal(400, (await s.SendAsync(HttpMethod.Get, $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}", reader)).Status);
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Get, query, s.Mint(s.Qa, ["expiry.dashboard.read"]))).Status);
    }

    [Fact]
    public async Task A_manual_case_carries_its_unit_FU_0176_F1_UOM()
    {
        var s = new ExpiryScenario(_factory);
        var stocked = await s.LotAsync(Day(40));
        await s.Lots.PostOkAsync(s.Lots.Receipt("3", stocked, postingDate: Posting));
        var empty = await s.LotAsync(Day(40));

        var given = ExpiryScenario.Open(stocked, s.Ledger.Warehouse, "FEFO_BYPASS", quantity: 2m);
        given["baseUomId"] = "BOX";
        var explicitUnit = await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, given, 201, (string)given["idempotencyKey"]!);
        Assert.Equal("BOX", explicitUnit.GetProperty("baseUomId").GetString());

        var fromStock = await s.OpenAsync(stocked, "BELOW_MIN_REMAINING");
        Assert.Equal("EA", fromStock.GetProperty("baseUomId").GetString()); // read from the lot's stock

        var none = await s.OpenAsync(empty);
        Assert.False(none.TryGetProperty("baseUomId", out _)); // no stock → no unit ("birim yok" on screen)

        var replay = await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, given, 200, (string)given["idempotencyKey"]!);
        Assert.Equal(explicitUnit.GetProperty("exceptionId").GetString(), replay.GetProperty("exceptionId").GetString());
    }
}
