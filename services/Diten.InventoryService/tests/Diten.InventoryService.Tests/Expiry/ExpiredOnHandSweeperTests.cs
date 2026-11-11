using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F1 — pack §8.5 / §13 #11: the expired-on-hand sweeper (driven directly, one tenant; the hosted one is off in tests) opens ONE
/// EXPIRED_ON_HAND case per (company, warehouse, lot) with stock on hand past the company's threshold, as a system case, and records one
/// SlaBreached event per overdue case. Real ledger stock (GOODS_RECEIPT_PO through the API), real lots.
/// </summary>
public sealed class ExpiredOnHandSweeperTests : IClassFixture<ExpiryApiFactory>
{
    private static readonly DateTimeOffset Expiry = new(2027, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly ExpiryApiFactory _factory;

    public ExpiredOnHandSweeperTests(ExpiryApiFactory factory) => _factory = factory;

    private async Task<(ExpiryScenario S, string Lot)> StockAsync(string quantity = "10")
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync("2027-01-31T00:00:00Z");
        await s.Lots.PostOkAsync(s.Lots.Receipt(quantity, lot));
        return (s, lot);
    }

    [Fact]
    public async Task One_system_case_per_lot_and_warehouse_and_a_second_run_opens_nothing()
    {
        var (s, lot) = await StockAsync();
        var now = Expiry.AddDays(29); // 29 days after expiry > default threshold 14

        var first = await _factory.SweepAsync(s.Tenant, now);
        Assert.Equal(1, first.Opened);
        var second = await _factory.SweepAsync(s.Tenant, now);
        Assert.Equal(0, second.Opened);

        var stored = Assert.Single(s.Persisted("expiry_exceptions"));
        Assert.Equal("ExpiredOnHand", stored["Type"].AsString);
        Assert.Equal("System", stored["Origin"].AsString);
        Assert.Equal("Open", stored["Status"].AsString);
        Assert.Equal(lot, stored["LotId"].AsString);
        Assert.Equal(s.Ledger.Warehouse, stored["WarehouseId"].AsString);
        Assert.Equal(s.Ledger.LegalEntity.ToString(), stored["LegalEntityId"].AsString);
        Assert.Equal(10m, stored["Quantity"].ToDecimal());
        Assert.True(stored["OriginUserId"].IsBsonNull); // CT-F10: a system case has no origin user
        Assert.Equal("29", stored["Facts"]["daysSinceExpiry"].AsString);
        Assert.Equal($"EXPIRED_ON_HAND:{lot}:{s.Ledger.Warehouse}", stored["DedupKey"].AsString);

        var detail = await s.GetAsync(stored["_id"].AsString);
        Assert.Equal("EXPIRED_ON_HAND", detail.GetProperty("type").GetString());
        Assert.False(detail.TryGetProperty("openedBy", out _));
        Assert.Equal(["Opened"], detail.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("action").GetString()));
        var audit = Assert.Single(s.Audit("expiry.exception.opened"));
        Assert.True(audit["UserId"].IsBsonNull);
    }

    [Fact]
    public async Task The_company_threshold_decides_and_no_stock_means_no_case()
    {
        var (s, _) = await StockAsync();
        Assert.Equal(0, (await _factory.SweepAsync(s.Tenant, Expiry.AddDays(10))).Opened); // 10 < 14 (default)

        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(threshold: 40), "\"0\"")).Status);
        Assert.Equal(0, (await _factory.SweepAsync(s.Tenant, Expiry.AddDays(29))).Opened); // 29 < 40 (the company's)
        Assert.Equal(1, (await _factory.SweepAsync(s.Tenant, Expiry.AddDays(48))).Opened);

        var (empty, lot) = await StockAsync("5");
        await empty.Lots.PostOkAsync(empty.Lots.Issue("5", lot)); // all issued: nothing on hand
        Assert.Equal(0, (await _factory.SweepAsync(empty.Tenant, Expiry.AddDays(60))).Opened);
        Assert.Empty(empty.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task An_overdue_case_gets_one_SlaBreached_event()
    {
        var (s, _) = await StockAsync();
        var opened = Expiry.AddDays(29);
        Assert.Equal(1, (await _factory.SweepAsync(s.Tenant, opened)).Opened);
        Assert.Equal(0, (await _factory.SweepAsync(s.Tenant, opened.AddHours(119))).SlaBreached); // EXPIRED_ON_HAND SLA 120 h
        Assert.Equal(1, (await _factory.SweepAsync(s.Tenant, opened.AddHours(121))).SlaBreached);
        Assert.Equal(0, (await _factory.SweepAsync(s.Tenant, opened.AddHours(200))).SlaBreached);

        var events = s.Persisted("expiry_exception_events");
        Assert.Single(events, e => e["Action"].AsString == "SlaBreached");
        Assert.Single(s.Audit("expiry.exception.sla-breached"));
    }

    [Fact]
    public async Task A_closed_case_frees_the_key_and_the_next_run_opens_a_new_one()
    {
        var (s, _) = await StockAsync();
        var now = Expiry.AddDays(29);
        Assert.Equal(1, (await _factory.SweepAsync(s.Tenant, now)).Opened);
        var id = Assert.Single(s.Persisted("expiry_exceptions"))["_id"].AsString;
        var current = await s.GetAsync(id);
        var resolved = await s.ResolveSignedAsync(id, "Fixed", ExpiryScenario.ETag(current)); // a system case: any QA user may resolve it
        Assert.Equal("Closed", (await s.CloseAsync(id, ExpiryScenario.ETag(resolved))).Body.GetProperty("status").GetString());

        Assert.Equal(1, (await _factory.SweepAsync(s.Tenant, now)).Opened); // still on hand → a new case
        Assert.Equal(2, s.Persisted("expiry_exceptions").Count);
    }
}
