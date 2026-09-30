using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC9 base UoM and the pack §12 rules that need PRODUCT-MASTER / LOCATION (all 422, nothing written).</summary>
public sealed class MasterDataValidationTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public MasterDataValidationTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Hundred_BOX_with_BOX_to_EA_10_over_1_persists_BaseQuantity_1000_EA()
    {
        var s = new InventoryScenario(_factory, conversions: new UomConversionRule("BOX", "EA", 10m, 1m));

        var posted = await s.PostExpectingAsync(s.Receipt("100", uomId: "BOX"), 201);

        Assert.Equal("1000.000", posted.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());
        Assert.Equal("1000.000", posted.GetProperty("balanceSnapshot").GetProperty("baseQuantity").GetString());
        Assert.Equal("EA", posted.GetProperty("balanceSnapshot").GetProperty("baseUomId").GetString());

        var stored = Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Equal(100m, stored["Quantity"].AsDecimal);
        Assert.Equal("BOX", stored["UomId"].AsString);
        Assert.Equal(1000m, stored["BaseQuantity"].AsDecimal);
        Assert.Equal("EA", stored["BaseUomId"].AsString);

        var history = await s.ReadAsync(await s.GetAsync($"/api/inventory/transactions?itemId={s.Item}"));
        var item = Assert.Single(history.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("100.000", item.GetProperty("quantity").GetString());
        Assert.Equal("1000.000", item.GetProperty("baseQuantity").GetString());
    }

    [Fact]
    public async Task A_chained_conversion_PALLET_BOX_EA_is_followed()
    {
        var s = new InventoryScenario(_factory, conversions:
        [
            new UomConversionRule("BOX", "EA", 10m, 1m),
            new UomConversionRule("PALLET", "BOX", 100m, 1m)
        ]);

        var posted = await s.PostExpectingAsync(s.Receipt("2", uomId: "PALLET"), 201);

        Assert.Equal("2000.000", posted.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());
    }

    [Fact]
    public async Task A_UoM_without_a_conversion_is_422_UOM_CONVERSION_MISSING()
        => await AssertRefused(new InventoryScenario(_factory), s => s.Receipt("1", uomId: "CASE"), "UOM_CONVERSION_MISSING");

    [Fact]
    public async Task Lot_controlled_item_without_lotId_is_422_LOT_REQUIRED()
        => await AssertRefused(new InventoryScenario(_factory, lotControlled: true), s =>
        {
            var body = s.Receipt("1");
            body.Remove("lotId");
            return body;
        }, "LOT_REQUIRED");

    [Fact]
    public async Task Serial_controlled_item_needs_as_many_distinct_serials_as_base_quantity()
    {
        var s = new InventoryScenario(_factory, serialControlled: true);
        var wrong = s.Receipt("3");
        wrong["serialIds"] = new[] { "S1", "S2", "S2" };
        var refused = await s.PostExpectingAsync(wrong, 422);
        Assert.Equal("SERIAL_COUNT_MISMATCH", refused.GetProperty("error").GetProperty("code").GetString());

        var right = s.Receipt("3");
        right["serialIds"] = new[] { "S1", "S2", "S3" };
        await s.PostExpectingAsync(right, 201);
    }

    [Fact]
    public async Task Unknown_item_is_422_UNKNOWN_ITEM()
        => await AssertRefused(new InventoryScenario(_factory), s =>
        {
            var body = s.Receipt("1");
            body["itemId"] = Guid.NewGuid().ToString();
            return body;
        }, "UNKNOWN_ITEM");

    [Fact]
    public async Task Sku_of_another_item_is_422_SKU_ITEM_MISMATCH()
    {
        var s = new InventoryScenario(_factory);
        var foreignSku = Guid.NewGuid();
        var otherItem = Guid.NewGuid();
        _factory.Products.AddItem(otherItem);
        _factory.Products.AddSku(foreignSku, SkuLevel.Lsku, otherItem);

        await AssertRefused(s, x =>
        {
            var body = x.Receipt("1");
            body["skuId"] = foreignSku.ToString();
            return body;
        }, "SKU_ITEM_MISMATCH");
    }

    [Fact]
    public async Task Wrong_sku_level_is_422_UNKNOWN_ITEM()
        => await AssertRefused(new InventoryScenario(_factory), s =>
        {
            var body = s.Receipt("1");
            body["skuLevel"] = "Gsku";
            return body;
        }, "UNKNOWN_ITEM");

    [Fact]
    public async Task Unknown_location_is_422_UNKNOWN_LOCATION()
        => await AssertRefused(new InventoryScenario(_factory), s =>
        {
            var body = s.Receipt("1");
            body["toLocationId"] = "no-such-bin";
            return body;
        }, "UNKNOWN_LOCATION");

    [Fact]
    public async Task Location_under_another_warehouse_is_422_UNKNOWN_LOCATION()
        => await AssertRefused(new InventoryScenario(_factory), s =>
        {
            var body = s.Receipt("1");
            body["toWarehouseId"] = s.Warehouse2;
            return body;
        }, "UNKNOWN_LOCATION");

    [Fact]
    public async Task Non_stockable_or_inactive_location_is_422_UNKNOWN_LOCATION()
    {
        var s = new InventoryScenario(_factory);
        _factory.Locations.AddBin("zone-" + s.Tenant.ToString("N"), s.Warehouse, s.LegalEntity.ToString(), stockable: false);
        _factory.Locations.AddBin("old-" + s.Tenant.ToString("N"), s.Warehouse, s.LegalEntity.ToString(), status: "Inactive");

        foreach (var location in new[] { "zone-" + s.Tenant.ToString("N"), "old-" + s.Tenant.ToString("N") })
        {
            var body = s.Receipt("1");
            body["toLocationId"] = location;
            var refused = await s.PostExpectingAsync(body, 422);
            Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());
        }

        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    /// <summary>
    /// ASSUMPTION-0173-07: the Location-LE check is ON by default. With it on, a location that belongs to ANOTHER
    /// legal entity is refused — the proof the pack demands before the check may be disabled in the Mock profile.
    /// </summary>
    [Fact]
    public async Task Location_of_another_legal_entity_is_refused_while_the_check_is_on()
    {
        var s = new InventoryScenario(_factory);
        var foreign = "foreign-" + s.Tenant.ToString("N");
        _factory.Locations.AddBin(foreign, s.Warehouse, Guid.NewGuid().ToString());

        await AssertRefused(s, x =>
        {
            var body = x.Receipt("1");
            body["toLocationId"] = foreign;
            return body;
        }, "UNKNOWN_LOCATION");

        // Control: the same request against a location of the caller's own LE posts.
        await s.PostExpectingAsync(s.Receipt("1"), 201);
    }

    private static async Task AssertRefused(InventoryScenario s, Func<InventoryScenario, Dictionary<string, object?>> body, string code)
    {
        var refused = await s.PostExpectingAsync(body(s), 422);
        Assert.Equal(code, refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_balances"));
        Assert.Empty(s.Persisted("inventory_outbox"));
    }
}
