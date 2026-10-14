using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-PM1 — the PRODUCT-MASTER v1.1 consumer rules, through the REAL host AND the REAL <c>ProductMasterHttpClient</c>
/// (<see cref="ProductMasterV11ApiFactory"/>: v1.1 wire shapes at the HTTP edge). Written FIRST, against the pre-PM1 code, to
/// show where control was silently lost (evidence §3):
/// <list type="bullet">
/// <item>Flags come from <c>getSku</c> (OD-1, R-04); an unknown lot flag refuses (OD-5) — never "false".</item>
/// <item>Serial at GSKU = "not applicable" (OD-10): a GSKU movement only when the PRODUCT's serial flag is explicitly false;
/// at LSKU / FinishedGood an unknown serial flag refuses; at Item it is always false.</item>
/// <item>Lifecycle (OD-3 / OD-4 / CT-PM1-2): Deactivated refuses purchase / production receipt (and opening balance — "no new
/// stock"), Retired / Archived allows only SCRAP / WRITE_OFF, Draft / unknown / missing allows nothing; REVERSAL is never
/// asked of PRODUCT-MASTER (contract: accepted in every state).</item>
/// <item>Open enums (CT-PM1-3): unknown values never crash; they refuse where a decision depends on them.</item>
/// </list>
/// </summary>
public sealed class ProductMasterV11RulesTests : IClassFixture<ProductMasterV11ApiFactory>
{
    private readonly ProductMasterV11ApiFactory _factory;

    public ProductMasterV11RulesTests(ProductMasterV11ApiFactory factory) => _factory = factory;

    private FakeProductMaster Master => _factory.Master;

    private static string Code(JsonElement body) => body.GetProperty("error").GetProperty("code").GetString()!;

    /// <summary>An LSKU world: product flags as given (deprecated), SKU flags as given (authoritative).</summary>
    private InventoryScenario Lsku(object? skuLot, object? skuSerial, object? productLot = null, object? productSerial = null, bool lotControlled = false)
    {
        var s = new InventoryScenario(_factory, lotControlled: lotControlled);
        Master.Product(s.Item, lotControlled: productLot ?? FakeProductMaster.Absent, serialControlled: productSerial ?? FakeProductMaster.Absent);
        Master.Sku(s.Sku, "Lsku", s.Item, skuLot, skuSerial);
        return s;
    }

    private static Dictionary<string, object?> AtLevel(Dictionary<string, object?> body, string level, Guid skuId, Guid? itemId = null)
    {
        body["skuLevel"] = level;
        body["skuId"] = skuId.ToString();
        if (itemId is not null) body["itemId"] = itemId.ToString();
        return body;
    }

    // ── flags ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unknown_lot_flag_on_the_sku_refuses_the_receipt_and_writes_nothing()
    {
        var s = Lsku(skuLot: null, skuSerial: false);
        var refused = await s.PostExpectingAsync(s.Receipt("5", "GOODS_RECEIPT_PO"), 422);
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", Code(refused));
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task An_unknown_lot_flag_refuses_an_outbound_movement_too()
    {
        var s = Lsku(skuLot: false, skuSerial: false);
        await s.PostExpectingAsync(s.Receipt("5"), 201);
        Master.SetSku(s.Sku, "lotControlled", null);
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", Code(await s.PostExpectingAsync(s.Issue("2"), 422)));
    }

    [Fact]
    public async Task The_sku_flag_wins_over_the_deprecated_product_flag()
    {
        var s = Lsku(skuLot: false, skuSerial: false, productLot: true, productSerial: true);
        await s.PostExpectingAsync(s.Receipt("5", "GOODS_RECEIPT_PO"), 201); // no lot, no serials: the SKU says neither
    }

    [Fact]
    public async Task A_lot_controlled_sku_still_requires_a_lot()
    {
        var s = Lsku(skuLot: true, skuSerial: false, productLot: false);
        Assert.Equal("LOT_REQUIRED", Code(await s.PostExpectingAsync(s.Receipt("5", "GOODS_RECEIPT_PO"), 422)));
    }

    [Fact]
    public async Task An_unknown_serial_flag_at_Lsku_refuses()
    {
        var s = Lsku(skuLot: false, skuSerial: null, productSerial: false);
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", Code(await s.PostExpectingAsync(s.Receipt("5", "GOODS_RECEIPT_PO"), 422)));
    }

    [Fact]
    public async Task An_unknown_serial_flag_at_FinishedGood_refuses()
    {
        var s = new InventoryScenario(_factory);
        var fg = Guid.NewGuid();
        Master.Product(s.Item, serialControlled: false);
        Master.Sku(fg, "FinishedGood", s.Item, lotControlled: false, serialControlled: null);
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", Code(await s.PostExpectingAsync(AtLevel(s.Receipt("5", "GOODS_RECEIPT_PO"), "FinishedGood", fg), 422)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task A_Gsku_movement_of_a_product_that_is_not_explicitly_serial_free_is_refused(bool? productSerial)
    {
        var s = new InventoryScenario(_factory);
        var gsku = Guid.NewGuid();
        Master.Product(s.Item, lotControlled: false, serialControlled: productSerial is null ? FakeProductMaster.Absent : productSerial);
        if (productSerial is null) Master.SetProduct(s.Item, "serialControlled", null);
        Master.Sku(gsku, "Gsku", s.Item, lotControlled: false, serialControlled: null);
        var body = AtLevel(s.Receipt("4", "GOODS_RECEIPT_PO"), "Gsku", gsku);
        body["serialIds"] = new[] { "SN-1", "SN-2", "SN-3", "SN-4" };
        Assert.Equal("SKU_LEVEL_NOT_STOCKABLE", Code(await s.PostExpectingAsync(body, 422)));
    }

    [Fact]
    public async Task A_Gsku_movement_of_a_product_whose_serial_flag_is_explicitly_false_posts()
    {
        var s = new InventoryScenario(_factory);
        var gsku = Guid.NewGuid();
        Master.Product(s.Item, lotControlled: false, serialControlled: false);
        Master.Sku(gsku, "Gsku", s.Item, lotControlled: false, serialControlled: null);
        await s.PostExpectingAsync(AtLevel(s.Receipt("4", "GOODS_RECEIPT_PO"), "Gsku", gsku), 201);
    }

    // ── Item level (Q-1) ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_material_item_posts_at_Item_level_without_serials()
    {
        var s = new InventoryScenario(_factory);
        var item = Guid.NewGuid();
        Master.MaterialItem(item, lotControlled: false);
        var posted = await s.PostExpectingAsync(AtLevel(s.Receipt("2.5", "GOODS_RECEIPT_PO"), "Item", item, item), 201);
        Assert.Equal("Item", posted.GetProperty("balanceSnapshot").GetProperty("skuLevel").GetString());
    }

    [Fact]
    public async Task A_lot_controlled_material_item_requires_a_lot_at_Item_level()
    {
        var s = new InventoryScenario(_factory);
        var item = Guid.NewGuid();
        Master.MaterialItem(item, lotControlled: true);
        Assert.Equal("LOT_REQUIRED", Code(await s.PostExpectingAsync(AtLevel(s.Receipt("2", "GOODS_RECEIPT_PO"), "Item", item, item), 422)));
    }

    // ── lifecycle (OD-3 / OD-4 / CT-PM1-2) ───────────────────────────────────────────────────────────────

    private async Task<InventoryScenario> StockedThen(string lifecycle)
    {
        var s = Lsku(skuLot: false, skuSerial: false, productLot: false, productSerial: false);
        await s.PostExpectingAsync(s.Receipt("50"), 201);
        Master.SetLifecycle(s.Item, lifecycle);
        return s;
    }

    private static Dictionary<string, object?> Movement(InventoryScenario s, string type) => type switch
    {
        "GOODS_RECEIPT_PO" or "GOODS_RECEIPT_PROD" or "OPENING_BALANCE" or "COUNT_GAIN" or "CUSTOMER_RETURN" => s.Receipt("1", type),
        "GOODS_ISSUE_SALES" or "GOODS_ISSUE_PROD" or "SUPPLIER_RETURN" or "COUNT_LOSS" or "SCRAP" or "WRITE_OFF" => s.Issue("1", type),
        "LOCATION_TRANSFER" => Transfer(s, "LOCATION_TRANSFER"),
        "WAREHOUSE_TRANSFER" => Transfer(s, "WAREHOUSE_TRANSFER"),
        "STATUS_TRANSFER" => StatusTransfer(s),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static Dictionary<string, object?> Transfer(InventoryScenario s, string type)
    {
        var body = s.Issue("1", type);
        body["toWarehouseId"] = type == "WAREHOUSE_TRANSFER" ? s.Warehouse2 : s.Warehouse;
        body["toLocationId"] = type == "WAREHOUSE_TRANSFER" ? s.LocationC : s.LocationB;
        return body;
    }

    private static Dictionary<string, object?> StatusTransfer(InventoryScenario s)
    {
        var body = s.Issue("1", "STATUS_TRANSFER", fromStatus: "AVAILABLE");
        body["toWarehouseId"] = s.Warehouse;
        body["toLocationId"] = s.LocationA;
        body["toStockStatus"] = "BLOCKED";
        return body;
    }

    [Theory]
    [InlineData("Deactivated", "GOODS_RECEIPT_PO", 422)]
    [InlineData("Deactivated", "GOODS_RECEIPT_PROD", 422)]
    [InlineData("Deactivated", "OPENING_BALANCE", 422)]
    [InlineData("Deactivated", "GOODS_ISSUE_SALES", 201)]
    [InlineData("Deactivated", "GOODS_ISSUE_PROD", 201)]
    [InlineData("Deactivated", "CUSTOMER_RETURN", 201)]
    [InlineData("Deactivated", "SUPPLIER_RETURN", 201)]
    [InlineData("Deactivated", "LOCATION_TRANSFER", 201)]
    [InlineData("Deactivated", "WAREHOUSE_TRANSFER", 201)]
    [InlineData("Deactivated", "STATUS_TRANSFER", 201)]
    [InlineData("Deactivated", "COUNT_GAIN", 201)]
    [InlineData("Deactivated", "COUNT_LOSS", 201)]
    [InlineData("Deactivated", "SCRAP", 201)]
    [InlineData("Deactivated", "WRITE_OFF", 201)]
    [InlineData("Retired", "GOODS_RECEIPT_PO", 422)]
    [InlineData("Retired", "GOODS_ISSUE_SALES", 422)]
    [InlineData("Retired", "CUSTOMER_RETURN", 422)]
    [InlineData("Retired", "LOCATION_TRANSFER", 422)]
    [InlineData("Retired", "STATUS_TRANSFER", 422)]
    [InlineData("Retired", "COUNT_LOSS", 422)]
    [InlineData("Retired", "SCRAP", 201)]
    [InlineData("Retired", "WRITE_OFF", 201)]
    [InlineData("Archived", "GOODS_ISSUE_SALES", 422)]
    [InlineData("Archived", "SCRAP", 201)]
    [InlineData("Draft", "GOODS_RECEIPT_PO", 422)]
    [InlineData("Draft", "GOODS_ISSUE_SALES", 422)]
    [InlineData("Draft", "SCRAP", 422)]
    [InlineData("Frozen", "GOODS_ISSUE_SALES", 422)]
    [InlineData("Frozen", "SCRAP", 422)]
    [InlineData("", "SCRAP", 422)]
    [InlineData("Active", "GOODS_RECEIPT_PO", 201)]
    [InlineData("Active", "GOODS_ISSUE_SALES", 201)]
    public async Task The_lifecycle_decides_which_movement_types_post(string lifecycle, string movementType, int expected)
    {
        var s = await StockedThen(lifecycle);
        var answer = await s.PostExpectingAsync(Movement(s, movementType), expected);
        if (expected == 422) Assert.Equal("ITEM_NOT_ACTIVE", Code(answer));
    }

    [Fact]
    public async Task A_missing_lifecycle_status_is_unknown_and_refuses()
    {
        var s = await StockedThen("Active");
        Master.SetProduct(s.Item, "lifecycleStatus", FakeProductMaster.Absent);
        Master.SetSku(s.Sku, "lifecycleStatus", FakeProductMaster.Absent);
        Assert.Equal("ITEM_NOT_ACTIVE", Code(await s.PostExpectingAsync(s.Issue("1"), 422)));
    }

    [Fact]
    public async Task The_more_restrictive_of_the_item_and_the_sku_status_decides()
    {
        var s = await StockedThen("Active");
        Master.SetProduct(s.Item, "lifecycleStatus", "Retired"); // the item retired, the SKU row still says Active
        Assert.Equal("ITEM_NOT_ACTIVE", Code(await s.PostExpectingAsync(s.Issue("1"), 422)));
        await s.PostExpectingAsync(s.Issue("1", "SCRAP"), 201);
    }

    [Fact]
    public async Task A_reversal_is_accepted_in_every_state_and_never_asks_product_master()
    {
        var s = Lsku(skuLot: false, skuSerial: false);
        var receipt = s.Receipt("5");
        var posted = await s.PostExpectingAsync(receipt, 201);
        Master.SetLifecycle(s.Item, "Frozen");
        var before = Master.Calls.Values.Sum();
        await s.PostExpectingAsync(s.Reversal(receipt, posted.GetProperty("transactionId").GetString()!), 201);
        Assert.Equal(before, Master.Calls.Values.Sum());
    }

    // ── open enums (CT-PM1-3) and outage ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_values_of_the_open_enums_do_not_crash_a_movement()
    {
        var s = Lsku(skuLot: false, skuSerial: false, productLot: false, productSerial: false);
        Master.SetProduct(s.Item, "itemKind", "Kit");
        Master.SetSku(s.Sku, "itemKind", "Kit");
        Master.SetSku(s.Sku, "materialType", "SOLVENT");
        Master.SetSku(s.Sku, "storageConditions", new System.Text.Json.Nodes.JsonArray("NITROGEN_BLANKET"));
        await s.PostExpectingAsync(s.Receipt("3"), 201);
    }

    [Fact]
    public async Task A_lot_flag_that_is_not_a_boolean_is_not_trusted()
    {
        var s = Lsku(skuLot: "yes", skuSerial: false);
        var answer = await s.PostAsync(s.Receipt("3"));
        Assert.True((int)answer.StatusCode is 422 or 503, "an unreadable flag must refuse, got " + (int)answer.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Product_master_unreachable_is_503_as_before()
    {
        var s = Lsku(skuLot: false, skuSerial: false);
        Master.MarkUnavailable(s.Sku);
        Assert.Equal("PRODUCT_MASTER_UNAVAILABLE", Code(await s.PostExpectingAsync(s.Receipt("3"), 503)));
    }

    // ── calls per movement (evidence) ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Product_master_calls_per_movement_are_measured()
    {
        var s = Lsku(skuLot: false, skuSerial: false, productLot: false, productSerial: false);
        var gsku = Guid.NewGuid();
        Master.Sku(gsku, "Gsku", s.Item, lotControlled: false, serialControlled: null);

        Master.Calls.Clear();
        await s.PostExpectingAsync(s.Receipt("3"), 201);
        var lsku = Master.Calls.ToDictionary(kv => kv.Key, kv => kv.Value);
        Master.Calls.Clear();
        await s.PostExpectingAsync(AtLevel(s.Receipt("3"), "Gsku", gsku), 201);
        var gskuCalls = Master.Calls.ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(3, lsku.Values.Sum()); // validateRefs + getSku + getSkuUom
        Assert.False(lsku.ContainsKey("getProduct"));
        Assert.Equal(4, gskuCalls.Values.Sum()); // + getProduct: the product's serial flag (OD-10)
        Assert.Equal(1, gskuCalls["getProduct"]);
    }
}
