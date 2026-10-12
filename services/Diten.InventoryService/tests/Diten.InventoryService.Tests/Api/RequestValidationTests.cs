using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>Pack §12 structural rules and §13.3 — each a contract 400 and no write.</summary>
public sealed class RequestValidationTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public RequestValidationTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Unknown_movement_type_is_400_INVALID_MOVEMENT_TYPE()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["movementType"] = "TELEPORT";

        var refused = await s.PostExpectingAsync(body, 400);

        Assert.Equal("INVALID_MOVEMENT_TYPE", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Movement_type_is_case_sensitive()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["movementType"] = "opening_balance";

        var refused = await s.PostExpectingAsync(body, 400);

        Assert.Equal("INVALID_MOVEMENT_TYPE", refused.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1e3")]
    [InlineData("1.1234567")]
    [InlineData("abc")]
    [InlineData(" 1")]
    [InlineData("1,5")]
    public async Task Bad_quantities_are_400(string quantity)
    {
        var s = new InventoryScenario(_factory);

        var refused = await s.PostExpectingAsync(s.Receipt(quantity), 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task A_numeric_JSON_quantity_is_400_decimals_travel_as_strings()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["quantity"] = 1.5m;

        await s.PostExpectingAsync(body, 400);
    }

    [Fact]
    public async Task Six_fraction_digits_are_accepted_and_kept_exactly()
    {
        var s = new InventoryScenario(_factory);

        var posted = await s.PostExpectingAsync(s.Receipt("0.123456"), 201);

        Assert.Equal("0.123456", posted.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());
    }

    [Fact]
    public async Task Receipt_without_a_target_location_is_400()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body.Remove("toLocationId");

        await s.PostExpectingAsync(body, 400);
    }

    [Fact]
    public async Task Inbound_naming_a_source_side_is_400()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["fromWarehouseId"] = s.Warehouse;

        await s.PostExpectingAsync(body, 400);
    }

    [Fact]
    public async Task Outbound_naming_a_target_side_is_400()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Issue("1");
        body["toLocationId"] = s.LocationB;

        await s.PostExpectingAsync(body, 400);
    }

    [Theory]
    [InlineData("WAREHOUSE_TRANSFER", true, false)]
    [InlineData("LOCATION_TRANSFER", false, false)]
    [InlineData("STATUS_TRANSFER", false, true)]
    public async Task Transfers_follow_the_direction_matrix(string type, bool otherWarehouse, bool statusChange)
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("10"), 201);
        var body = s.Movement(type, "4");
        body["fromWarehouseId"] = s.Warehouse;
        body["fromLocationId"] = s.LocationA;
        body["toWarehouseId"] = otherWarehouse ? s.Warehouse2 : s.Warehouse;
        body["toLocationId"] = otherWarehouse ? s.LocationC : statusChange ? s.LocationA : s.LocationB;
        if (statusChange) body["toStockStatus"] = "BLOCKED";

        await s.PostExpectingAsync(body, 201);

        Assert.Equal(6m, await s.OnHandAsync(s.LocationA, "AVAILABLE"));
        var target = statusChange ? await s.OnHandAsync(s.LocationA, "BLOCKED") : await s.OnHandAsync(otherWarehouse ? s.LocationC : s.LocationB);
        Assert.Equal(4m, target);
    }

    [Fact]
    public async Task A_transfer_that_changes_status_is_400_single_step()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Movement("LOCATION_TRANSFER", "1");
        body["fromWarehouseId"] = s.Warehouse;
        body["fromLocationId"] = s.LocationA;
        body["toWarehouseId"] = s.Warehouse;
        body["toLocationId"] = s.LocationB;
        body["toStockStatus"] = "QUARANTINE";

        await s.PostExpectingAsync(body, 400);
    }

    [Theory]
    [InlineData("2026-09-30T10:00:00+03:00")]
    [InlineData("2026-09-30T10:00:00")]
    [InlineData("2026-09-30")]
    [InlineData("30.09.2026")]
    public async Task Posting_date_must_be_UTC_ISO_8601(string date)
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["postingDate"] = date;

        await s.PostExpectingAsync(body, 400);
    }

    [Theory]
    [InlineData("itemId", "not-a-uuid")]
    [InlineData("skuLevel", "lsku")]
    [InlineData("toStockStatus", "GOOD")]
    [InlineData("sourceModule", "")]
    [InlineData("uomId", null)]
    public async Task Malformed_fields_are_400(string field, string? value)
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body[field] = value;

        var refused = await s.PostExpectingAsync(body, 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_body_that_is_not_a_JSON_object_is_400_not_415()
    {
        var s = new InventoryScenario(_factory);
        foreach (var content in new HttpContent[]
                 {
                     new StringContent("[1,2]", System.Text.Encoding.UTF8, "application/json"),
                     new StringContent("not json", System.Text.Encoding.UTF8, "text/plain"),
                     new StringContent(string.Empty)
                 })
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/movements") { Content = content };
            request.Headers.Add("Idempotency-Key", "k1");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", s.Token);

            using var response = await s.SendAsync(request);

            Assert.Equal(400, (int)response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/inventory/balance?pageSize=201")]
    [InlineData("/api/inventory/balance?pageSize=0")]
    [InlineData("/api/inventory/balance?page=0")]
    [InlineData("/api/inventory/balance?itemId=nope")]
    [InlineData("/api/inventory/balance?stockStatus=GOOD")]
    [InlineData("/api/inventory/transactions?pageSize=500")]
    [InlineData("/api/inventory/transactions?from=2026-13-01")]
    [InlineData("/api/inventory/transactions?from=2026-10-02&to=2026-10-01")]
    [InlineData("/api/inventory/availability")]
    [InlineData("/api/inventory/availability?itemId=nope")]
    public async Task Malformed_read_parameters_are_400(string url)
    {
        var s = new InventoryScenario(_factory);

        using var response = await s.GetAsync(url);

        Assert.Equal(400, (int)response.StatusCode);
    }
}
