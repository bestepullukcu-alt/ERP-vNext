using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC7 / pack §4 bucket rule and §16 StockStatus box.</summary>
public sealed class StockStatusTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public StockStatusTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Receipt_into_QUALITY_INSPECTION_is_on_hand_but_not_available()
    {
        var s = new InventoryScenario(_factory);

        var posted = await s.PostExpectingAsync(s.Receipt("100", "GOODS_RECEIPT_PO", "QUALITY_INSPECTION"), 201);

        var snapshot = posted.GetProperty("balanceSnapshot");
        Assert.Equal("QUALITY_INSPECTION", snapshot.GetProperty("stockStatus").GetString());
        Assert.Equal("100.000", snapshot.GetProperty("onHand").GetString());
        Assert.Equal("0.000", snapshot.GetProperty("available").GetString());
        Assert.Equal("100.000", snapshot.GetProperty("qualityInspection").GetString());

        var availability = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?itemId={s.Item}"));
        Assert.Equal("100.000", availability.GetProperty("onHand").GetString());
        Assert.Equal("0.000", availability.GetProperty("available").GetString());
        Assert.Equal("100.000", availability.GetProperty("byStatus").GetProperty("QUALITY_INSPECTION").GetString());
        Assert.Equal("0.000", availability.GetProperty("byStatus").GetProperty("AVAILABLE").GetString());
    }

    [Fact]
    public async Task STATUS_TRANSFER_QI_to_AVAILABLE_moves_the_quantity_between_rows()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100", "GOODS_RECEIPT_PO", "QUALITY_INSPECTION"), 201);

        var release = s.Movement("STATUS_TRANSFER", "100");
        release["fromWarehouseId"] = s.Warehouse;
        release["fromLocationId"] = s.LocationA;
        release["toWarehouseId"] = s.Warehouse;
        release["toLocationId"] = s.LocationA;
        release["fromStockStatus"] = "QUALITY_INSPECTION";
        release["toStockStatus"] = "AVAILABLE";
        await s.PostExpectingAsync(release, 201);

        var availability = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?itemId={s.Item}"));
        Assert.Equal("100.000", availability.GetProperty("onHand").GetString());
        Assert.Equal("100.000", availability.GetProperty("available").GetString());
        Assert.Equal("0.000", availability.GetProperty("byStatus").GetProperty("QUALITY_INSPECTION").GetString());
        Assert.Equal("100.000", availability.GetProperty("byStatus").GetProperty("AVAILABLE").GetString());
    }

    [Fact]
    public async Task ByStatus_reports_every_status_and_each_bucket_is_counted_where_the_rule_says()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("10", "GOODS_RECEIPT_PO", "AVAILABLE"), 201);
        await s.PostExpectingAsync(s.Receipt("20", "GOODS_RECEIPT_PO", "QUALITY_INSPECTION"), 201);
        await s.PostExpectingAsync(s.Receipt("30", "GOODS_RECEIPT_PO", "QUARANTINE"), 201);
        await s.PostExpectingAsync(s.Receipt("40", "GOODS_RECEIPT_PO", "BLOCKED"), 201);
        await s.PostExpectingAsync(s.Receipt("50", "GOODS_RECEIPT_PO", "IN_TRANSIT"), 201);

        var availability = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?itemId={s.Item}"));
        var byStatus = availability.GetProperty("byStatus");
        Assert.Equal("10.000", byStatus.GetProperty("AVAILABLE").GetString());
        Assert.Equal("20.000", byStatus.GetProperty("QUALITY_INSPECTION").GetString());
        Assert.Equal("30.000", byStatus.GetProperty("QUARANTINE").GetString());
        Assert.Equal("40.000", byStatus.GetProperty("BLOCKED").GetString());
        Assert.Equal("50.000", byStatus.GetProperty("IN_TRANSIT").GetString());
        Assert.Equal("0.000", byStatus.GetProperty("DAMAGED").GetString());
        Assert.Equal("0.000", byStatus.GetProperty("EXPIRED").GetString());
        Assert.Equal("150.000", availability.GetProperty("onHand").GetString());
        Assert.Equal("10.000", availability.GetProperty("available").GetString());

        var balance = await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?itemId={s.Item}&stockStatus=QUARANTINE"));
        var quarantine = Assert.Single(balance.GetProperty("rows").EnumerateArray().ToList());
        Assert.Equal("30.000", quarantine.GetProperty("blocked").GetString());
        Assert.Equal("0.000", quarantine.GetProperty("available").GetString());

        var transit = Assert.Single((await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?itemId={s.Item}&stockStatus=IN_TRANSIT")))
            .GetProperty("rows").EnumerateArray().ToList());
        Assert.Equal("50.000", transit.GetProperty("inTransit").GetString());
    }

    [Fact]
    public async Task Issuing_AVAILABLE_stock_cannot_consume_quality_inspection_stock()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100", "GOODS_RECEIPT_PO", "QUALITY_INSPECTION"), 201);

        var refused = await s.PostExpectingAsync(s.Issue("1"), 422);

        Assert.Equal("INSUFFICIENT_STOCK", refused.GetProperty("error").GetProperty("code").GetString());
    }
}
