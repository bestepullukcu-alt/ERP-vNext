using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §16 Q1 #1 / §13 #23 — intake: an inbound ledger movement into QUARANTINE / QUALITY_INSPECTION creates
/// exactly ONE hold (post-commit observer), and the sweep never duplicates it; it re-creates a hold the observer missed,
/// once. Inbound into BLOCKED → one blocked-stock record (origin OtherInbound); into DAMAGED / AVAILABLE → nothing.
/// </summary>
public sealed class QuarantineIntakeTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineIntakeTests(QuarantineApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("QUARANTINE", "GOODS_RECEIPT_PO", "GoodsReceipt")]
    [InlineData("QUALITY_INSPECTION", "GOODS_RECEIPT_PO", "GoodsReceipt")]
    [InlineData("QUARANTINE", "CUSTOMER_RETURN", "CustomerReturn")]
    [InlineData("QUARANTINE", "GOODS_RECEIPT_PROD", "Production")]
    [InlineData("QUARANTINE", "OPENING_BALANCE", "OtherInbound")]
    public async Task An_inbound_movement_into_quarantine_creates_exactly_one_hold(string status, string movementType, string source)
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var tx = await s.ReceiveAsync("1200", lot, status, movementType);

        var hold = await s.HoldBySourceAsync(tx);
        Assert.Equal(status, hold.GetProperty("stockStatus").GetString());
        Assert.Equal(1200m, QuarantineScenario.D(hold.GetProperty("quantity")));
        Assert.Equal(source, hold.GetProperty("source").GetString());
        Assert.Equal("Open", hold.GetProperty("status").GetString());
        Assert.Equal("Waiting", hold.GetProperty("reviewState").GetString());
        Assert.Equal(lot, hold.GetProperty("lot").GetProperty("lotId").GetString());
        Assert.Matches("^KRN-\\d{4}-\\d{4,}$", hold.GetProperty("holdNumber").GetString()!);
        Assert.Contains(hold.GetProperty("history").EnumerateArray(), h => h.GetProperty("action").GetString() == "Quarantined");
        Assert.Contains(hold.GetProperty("movements").EnumerateArray(), m => m.GetProperty("transactionId").GetString() == tx);

        // the sweep finds the observer's hold and creates nothing
        Assert.Equal(0, await _factory.SweepAsync(s.Tenant));
        Assert.Single(s.Holds());
    }

    [Fact]
    public async Task The_sweep_recreates_a_hold_the_observer_missed_exactly_once()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var tx = await s.ReceiveAsync("300", lot, "QUARANTINE");
        Assert.Single(s.Holds());

        // simulate "crash between commit and observer": the hold never got written
        _factory.Collection("quarantine_holds").DeleteMany(new BsonDocument("TenantId", s.Tenant.ToString()));
        Assert.Empty(s.Holds());

        Assert.Equal(1, await _factory.SweepAsync(s.Tenant));
        Assert.Equal(0, await _factory.SweepAsync(s.Tenant));
        var hold = Assert.Single(s.Holds());
        Assert.Equal(tx, hold["SourceMovementId"].AsString);
    }

    [Fact]
    public async Task Inbound_into_blocked_is_a_blocked_record_and_damaged_or_available_creates_nothing()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var blockedTx = await s.ReceiveAsync("40", lot, "BLOCKED");
        await s.ReceiveAsync("10", lot, "DAMAGED");
        await s.ReceiveAsync("10", lot, "AVAILABLE");

        Assert.Empty(s.Holds());
        var blocked = Assert.Single(s.Blocked());
        Assert.Equal("OtherInbound", blocked["Origin"].AsString);
        Assert.Equal(blockedTx, blocked["SourceMovementId"].AsString);
        Assert.Equal(0, await _factory.SweepAsync(s.Tenant));
        Assert.Single(s.Blocked());
    }

    [Fact]
    public async Task A_movement_mod_0175_posts_itself_is_never_taken_in_twice()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("100", lot, "AVAILABLE");
        var key = QuarantineScenario.Key("hold");
        await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new
        {
            mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key,
            buckets = new[] { new { itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Lsku", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA, quantity = 30m, lotId = lot } }
        }, 201, key);

        Assert.Single(s.Holds()); // the manual hold — the observer skipped the MOD-0175 STATUS_TRANSFER
        Assert.Equal(0, await _factory.SweepAsync(s.Tenant));
        Assert.Single(s.Holds());
    }
}
