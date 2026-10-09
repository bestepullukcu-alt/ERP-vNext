using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-PM1 NE-4 — <c>skuLevel: Item</c> (Q-1, <c>skuId == itemId</c>) on every ledger operation INVENTORY-BUNDLE 1.1.0 lists:
/// postMovement, getAvailability, getBalance, getTransactions, createReservation / releaseReservation. Every answer is checked
/// against the contract by <see cref="ContractValidatingHandler"/> (1.1.0 <c>SkuLevel</c> carries <c>Item</c>).
/// </summary>
public sealed class ItemLevelLedgerTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ItemLevelLedgerTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_material_item_is_received_read_listed_reserved_and_released_at_Item_level()
    {
        var s = new InventoryScenario(_factory);
        var item = Guid.NewGuid();
        _factory.Products.AddMaterialItem(item, baseUomId: "KGM");
        var receipt = s.Receipt("12.5", "GOODS_RECEIPT_PO", uomId: "KGM");
        receipt["itemId"] = item.ToString();
        receipt["skuId"] = item.ToString();
        receipt["skuLevel"] = "Item";
        var posted = await s.PostExpectingAsync(receipt, 201);
        Assert.Equal("Item", posted.GetProperty("balanceSnapshot").GetProperty("skuLevel").GetString());

        var availability = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?itemId={item}&skuId={item}&skuLevel=Item"));
        Assert.Equal("12.500", availability.GetProperty("onHand").GetString());

        var balance = await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?itemId={item}&skuId={item}&skuLevel=Item"));
        Assert.Equal("Item", Assert.Single(balance.GetProperty("rows").EnumerateArray()).GetProperty("skuLevel").GetString());

        var transactions = await s.ReadAsync(await s.GetAsync($"/api/inventory/transactions?itemId={item}"));
        Assert.Single(transactions.GetProperty("items").EnumerateArray());

        var reservation = s.Reservation("2");
        reservation["itemId"] = item.ToString();
        reservation["skuId"] = item.ToString();
        reservation["skuLevel"] = "Item";
        var reserved = await s.ReserveExpectingAsync(reservation, 201);
        using var released = await s.ReleaseAsync(reserved.GetProperty("reservationId").GetString()!, reserved.GetProperty("etag").GetString());
        Assert.Equal(200, (int)released.StatusCode);
    }
}

/// <summary>
/// WP-0173-PM1 — MOD-0174 lot creation and the typed-lot migration read the lot flag from the SKU (OD-1); an unknown flag
/// refuses (OD-5) and never reads as "not lot-controlled"; a new lot's tracking mode over-requires under doubt.
/// TRACE-BUNDLE 1.2.0 is unchanged: it has no <c>Item</c> level and no "data incomplete" code (open point O-PM1-1).
/// </summary>
public sealed class LotFlagFromSkuTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LotFlagFromSkuTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_unknown_lot_flag_on_the_sku_refuses_lot_creation_and_saves_nothing()
    {
        var t = new TraceScenario(_factory, lotControlled: true);
        _factory.Skus.SetFlags(t.Sku, lotControlled: null, serialControlled: false);
        var refused = await t.Expect(t.CreateAsync(t.LotBody(TraceScenario.LotNo())), 503);
        Assert.Equal("PRODUCT_MASTER_UNAVAILABLE", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, await _factory.Collection("trace_lots").CountDocumentsAsync(new BsonDocument("TenantId", t.Tenant.ToString())));
    }

    [Fact]
    public async Task The_sku_lot_flag_wins_over_the_product_flag()
    {
        var t = new TraceScenario(_factory, lotControlled: true);
        _factory.Skus.SetFlags(t.Sku, lotControlled: false, serialControlled: false);
        var refused = await t.Expect(t.CreateAsync(t.LotBody(TraceScenario.LotNo())), 422);
        Assert.Equal("PRODUCT_NOT_LOT_CONTROLLED", refused.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(true, "LotAndSerial")]
    [InlineData(false, "Lot")]
    [InlineData(null, "LotAndSerial")]
    public async Task A_new_lot_follows_the_sku_serial_flag_and_an_unknown_one_requires_serials(bool? skuSerial, string expected)
    {
        var t = new TraceScenario(_factory, lotControlled: true, serialControlled: false);
        _factory.Skus.SetFlags(t.Sku, lotControlled: true, serialControlled: skuSerial);
        var (_, _, json) = await t.CreateLotAsync();
        Assert.Equal(expected, json.GetProperty("trackingMode").GetString());
    }

    [Fact]
    public async Task An_Item_level_lot_is_still_the_TRACE_400()
    {
        var t = new TraceScenario(_factory, lotControlled: true);
        var body = t.LotBody(TraceScenario.LotNo());
        body["skuLevel"] = "Item";
        await t.Expect(t.CreateAsync(body), 400);
    }

    [Fact]
    public async Task The_typed_lot_migration_stops_with_503_on_an_unknown_lot_flag()
    {
        var m = new MigrationScenario(_factory);
        await m.ReceiveTypedAsync("TYPED-1", "5");
        Assert.True((await m.ListAsync()).GetProperty("items").GetArrayLength() >= 1);

        _factory.Products.SetSkuFlags(m.Ledger.Sku, lotControlled: null, serialControlled: false);
        await m.ListAsync(expected: 503);
    }

    [Fact]
    public async Task A_typed_group_whose_sku_is_not_lot_controlled_is_left_out_even_if_the_product_says_lot()
    {
        var m = new MigrationScenario(_factory);
        await m.ReceiveTypedAsync("TYPED-2", "5");
        _factory.Products.SetSkuFlags(m.Ledger.Sku, lotControlled: false, serialControlled: false);
        var list = await m.ListAsync();
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());
    }
}
