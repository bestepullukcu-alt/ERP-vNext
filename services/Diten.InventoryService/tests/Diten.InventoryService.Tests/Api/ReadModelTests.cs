using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>getBalance / getTransactions / getAvailability filters, paging and ordering.</summary>
public sealed class ReadModelTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReadModelTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Transactions_are_paged_newest_posting_first_with_total()
    {
        var s = new InventoryScenario(_factory);
        for (var day = 1; day <= 5; day++)
        {
            var body = s.Receipt("1");
            body["postingDate"] = $"2026-10-0{day}T08:00:00Z";
            await s.PostExpectingAsync(body, 201);
        }

        var page1 = await s.ReadAsync(await s.GetAsync("/api/inventory/transactions?pageSize=2"));
        var page3 = await s.ReadAsync(await s.GetAsync("/api/inventory/transactions?pageSize=2&page=3"));

        Assert.Equal(5, page1.GetProperty("total").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        var dates = page1.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("postingDate").GetString()).ToList();
        Assert.Equal(["2026-10-05T08:00:00.000Z", "2026-10-04T08:00:00.000Z"], dates);
        Assert.Single(page3.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Transactions_from_to_filter_posting_date_by_inclusive_utc_day()
    {
        var s = new InventoryScenario(_factory);
        foreach (var date in new[] { "2026-10-01T23:59:59Z", "2026-10-02T00:00:00Z", "2026-10-03T12:00:00Z", "2026-10-04T00:00:00Z" })
        {
            var body = s.Receipt("1");
            body["postingDate"] = date;
            await s.PostExpectingAsync(body, 201);
        }

        var json = await s.ReadAsync(await s.GetAsync("/api/inventory/transactions?from=2026-10-02&to=2026-10-03"));

        Assert.Equal(2, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Transactions_location_filter_matches_either_side()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("10"), 201);
        var move = s.Movement("LOCATION_TRANSFER", "3");
        move["fromWarehouseId"] = s.Warehouse;
        move["fromLocationId"] = s.LocationA;
        move["toWarehouseId"] = s.Warehouse;
        move["toLocationId"] = s.LocationB;
        await s.PostExpectingAsync(move, 201);

        var atA = await s.ReadAsync(await s.GetAsync($"/api/inventory/transactions?locationId={s.LocationA}"));
        var atB = await s.ReadAsync(await s.GetAsync($"/api/inventory/transactions?locationId={s.LocationB}"));

        Assert.Equal(2, atA.GetProperty("total").GetInt32());
        Assert.Equal(1, atB.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Balance_filters_by_warehouse_and_lot()
    {
        var s = new InventoryScenario(_factory, lotControlled: true);
        await s.PostExpectingAsync(s.Receipt("5"), 201);
        var otherLot = s.Receipt("7", location: s.LocationC);
        otherLot["lotId"] = "lot-2";
        await s.PostExpectingAsync(otherLot, 201);

        var byWarehouse = await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?warehouseId={s.Warehouse2}"));
        var byLot = await s.ReadAsync(await s.GetAsync("/api/inventory/balance?lotId=lot-1"));
        var all = await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?itemId={s.Item}"));

        Assert.Equal("7.000", Assert.Single(byWarehouse.GetProperty("rows").EnumerateArray().ToList()).GetProperty("onHand").GetString());
        Assert.Equal("5.000", Assert.Single(byLot.GetProperty("rows").EnumerateArray().ToList()).GetProperty("onHand").GetString());
        Assert.Equal(2, all.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Availability_can_be_scoped_to_a_warehouse_and_echoes_the_scope()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("5"), 201);
        await s.PostExpectingAsync(s.Receipt("7", location: s.LocationC), 201);

        var json = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?skuId={s.Sku}&warehouseId={s.Warehouse2}"));

        Assert.Equal("7.000", json.GetProperty("onHand").GetString());
        Assert.Equal(s.Warehouse2, json.GetProperty("scope").GetProperty("warehouseId").GetString());
        Assert.Equal(s.Item.ToString(), json.GetProperty("itemId").GetString());
        Assert.Equal("v1", json.GetProperty("contractVersion").GetString());
    }
}
