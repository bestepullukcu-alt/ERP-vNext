using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Features.ProductMaster.Handlers;
using Diten.MdmService.Application.Features.ProductMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 (P-2026-10-08-FU04-S4) — product-master-bundle v1.1 over HTTP against a throwaway replica-set
/// mongod: the five v1.0 reads answer the material item too, the three v1.1 additions, every answer validated against
/// the contract file (v1.1 strict, v1.0 backward compatible), §16.3 status mapping, O-1 (no stock field on a Global
/// Product), the product scope on product rows only, tenant isolation, §13 refusals and the 64 KB header.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class ProductMasterS4HttpTests(MdmHttpHost host)
{
    private const string Base = "/api/product-master";
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    private static readonly string[] StockFields =
    [
        // OD-1 (2026-10-09): lotControlled / serialControlled came back; every other stock field stays off a Global Product.
        "baseUomId", "shelfLifeDays", "minRemainingShelfLifeDays", "storageCondition", "retestDays",
        "storageConditions", "stockable", "materialType"
    ];

    private HttpClient Reader() => host.Client(host.Token(_tenant, _user, ProductMasterContract.ReadPermission));

    // ------------------------------------------------------------------------------------------- getProduct / getItem

    [Fact]
    public async Task S4_GetProduct_answers_the_item_with_its_v11_fields()
    {
        var world = await SeedWorldAsync();

        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{world.Item.Id:D}"), "getProduct");

        Assert.Equal("Item", body.GetProperty("itemKind").GetString());
        Assert.Equal(world.Item.Id.ToString("D"), body.GetProperty("itemId").GetString());
        Assert.Equal(world.Item.CanonicalCode, body.GetProperty("canonicalCode").GetString());
        Assert.Equal("ACTIVE_INGREDIENT", body.GetProperty("materialType").GetString());
        Assert.Equal("Active", body.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("KGM", body.GetProperty("baseUomId").GetString());
        Assert.Equal(1825, body.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal(365, body.GetProperty("retestDays").GetInt32());
        Assert.Equal("AMBIENT_15_25", body.GetProperty("storageCondition").GetString());
        Assert.Equal(["AMBIENT_15_25", "PROTECT_FROM_MOISTURE"], Strings(body.GetProperty("storageConditions")));
        Assert.True(body.GetProperty("lotControlled").GetBoolean());
        Assert.False(body.GetProperty("serialControlled").GetBoolean());
        Assert.True(body.GetProperty("stockable").GetBoolean());
        Assert.Equal("v1.1", body.GetProperty("contractVersion").GetString());
    }

    // O-1 — behaviour narrowing (not behind a flag), as OD-1 narrowed it: a Global Product's ProductRef carries none of the
    // stock fields but the deprecated lot / serial pair.
    [Fact]
    public async Task S4_O1_GetProduct_of_a_global_product_returns_no_stock_field()
    {
        var world = await SeedWorldAsync();

        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{world.Product.Id:D}"), "getProduct");

        Assert.Equal("GlobalProduct", body.GetProperty("itemKind").GetString());
        Assert.Equal(world.Product.CanonicalCode, body.GetProperty("canonicalCode").GetString());
        Assert.Equal("Active", body.GetProperty("lifecycleStatus").GetString());
        foreach (var field in StockFields)
            Assert.False(body.TryGetProperty(field, out _), $"a Global Product answered the stock field '{field}' (O-1)");
        Assert.True(body.GetProperty("lotControlled").GetBoolean());
        Assert.True(body.GetProperty("serialControlled").GetBoolean());
    }

    // OD-1, draft.7 value rule (true in doubt) — the sabotage target "the old rule (all agree → that value, else null)
    // comes back": over the product's non-retired GSKUs, any true or null is true, only all definitively false is false.
    [Theory]
    [InlineData(new[] { "true", "false" }, "true")]
    [InlineData(new[] { "false", "null" }, "true")]
    [InlineData(new[] { "false", "false" }, "false")]
    [InlineData(new[] { "true", "true" }, "true")]
    public async Task S4_OD1_GetProduct_lot_is_true_in_doubt_over_the_live_gskus(string[] lots, string expected)
    {
        var (product, revision) = await SeedProductAsync();
        var pack = 2;
        foreach (var lot in lots)
        {
            var gsku = await SeedGskuAsync(revision.Id, pack++, complete: true, ProductIdentityLifecycleStatus.IdentityApproved);
            await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku.Id,
                Builders<Gsku>.Update.Set(x => x.LotControlled, Flag(lot)));
        }
        // A retired GSKU does not count, whatever it says.
        var retired = await SeedGskuAsync(revision.Id, 99, complete: true, ProductIdentityLifecycleStatus.Retired);
        await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == retired.Id,
            Builders<Gsku>.Update.Set(x => x.LotControlled, expected == "false" ? (bool?)true : false));

        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{product.Id:D}"), "getProduct");

        Assert.Equal(expected, body.GetProperty("lotControlled").GetRawText());
    }

    // The same rule for serial, over the product's non-retired LSKUs and Finished Goods (one LSKU, one FG here).
    [Theory]
    [InlineData("true", "false", "true")]
    [InlineData("false", "null", "true")]
    [InlineData("false", "false", "false")]
    [InlineData("null", "null", "true")]
    public async Task S4_OD1_GetProduct_serial_is_true_in_doubt_over_the_live_lskus_and_finished_goods(
        string lskuSerial, string finishedGoodSerial, string expected)
    {
        var world = await SeedWorldAsync();
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == world.Lsku.Id,
            Builders<Lsku>.Update.Set(x => x.SerialControlled, Flag(lskuSerial)));
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").UpdateOneAsync(x => x.Id == world.FinishedGood.Id,
            Builders<FinishedGood>.Update.Set(x => x.SerialControlled, Flag(finishedGoodSerial)));
        // A retired LSKU does not count, whatever it says.
        var retired = await SeedLskuAsync(world.Gsku.Id, "DE", serial: expected == "false");
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == retired.Id,
            Builders<Lsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired));

        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{world.Product.Id:D}"), "getProduct");

        Assert.Equal(expected, body.GetProperty("serialControlled").GetRawText());
    }

    // No live SKU at all: lot is null; a product with GSKUs but no LSKU / FG: serial is null.
    [Fact]
    public async Task S4_OD1_GetProduct_without_live_skus_answers_null()
    {
        var (product, revision) = await SeedProductAsync();
        await SeedGskuAsync(revision.Id, 3, complete: true, ProductIdentityLifecycleStatus.Retired);
        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{product.Id:D}"), "getProduct");
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lotControlled").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("serialControlled").ValueKind);

        await SeedGskuAsync(revision.Id, 4, complete: true, ProductIdentityLifecycleStatus.IdentityApproved);
        body = await OkAsync(await Reader().GetAsync($"{Base}/products/{product.Id:D}"), "getProduct");
        Assert.True(body.GetProperty("lotControlled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("serialControlled").ValueKind);
    }

    private static bool? Flag(string text) => text switch { "true" => true, "false" => false, _ => null };

    private async Task<(GlobalProduct Product, ProductDefinitionRevision Revision)> SeedProductAsync()
    {
        var product = new GlobalProduct
        {
            TenantId = _tenant, CanonicalCode = Code("GP"), GlobalProductName = "Product " + Guid.NewGuid().ToString("N")[..8],
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        product.GlobalProductNameNormalized = product.GlobalProductName.ToUpperInvariant();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
        var revision = new ProductDefinitionRevision
        {
            TenantId = _tenant, GlobalProductId = product.Id, RevisionIdentifier = "R1",
            CreationCommandId = "rev-" + Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        return (product, revision);
    }

    // No SKU at all: both unknown.
    [Fact]
    public async Task S4_OD1_GetProduct_of_a_product_without_skus_answers_null_lot_and_serial()
    {
        var world = await SeedWorldAsync();
        var bare = new GlobalProduct
        {
            TenantId = _tenant, CanonicalCode = Code("GP"), GlobalProductName = "Bare product", GlobalProductNameNormalized = "BARE PRODUCT",
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(bare);

        var body = await OkAsync(await Reader().GetAsync($"{Base}/products/{bare.Id:D}"), "getProduct");

        Assert.Equal(JsonValueKind.Null, body.GetProperty("lotControlled").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("serialControlled").ValueKind);
        Assert.Equal("Draft", body.GetProperty("lifecycleStatus").GetString());
        Assert.NotEqual(world.Product.Id, bare.Id);
    }

    // Items 2 and 3: every getSku level carries lot + serial, retestDays and minRemainingShelfLifeDays as fields; a GSKU's
    // serial is null (decided at LSKU / Finished Good level); product SKUs have no retest / minimum remaining yet (S3b).
    [Fact]
    public async Task S4_GetSku_every_level_carries_lot_serial_retest_and_minimum_remaining()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        var levels = new[]
        {
            (world.Item.Id, "Item"), (world.Gsku.Id, "Gsku"), (world.Lsku.Id, "Lsku"), (world.FinishedGood.Id, "FinishedGood")
        };
        foreach (var (id, level) in levels)
        {
            var sku = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}?level={level}"), "getSku");
            foreach (var field in new[] { "lotControlled", "serialControlled", "retestDays", "minRemainingShelfLifeDays" })
                Assert.True(sku.TryGetProperty(field, out _), $"{level} has no '{field}'");
            if (level != "Item")
            {
                Assert.Equal(JsonValueKind.Null, sku.GetProperty("retestDays").ValueKind);
                Assert.Equal(JsonValueKind.Null, sku.GetProperty("minRemainingShelfLifeDays").ValueKind);
            }
        }
        var gsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"), "getSku");
        Assert.True(gsku.GetProperty("lotControlled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gsku.GetProperty("serialControlled").ValueKind);
        var item = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Item.Id:D}?level=Item"), "getSku");
        Assert.Equal(90, item.GetProperty("minRemainingShelfLifeDays").GetInt32());
    }

    // Item 4: getSkuUom reads `level` exactly as getSku: the same values, the same 400 for an unknown one, the same 404
    // when the record is not of that level. Without it the v1.0 call still answers.
    [Fact]
    public async Task S4_GetSkuUom_reads_level_as_getSku_does()
    {
        var world = await SeedWorldAsync();
        var client = Reader();

        foreach (var level in new[] { "Box", "item", "GSKU" })
        {
            var sku = await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level={level}");
            var uom = await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}/uom?level={level}");
            Assert.Equal(sku.StatusCode, uom.StatusCode);
            Assert.Equal(await ErrorCodeAsync(sku), await ErrorCodeAsync(uom));
        }
        foreach (var (id, level, wrong) in new[]
                 {
                     (world.Item.Id, "Item", "Gsku"), (world.Gsku.Id, "Gsku", "Lsku"), (world.Lsku.Id, "Lsku", "FinishedGood"),
                     (world.FinishedGood.Id, "FinishedGood", "Item")
                 })
        {
            await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}/uom?level={level}"), "getSkuUom");
            var sku = await client.GetAsync($"{Base}/skus/{id:D}?level={wrong}");
            var uom = await client.GetAsync($"{Base}/skus/{id:D}/uom?level={wrong}");
            Assert.Equal(HttpStatusCode.NotFound, uom.StatusCode);
            Assert.Equal(sku.StatusCode, uom.StatusCode);
            Assert.Equal("UNKNOWN_SKU", await ErrorCodeAsync(uom));
        }
        await OkAsync(await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}/uom"), "getSkuUom");
    }

    // X-08 (draft.6), the sabotage target "getSku without a level answers the wrong level": order and shipment rows carry
    // no level; the server resolves it from the id and answers it in skuLevel — on getSku, resolveSkuProduct and getSkuUom.
    [Fact]
    public async Task S4_X08_Without_a_level_the_level_is_resolved_from_the_id()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        foreach (var (id, level) in new[]
                 {
                     (world.Item.Id, "Item"), (world.Gsku.Id, "Gsku"), (world.Lsku.Id, "Lsku"), (world.FinishedGood.Id, "FinishedGood")
                 })
        {
            var sku = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}"), "getSku");
            Assert.Equal(level, sku.GetProperty("skuLevel").GetString());
            Assert.Equal(id.ToString("D"), sku.GetProperty("skuId").GetString());
            var withLevel = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}?level={level}"), "getSku");
            Assert.Equal(withLevel.GetRawText(), sku.GetRawText());

            var link = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}/product"), "resolveSkuProduct");
            Assert.Equal(level, link.GetProperty("skuLevel").GetString());
            Assert.Equal(level == "Item" ? id.ToString("D") : world.Product.Id.ToString("D"), link.GetProperty("itemId").GetString());

            var uom = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}/uom"), "getSkuUom");
            var uomWithLevel = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}/uom?level={level}"), "getSkuUom");
            Assert.Equal(uomWithLevel.GetRawText(), uom.GetRawText());
        }
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Product.Id:D}"), 404, "UNKNOWN_SKU", "getSku");
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{Guid.NewGuid():D}/product"), 404, "UNKNOWN_SKU", "resolveSkuProduct");
    }

    // X-08, the sabotage target "a (b') row is refused": validateRefs takes a sku without its level (b') and an itemId
    // with a sku without its level (c'); the resolved level comes back on the row.
    [Fact]
    public async Task S4_X08_Validate_takes_sku_rows_without_a_level()
    {
        var world = await SeedWorldAsync();
        var refs = new object[]
        {
            new { skuId = world.FinishedGood.Id.ToString("D") },
            new { skuId = world.Item.Id.ToString("D") },
            new { skuId = Guid.NewGuid().ToString("D") },
            new { itemId = world.Product.Id.ToString("D"), skuId = world.Lsku.Id.ToString("D") },
            new { itemId = world.Item.Id.ToString("D"), skuId = world.Lsku.Id.ToString("D") }
        };

        var body = await OkAsync(await Reader().PostAsync($"{Base}/validate", Json(new { refs })), "validateRefs");
        var results = body.GetProperty("results").EnumerateArray().ToArray();

        Assert.Equal([true, true, false, true, false], results.Select(x => x.GetProperty("exists").GetBoolean()));
        Assert.Equal("FinishedGood", results[0].GetProperty("skuLevel").GetString());
        Assert.Equal("GlobalProduct", results[0].GetProperty("itemKind").GetString());
        Assert.Equal("Item", results[1].GetProperty("skuLevel").GetString());
        Assert.Equal("Item", results[1].GetProperty("itemKind").GetString());
        Assert.False(results[2].TryGetProperty("skuLevel", out _));
        Assert.Equal("Lsku", results[3].GetProperty("skuLevel").GetString());
        Assert.Equal("Active", results[3].GetProperty("lifecycleStatus").GetString());
    }

    [Fact]
    public async Task S4_GetItem_answers_an_item_and_404_UNKNOWN_ITEM_for_a_global_product()
    {
        var world = await SeedWorldAsync();
        var client = Reader();

        var item = await OkAsync(await client.GetAsync($"{Base}/items/{world.Item.Id:D}"), "getMaterialItem");
        Assert.Equal("Item", item.GetProperty("itemKind").GetString());
        await ErrorAsync(await client.GetAsync($"{Base}/items/{world.Product.Id:D}"), 404, "UNKNOWN_ITEM", "getMaterialItem");
    }

    // ------------------------------------------------------------------------------------------- getSku (§16.1 table)

    [Fact]
    public async Task S4_GetSku_item_level_is_the_item_itself()
    {
        var world = await SeedWorldAsync();

        var sku = await OkAsync(await Reader().GetAsync($"{Base}/skus/{world.Item.Id:D}?level=Item"), "getSku");

        Assert.Equal(world.Item.Id.ToString("D"), sku.GetProperty("skuId").GetString());
        Assert.Equal(world.Item.Id.ToString("D"), sku.GetProperty("itemId").GetString());
        Assert.Equal("Item", sku.GetProperty("skuLevel").GetString());
        Assert.Equal("Item", sku.GetProperty("itemKind").GetString());
        Assert.Equal("ACTIVE_INGREDIENT", sku.GetProperty("materialType").GetString());
        Assert.Equal("Own", sku.GetProperty("shelfLifeDaysSource").GetString());
        Assert.Equal(365, sku.GetProperty("retestDays").GetInt32());
        Assert.False(sku.GetProperty("serialControlled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("packUomCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("gtin").ValueKind);
    }

    [Fact]
    public async Task S4_GetSku_product_levels_follow_the_s3_table()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        var name = world.Product.GlobalProductName + " · 16 C62";

        var gsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"), "getSku");
        Assert.Equal("GlobalProduct", gsku.GetProperty("itemKind").GetString());
        Assert.Equal(world.Product.Id.ToString("D"), gsku.GetProperty("itemId").GetString());
        Assert.Equal(name, gsku.GetProperty("name").GetString());
        Assert.Equal("16", gsku.GetProperty("packQuantity").GetString());
        Assert.Equal("C62", gsku.GetProperty("baseUomId").GetString());
        Assert.Equal(730, gsku.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal("Own", gsku.GetProperty("shelfLifeDaysSource").GetString());
        Assert.Equal(JsonValueKind.Null, gsku.GetProperty("materialType").ValueKind);
        Assert.Equal(JsonValueKind.Null, gsku.GetProperty("retestDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, gsku.GetProperty("serialControlled").ValueKind);
        Assert.True(gsku.GetProperty("stockable").GetBoolean());

        // O-3: an LSKU's stock behaviour is its GSKU's, but its own serial control.
        var lsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}?level=Lsku"), "getSku");
        Assert.Equal(name + " · TR", lsku.GetProperty("name").GetString());
        Assert.Equal("TR", lsku.GetProperty("marketCode").GetString());
        Assert.Equal(730, lsku.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal("Gsku", lsku.GetProperty("shelfLifeDaysSource").GetString());
        Assert.Equal(["AMBIENT_15_25"], Strings(lsku.GetProperty("storageConditions")));
        Assert.True(lsku.GetProperty("serialControlled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, lsku.GetProperty("gtin").ValueKind);
        Assert.True(lsku.GetProperty("stockable").GetBoolean());

        // O-14 / D-2: the FG's base unit is the GSKU's; with no own shelf life it inherits the GSKU's; GTIN = sales box.
        var fg = await OkAsync(await client.GetAsync($"{Base}/skus/{world.FinishedGood.Id:D}?level=FinishedGood"), "getSku");
        Assert.Equal(name, fg.GetProperty("name").GetString());
        Assert.Equal("C62", fg.GetProperty("baseUomId").GetString());
        Assert.Equal("Gsku", fg.GetProperty("shelfLifeDaysSource").GetString());
        Assert.Equal(world.FinishedGood.Gtin, fg.GetProperty("gtin").GetString());
        Assert.True(fg.GetProperty("stockable").GetBoolean());
        Assert.False(fg.TryGetProperty("packQuantity", out _));
    }

    // O-25: a record whose required stock field is not filled answers null (SkuRef) and stockable false.
    [Fact]
    public async Task S4_GetSku_incomplete_gsku_answers_nulls_and_not_stockable()
    {
        var world = await SeedWorldAsync();
        var draft = await SeedGskuAsync(world.Revision.Id, 4, complete: false, ProductIdentityLifecycleStatus.Draft);

        var sku = await OkAsync(await Reader().GetAsync($"{Base}/skus/{draft.Id:D}?level=Gsku"), "getSku");

        Assert.Equal("Draft", sku.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("baseUomId").ValueKind);
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("shelfLifeDays").ValueKind);
        Assert.False(sku.TryGetProperty("shelfLifeDaysSource", out _));
        Assert.False(sku.TryGetProperty("storageConditions", out _));
        Assert.False(sku.GetProperty("stockable").GetBoolean());
    }

    // The sabotage target "an item without itemKind": every path that answers an item says so (no itemKind = Global Product).
    [Fact]
    public async Task S4_Item_answers_carry_itemKind_Item_on_every_path()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        var id = world.Item.Id.ToString("D");

        var answers = new[]
        {
            await OkAsync(await client.GetAsync($"{Base}/products/{id}"), "getProduct"),
            await OkAsync(await client.GetAsync($"{Base}/items/{id}"), "getMaterialItem"),
            await OkAsync(await client.GetAsync($"{Base}/skus/{id}?level=Item"), "getSku"),
            await OkAsync(await client.GetAsync($"{Base}/skus/{id}/product?level=Item"), "resolveSkuProduct")
        };
        var search = await OkAsync(await client.GetAsync($"{Base}/stockable-items?skuLevel=Item"), "searchStockableItems");
        var validate = await OkAsync(await client.PostAsync($"{Base}/validate",
            Json(new { refs = new object[] { new { itemId = id } } })), "validateRefs");

        Assert.All(answers, answer => Assert.Equal("Item", answer.GetProperty("itemKind").GetString()));
        Assert.Equal("Item", Row(search, id).GetProperty("itemKind").GetString());
        Assert.Equal("Item", validate.GetProperty("results")[0].GetProperty("itemKind").GetString());
    }

    // ------------------------------------------------------------------------------------------- resolveSkuProduct, uom

    [Fact]
    public async Task S4_ResolveSkuProduct_links_an_item_to_itself_and_a_product_sku_to_its_global_product()
    {
        var world = await SeedWorldAsync();
        var client = Reader();

        var item = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Item.Id:D}/product?level=Item"), "resolveSkuProduct");
        Assert.Equal(world.Item.Id.ToString("D"), item.GetProperty("itemId").GetString());
        foreach (var (id, level) in new[] { (world.Gsku.Id, "Gsku"), (world.Lsku.Id, "Lsku"), (world.FinishedGood.Id, "FinishedGood") })
        {
            var link = await OkAsync(await client.GetAsync($"{Base}/skus/{id:D}/product?level={level}"), "resolveSkuProduct");
            Assert.Equal(world.Product.Id.ToString("D"), link.GetProperty("itemId").GetString());
            Assert.Equal("GlobalProduct", link.GetProperty("itemKind").GetString());
            Assert.Equal(level, link.GetProperty("skuLevel").GetString());
        }
        // A level that is not the record's is "no such SKU".
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Item.Id:D}/product?level=Gsku"), 404, "UNKNOWN_SKU", "resolveSkuProduct");
    }

    [Fact]
    public async Task S4_GetSkuUom_answers_each_kinds_chain()
    {
        var world = await SeedWorldAsync();
        var client = Reader();

        var item = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Item.Id:D}/uom"), "getSkuUom");
        Assert.Equal("KGM", item.GetProperty("baseUomId").GetString());
        Assert.Equal(["GRM>KGM 1/1000"], Conversions(item));

        var gsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}/uom"), "getSkuUom");
        Assert.Equal("C62", gsku.GetProperty("baseUomId").GetString());
        Assert.Equal(["XBX>C62 16/1"], Conversions(gsku));

        var lsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}/uom"), "getSkuUom");
        Assert.Equal(Conversions(gsku), Conversions(lsku));

        var fg = await OkAsync(await client.GetAsync($"{Base}/skus/{world.FinishedGood.Id:D}/uom"), "getSkuUom");
        Assert.Equal("C62", fg.GetProperty("baseUomId").GetString());
        Assert.Equal(["XCS>XBX 48/1", "XBX>C62 16/1"], Conversions(fg));

        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Product.Id:D}/uom"), 404, "UNKNOWN_SKU", null);
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{Guid.NewGuid():D}/uom"), 404, "UNKNOWN_SKU", null);
    }

    // ------------------------------------------------------------------------------------------- §16.3 status

    [Theory]
    [InlineData(ItemLifecycleStatus.Draft, null, "Draft")]
    [InlineData(ItemLifecycleStatus.PendingActivation, ItemActivationKind.Activation, "Draft")]
    [InlineData(ItemLifecycleStatus.Active, null, "Active")]
    [InlineData(ItemLifecycleStatus.Deactivated, null, "Deactivated")]
    [InlineData(ItemLifecycleStatus.PendingActivation, ItemActivationKind.Reactivation, "Deactivated")]
    [InlineData(ItemLifecycleStatus.Retired, null, "Retired")]
    public async Task S4_Item_status_maps_as_16_3(ItemLifecycleStatus status, ItemActivationKind? kind, string expected)
    {
        var item = await SeedItemAsync("Status " + status + kind, status: status, activationKind: kind);
        var body = await OkAsync(await Reader().GetAsync($"{Base}/items/{item.Id:D}"), "getMaterialItem");
        Assert.Equal(expected, body.GetProperty("lifecycleStatus").GetString());
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.Draft, "Draft")]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval, "Draft")]
    [InlineData(ProductIdentityLifecycleStatus.IdentityApproved, "Active")]
    [InlineData(ProductIdentityLifecycleStatus.Retired, "Retired")]
    public async Task S4_Product_status_maps_as_O4_and_never_Archived(ProductIdentityLifecycleStatus status, string expected)
    {
        var world = await SeedWorldAsync();
        var gsku = await SeedGskuAsync(world.Revision.Id, 7, complete: true, status);
        var body = await OkAsync(await Reader().GetAsync($"{Base}/skus/{gsku.Id:D}?level=Gsku"), "getSku");
        Assert.Equal(expected, body.GetProperty("lifecycleStatus").GetString());
    }

    // ------------------------------------------------------------------------------------------- errors §16.4 / §13

    [Fact]
    public async Task S4_Unknown_ids_answer_v10_404s_in_the_contract_error_shape()
    {
        var world = await SeedWorldAsync();
        var client = Reader();

        await ErrorAsync(await client.GetAsync($"{Base}/products/{Guid.NewGuid():D}"), 404, "UNKNOWN_ITEM", "getProduct");
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{Guid.NewGuid():D}?level=Gsku"), 404, "UNKNOWN_SKU", "getSku");
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Lsku"), 404, "UNKNOWN_SKU", "getSku");
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Product.Id:D}?level=Gsku"), 404, "UNKNOWN_SKU", "getSku");
        // An unknown request value is 400 (open enums are for answers, not requests).
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Box"), 400, "INVALID_PARAMETER", null);
        await ErrorAsync(await client.GetAsync($"{Base}/products/not-a-uuid"), 400, "INVALID_UUID", null);
    }

    [Fact]
    public async Task S4_No_token_is_401_and_no_contract_permission_is_403()
    {
        var world = await SeedWorldAsync();
        var anonymous = host.Client(null);
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));
        // The screens' own read permissions do not open the contract (O-13).
        var screens = host.Client(host.Token(_tenant, _user, "mdm.items.read", "mdm.gskus.read", "mdm.global-products.read"));

        foreach (var path in Paths(world))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(screens, path)).StatusCode);
        }
    }

    // ------------------------------------------------------------------------------------------- searchStockableItems

    // The sabotage target "the search answers a stockable: false row".
    [Fact]
    public async Task S4_Search_answers_only_stockable_rows()
    {
        var world = await SeedWorldAsync();
        var notStockableItem = await SeedItemAsync("Not stockable", stockable: false);
        var incomplete = await SeedGskuAsync(world.Revision.Id, 5, complete: false, ProductIdentityLifecycleStatus.Draft,
            storedStockable: true);
        var notStockableLsku = await SeedLskuAsync(incomplete.Id, "DE", serial: true);

        var body = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems");
        var ids = Ids(body);

        Assert.Equal(
            new[] { world.Item.Id, world.Gsku.Id, world.Lsku.Id, world.FinishedGood.Id }.Select(x => x.ToString("D")).Order(),
            ids.Order());
        Assert.DoesNotContain(notStockableItem.Id.ToString("D"), ids);
        Assert.DoesNotContain(incomplete.Id.ToString("D"), ids);
        Assert.DoesNotContain(notStockableLsku.Id.ToString("D"), ids);
        Assert.Equal(4, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task S4_Search_is_paged_and_ordered_by_canonical_code()
    {
        await SeedWorldAsync();
        var client = Reader();

        var all = await OkAsync(await client.GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems");
        var codes = all.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("canonicalCode").GetString()!).ToArray();
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);

        var first = await OkAsync(await client.GetAsync($"{Base}/stockable-items?page=1&pageSize=3"), "searchStockableItems");
        var second = await OkAsync(await client.GetAsync($"{Base}/stockable-items?page=2&pageSize=3"), "searchStockableItems");
        Assert.Equal(codes, first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray())
            .Select(x => x.GetProperty("canonicalCode").GetString()!));
        Assert.Equal(25, (await OkAsync(await client.GetAsync($"{Base}/stockable-items"), "searchStockableItems"))
            .GetProperty("pageSize").GetInt32());

        var past = await OkAsync(await client.GetAsync($"{Base}/stockable-items?page=9&pageSize=25"), "searchStockableItems");
        Assert.Equal(0, past.GetProperty("items").GetArrayLength());
        Assert.Equal(4, past.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task S4_Search_filters_q_level_material_type_and_status()
    {
        var world = await SeedWorldAsync();
        var retired = await SeedItemAsync("Retired lactose", status: ItemLifecycleStatus.Retired);
        var client = Reader();

        // q: a code prefix (any case) or a part of the name (any case).
        var byCode = Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?q={world.Item.CanonicalCode[..6].ToLowerInvariant()}"), "searchStockableItems"));
        Assert.Contains(world.Item.Id.ToString("D"), byCode);
        var byName = Ids(await OkAsync(await client.GetAsync($"{Base}/stockable-items?q=TRIHYDRATE"), "searchStockableItems"));
        Assert.Equal([world.Item.Id.ToString("D")], byName);
        var byProductName = Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?q={Uri.EscapeDataString("16 c62 · tr")}"), "searchStockableItems"));
        Assert.Equal([world.Lsku.Id.ToString("D")], byProductName);

        Assert.Equal([world.FinishedGood.Id.ToString("D")], Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?skuLevel=FinishedGood"), "searchStockableItems")));
        Assert.Equal([world.Item.Id.ToString("D")], Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?materialType=ACTIVE_INGREDIENT"), "searchStockableItems")));
        Assert.Empty(Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?materialType=EXCIPIENT"), "searchStockableItems")));

        // No status: everything but Retired; status=Retired: only those.
        Assert.DoesNotContain(retired.Id.ToString("D"), Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?pageSize=100"), "searchStockableItems")));
        Assert.Equal([retired.Id.ToString("D")], Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?status=Retired"), "searchStockableItems")));
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    [InlineData("page=x")]
    [InlineData("skuLevel=Box")]
    [InlineData("materialType=CONSUMABLE")]
    [InlineData("status=Inactive")]
    public async Task S4_Search_refuses_a_bad_parameter_with_400_INVALID_PARAMETER(string query)
    {
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?{query}"), 400, "INVALID_PARAMETER",
            "searchStockableItems");
    }

    [Fact]
    public async Task S4_Search_refuses_q_over_200_characters()
    {
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?q={new string('a', 201)}"), 400,
            "INVALID_PARAMETER", "searchStockableItems");
        await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?q={new string('a', 200)}"), "searchStockableItems");
    }

    // ------------------------------------------------------------------------------------------- bulkReadSkus

    [Fact]
    public async Task S4_BulkRead_answers_in_request_order_with_notFound()
    {
        var world = await SeedWorldAsync();
        var retired = await SeedItemAsync("Retired povidone", status: ItemLifecycleStatus.Retired);
        var other = await SeedItemAsync("Other tenant item", tenant: Guid.NewGuid());
        var unknown = Guid.NewGuid();
        var ids = new[] { world.Lsku.Id, world.Product.Id, world.Item.Id, unknown, retired.Id, other.Id, world.FinishedGood.Id };

        var body = await OkAsync(await Reader().PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = ids.Select(x => x.ToString("D")) })), "bulkReadSkus");

        Assert.Equal(new[] { world.Lsku.Id, world.Item.Id, retired.Id, world.FinishedGood.Id }.Select(x => x.ToString("D")),
            body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("skuId").GetString()));
        Assert.Equal(["Lsku", "Item", "Item", "FinishedGood"],
            body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("skuLevel").GetString()!));
        Assert.Equal("Retired", body.GetProperty("items")[2].GetProperty("lifecycleStatus").GetString());
        Assert.Equal(new[] { world.Product.Id, unknown, other.Id }.Select(x => x.ToString("D")),
            Strings(body.GetProperty("notFound")));
    }

    // The sabotage target "bulk read accepts 201 ids".
    [Fact]
    public async Task S4_BulkRead_refuses_0_and_201_ids_and_accepts_200()
    {
        var client = Reader();
        var ids200 = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid().ToString("D")).ToArray();

        var ok = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read", Json(new { skuIds = ids200 })), "bulkReadSkus");
        Assert.Equal(200, ok.GetProperty("notFound").GetArrayLength());
        await ErrorAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = ids200.Append(Guid.NewGuid().ToString("D")) })), 400, "BATCH_LIMIT_EXCEEDED", "bulkReadSkus");
        await ErrorAsync(await client.PostAsync($"{Base}/skus/bulk-read", Json(new { skuIds = Array.Empty<string>() })), 400,
            "BATCH_LIMIT_EXCEEDED", "bulkReadSkus");
    }

    [Fact]
    public async Task S4_BulkRead_refuses_a_malformed_id_with_400_INVALID_UUID()
    {
        var client = Reader();
        await ErrorAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { Guid.NewGuid().ToString("D"), "not-a-uuid" } })), 400, "INVALID_UUID", "bulkReadSkus");
        await ErrorAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new object[] { 42 } })), 400, "INVALID_UUID", "bulkReadSkus");
    }

    // ------------------------------------------------------------------------------------------- validateRefs

    [Fact]
    public async Task S4_Validate_resolves_items_and_products_and_checks_level_and_link()
    {
        var world = await SeedWorldAsync();
        var item = world.Item.Id.ToString("D");
        var refs = new object[]
        {
            new { skuId = item, skuLevel = "Item" },
            new { skuId = item, skuLevel = "Gsku" },
            new { itemId = world.Product.Id.ToString("D") },
            new { itemId = item },
            new { itemId = world.Product.Id.ToString("D"), skuId = world.Lsku.Id.ToString("D"), skuLevel = "Lsku" },
            new { itemId = item, skuId = world.Lsku.Id.ToString("D"), skuLevel = "Lsku" },
            new { skuId = Guid.NewGuid().ToString("D"), skuLevel = "Gsku" }
        };

        var body = await OkAsync(await Reader().PostAsync($"{Base}/validate", Json(new { refs })), "validateRefs");
        var results = body.GetProperty("results").EnumerateArray().ToArray();

        Assert.Equal([true, false, true, true, true, false, false], results.Select(x => x.GetProperty("exists").GetBoolean()));
        Assert.Equal("Item", results[0].GetProperty("itemKind").GetString());
        Assert.Equal("Active", results[0].GetProperty("lifecycleStatus").GetString());
        Assert.False(results[1].TryGetProperty("itemKind", out _));
        Assert.Equal("GlobalProduct", results[2].GetProperty("itemKind").GetString());
        Assert.Equal("Item", results[3].GetProperty("itemKind").GetString());
        Assert.Equal("GlobalProduct", results[4].GetProperty("itemKind").GetString());
    }

    [Theory]
    [InlineData("""{"refs":[{}]}""")]
    [InlineData("""{"refs":[{"skuLevel":"Item"}]}""")]
    [InlineData("""{"refs":[{"itemId":"d4e5f6a7-0000-0000-0000-000000000003","skuLevel":"Item"}]}""")]
    [InlineData("""{"refs":[{"skuId":"nope","skuLevel":"Item"}]}""")]
    [InlineData("""{"refs":[{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","skuLevel":"Box"}]}""")]
    [InlineData("""{"refs":[{"itemId":"d4e5f6a7-0000-0000-0000-000000000003","extra":1}]}""")]
    [InlineData("""{"refs":[42]}""")]
    [InlineData("""{"refs":{}}""")]
    [InlineData("""{}""")]
    [InlineData("""not json""")]
    public async Task S4_Validate_refuses_a_bad_shape_with_400_INVALID_REF(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        await ErrorAsync(await Reader().PostAsync($"{Base}/validate", content), 400, "INVALID_REF", "validateRefs");
    }

    [Fact]
    public async Task S4_Validate_refuses_201_refs_and_accepts_200()
    {
        var client = Reader();
        var refs = Enumerable.Range(0, 200).Select(_ => new { itemId = Guid.NewGuid().ToString("D") }).ToArray();
        var ok = await OkAsync(await client.PostAsync($"{Base}/validate", Json(new { refs })), "validateRefs");
        Assert.Equal(200, ok.GetProperty("results").GetArrayLength());
        await ErrorAsync(await client.PostAsync($"{Base}/validate",
            Json(new { refs = refs.Append(new { itemId = Guid.NewGuid().ToString("D") }) })), 400, "BATCH_LIMIT_EXCEEDED", "validateRefs");
    }

    // ------------------------------------------------------------------------------------------- scope (§8.5)

    // The sabotage target "the scope guard is skipped on a product read": in an Enforced tenant a product row outside
    // the caller's legal-entity scope is "none" on every path, exactly as the screens' detail reads answer it.
    [Fact]
    public async Task S4_Enforced_scope_out_of_scope_product_rows_are_none_on_every_path()
    {
        var inScope = await SeedWorldAsync();
        var outScope = await SeedWorldAsync(itemName: "Out of scope item");
        await EnforceScopeAsync(inScope.Product.Id);
        var client = Reader();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/products/{inScope.Product.Id:D}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/skus/{inScope.Lsku.Id:D}?level=Lsku")).StatusCode);
        await ErrorAsync(await client.GetAsync($"{Base}/products/{outScope.Product.Id:D}"), 404, "UNKNOWN_ITEM", "getProduct");
        foreach (var (id, level) in new[] { (outScope.Gsku.Id, "Gsku"), (outScope.Lsku.Id, "Lsku"), (outScope.FinishedGood.Id, "FinishedGood") })
        {
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}?level={level}"), 404, "UNKNOWN_SKU", "getSku");
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}/product?level={level}"), 404, "UNKNOWN_SKU", "resolveSkuProduct");
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}/uom"), 404, "UNKNOWN_SKU", null);
        }
        var search = Ids(await OkAsync(await client.GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems"));
        Assert.Contains(inScope.Gsku.Id.ToString("D"), search);
        Assert.DoesNotContain(outScope.Gsku.Id.ToString("D"), search);
        Assert.DoesNotContain(outScope.Lsku.Id.ToString("D"), search);
        Assert.DoesNotContain(outScope.FinishedGood.Id.ToString("D"), search);
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { inScope.Gsku.Id, outScope.Gsku.Id, outScope.FinishedGood.Id }.Select(x => x.ToString("D")) })),
            "bulkReadSkus");
        Assert.Equal(new[] { outScope.Gsku.Id, outScope.FinishedGood.Id }.Select(x => x.ToString("D")), Strings(bulk.GetProperty("notFound")));
        var validate = await OkAsync(await client.PostAsync($"{Base}/validate", Json(new
        {
            refs = new object[] { new { itemId = outScope.Product.Id.ToString("D") }, new { skuId = outScope.Lsku.Id.ToString("D"), skuLevel = "Lsku" } }
        })), "validateRefs");
        Assert.All(validate.GetProperty("results").EnumerateArray(), x => Assert.False(x.GetProperty("exists").GetBoolean()));

        // Items are tenant-wide (Decision 6): both items answer, in scope or not.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/items/{outScope.Item.Id:D}")).StatusCode);
        Assert.Contains(outScope.Item.Id.ToString("D"), search);
    }

    // S4 FIX1 (B1), the sabotage target "the scope is resolved lazily again": when the product scope cannot be read, an
    // id that exists and an id that does not get the SAME answer on every path — the refusal says nothing about existence.
    [Theory]
    [InlineData("getProduct")]
    [InlineData("getSku")]
    [InlineData("getSku-no-level")]
    [InlineData("resolveSkuProduct")]
    [InlineData("getSkuUom")]
    [InlineData("bulkReadSkus")]
    [InlineData("validateRefs")]
    public async Task S4_FIX1_B1_A_scope_that_cannot_be_read_answers_an_existing_and_a_random_id_alike(string path)
    {
        var world = await SeedWorldAsync();
        await EnforceScopeAsync(world.Product.Id, answerCandidates: false);
        var client = Reader();
        var existing = path == "getProduct" ? world.Product.Id : world.Gsku.Id;

        var known = await CallAsync(client, path, existing, world.Item.Id);
        var random = await CallAsync(client, path, Guid.NewGuid(), world.Item.Id);

        Assert.Equal(503, (int)known.StatusCode);
        Assert.Equal((int)known.StatusCode, (int)random.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE", await ErrorCodeAsync(known));
        Assert.Equal(await ErrorCodeAsync(known), await ErrorCodeAsync(random));
    }

    private static Task<HttpResponseMessage> CallAsync(HttpClient client, string path, Guid id, Guid item) => path switch
    {
        "getProduct" => client.GetAsync($"{Base}/products/{id:D}"),
        "getSku" => client.GetAsync($"{Base}/skus/{id:D}?level=Gsku"),
        "getSku-no-level" => client.GetAsync($"{Base}/skus/{id:D}"),
        "resolveSkuProduct" => client.GetAsync($"{Base}/skus/{id:D}/product?level=Gsku"),
        "getSkuUom" => client.GetAsync($"{Base}/skus/{id:D}/uom"),
        // An item next to it does not change the answer either.
        "bulkReadSkus" => client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { item.ToString("D"), id.ToString("D") } })),
        _ => client.PostAsync($"{Base}/validate", Json(new
        {
            refs = new object[] { new { itemId = item.ToString("D") }, new { skuId = id.ToString("D"), skuLevel = "Gsku" } }
        }))
    };

    // The item stays independent of the product scope: looked up first, and the scope is never asked for it.
    [Fact]
    public async Task S4_FIX1_B1_Item_reads_do_not_ask_the_product_scope()
    {
        var world = await SeedWorldAsync();
        await EnforceScopeAsync(world.Product.Id, answerCandidates: false);
        var client = Reader();
        var item = world.Item.Id.ToString("D");

        await OkAsync(await client.GetAsync($"{Base}/items/{item}"), "getMaterialItem");
        await OkAsync(await client.GetAsync($"{Base}/products/{item}"), "getProduct");
        await OkAsync(await client.GetAsync($"{Base}/skus/{item}"), "getSku");
        await OkAsync(await client.GetAsync($"{Base}/skus/{item}?level=Item"), "getSku");
        await OkAsync(await client.GetAsync($"{Base}/skus/{item}/uom"), "getSkuUom");
        await OkAsync(await client.GetAsync($"{Base}/stockable-items?skuLevel=Item"), "searchStockableItems");
        await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read", Json(new { skuIds = new[] { item } })), "bulkReadSkus");
        Assert.Equal(503, (int)(await client.GetAsync($"{Base}/stockable-items")).StatusCode);
    }

    // The provider's own refusals: 403 stays a named 403; its 401 and 504 are an outage to the consumer (a 401 here is not
    // "your token expired"), 503 PRODUCT_MASTER_UNAVAILABLE.
    [Theory]
    [InlineData(403, "LEGAL_ENTITY_SCOPE_FORBIDDEN", 403, "LEGAL_ENTITY_SCOPE_FORBIDDEN")]
    [InlineData(401, "TRUSTED_SUBJECT_REQUIRED", 503, "PRODUCT_MASTER_UNAVAILABLE")]
    [InlineData(504, "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT", 503, "PRODUCT_MASTER_UNAVAILABLE")]
    [InlineData(503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE", 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    public async Task S4_FIX1_The_scope_providers_refusals_answer_by_name(int providerStatus, string providerCode,
        int status, string code)
    {
        var world = await SeedWorldAsync();
        await EnforceScopeAsync(world.Product.Id);
        host.Scopes.Refuse(_tenant, providerStatus, providerCode);

        await ErrorAsync(await Reader().GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"), status, code, null);
        await ErrorAsync(await Reader().GetAsync($"{Base}/skus/{Guid.NewGuid():D}?level=Gsku"), status, code, null);
    }

    // An Enforced policy that names other legal entities: the product is out of the caller's scope, "none" everywhere.
    [Fact]
    public async Task S4_FIX1_A_policy_naming_other_legal_entities_keeps_the_product_out_of_scope()
    {
        var world = await SeedWorldAsync();
        await EnforceScopeAsync(world.Product.Id, policyLegalEntities: [Guid.NewGuid()]);
        var client = Reader();

        await ErrorAsync(await client.GetAsync($"{Base}/products/{world.Product.Id:D}"), 404, "UNKNOWN_ITEM", "getProduct");
        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}"), 404, "UNKNOWN_SKU", "getSku");
        Assert.DoesNotContain(world.Gsku.Id.ToString("D"),
            Ids(await OkAsync(await client.GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems")));
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { world.Gsku.Id.ToString("D") } })), "bulkReadSkus");
        Assert.Equal([world.Gsku.Id.ToString("D")], Strings(bulk.GetProperty("notFound")));
    }

    // Inherited, measured and documented (not changed here): a platform_admin token passes [HasPermission] without the
    // contract permission (PermissionAuthorizationHandler) and reads the tenant its token names, scope rules as for anyone.
    [Fact]
    public async Task S4_FIX1_A_platform_admin_reads_the_tenant_its_token_names()
    {
        var world = await SeedWorldAsync();
        var admin = host.Client(host.TokenOf("platform_admin", _tenant, Guid.NewGuid()));
        await OkAsync(await admin.GetAsync($"{Base}/items/{world.Item.Id:D}"), "getMaterialItem");
        await OkAsync(await admin.GetAsync($"{Base}/skus/{world.Gsku.Id:D}"), "getSku");
    }

    // B2 (BL-592) made visible: the test scope provider answers any permission key — a real Enforced tenant refuses
    // mdm.product-master.read until Platform's trusted scope list holds it. This test pins the fake's behaviour by name so
    // nobody reads the enforced-scope tests above as proof of the live path.
    [Fact]
    public async Task S4_FIX1_B2_Note_the_test_scope_provider_accepts_every_permission_key()
    {
        var legalEntity = Guid.NewGuid();
        host.Scopes.Answer(_tenant, legalEntity);
        var answer = await host.Scopes.ResolveAsync(_tenant, _user, "product-item-sku-master", "mdm.product-master.read");
        var invented = await host.Scopes.ResolveAsync(_tenant, _user, "product-item-sku-master", "no.such.permission");
        Assert.True(answer.IsSuccessful);
        Assert.True(invented.IsSuccessful);
    }

    // A store that cannot be reached is a coded 503 (fail closed), never an empty answer.
    [Fact]
    public async Task S4_Store_that_cannot_be_reached_is_503()
    {
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        var handler = ActivatorUtilities.CreateInstance<ProductMasterQueryHandler>(scope.ServiceProvider,
            (IItemRepository)new UnreachableItems(),
            (IProductMasterReadStore)new UnreachableStore());

        var single = await handler.Handle(new GetProductMasterItemQuery(Guid.NewGuid().ToString("D")), default);
        var search = await handler.Handle(new SearchProductMasterStockableItemsQuery(null, null, null, null, null, null), default);

        Assert.Equal(503, single.StatusCode);
        Assert.Equal(ProductMasterContract.Unavailable, single.Errors!.Single());
        Assert.Equal(503, search.StatusCode);
    }

    // ------------------------------------------------------------------------------------------- tenant isolation

    // The sabotage target "another tenant's record is read": every path answers it as "none".
    [Fact]
    public async Task S4_Another_tenants_records_are_none_on_every_path()
    {
        var other = Guid.NewGuid();
        var theirs = await SeedWorldAsync(tenant: other);
        await SeedWorldAsync();
        var client = Reader();

        await ErrorAsync(await client.GetAsync($"{Base}/products/{theirs.Item.Id:D}"), 404, "UNKNOWN_ITEM", "getProduct");
        await ErrorAsync(await client.GetAsync($"{Base}/products/{theirs.Product.Id:D}"), 404, "UNKNOWN_ITEM", "getProduct");
        await ErrorAsync(await client.GetAsync($"{Base}/items/{theirs.Item.Id:D}"), 404, "UNKNOWN_ITEM", "getMaterialItem");
        foreach (var (id, level) in new[] { (theirs.Item.Id, "Item"), (theirs.Gsku.Id, "Gsku"), (theirs.Lsku.Id, "Lsku"), (theirs.FinishedGood.Id, "FinishedGood") })
        {
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}?level={level}"), 404, "UNKNOWN_SKU", "getSku");
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}/product?level={level}"), 404, "UNKNOWN_SKU", "resolveSkuProduct");
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{id:D}/uom"), 404, "UNKNOWN_SKU", null);
        }
        var theirIds = new[] { theirs.Item.Id, theirs.Gsku.Id, theirs.Lsku.Id, theirs.FinishedGood.Id }.Select(x => x.ToString("D")).ToArray();
        var search = Ids(await OkAsync(await client.GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems"));
        Assert.Empty(search.Intersect(theirIds));
        Assert.Equal(4, search.Length);
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read", Json(new { skuIds = theirIds })), "bulkReadSkus");
        Assert.Equal(theirIds, Strings(bulk.GetProperty("notFound")));
        var validate = await OkAsync(await client.PostAsync($"{Base}/validate",
            Json(new { refs = new object[] { new { itemId = theirs.Item.Id.ToString("D") }, new { itemId = theirs.Product.Id.ToString("D") } } })),
            "validateRefs");
        Assert.All(validate.GetProperty("results").EnumerateArray(), x => Assert.False(x.GetProperty("exists").GetBoolean()));
    }

    // ------------------------------------------------------------------------------------------- v1.0 backward compatibility

    // A v1.0 consumer reads the same fields with the same names and types (non-strict: v1.1 adds fields).
    [Fact]
    public async Task S4_V10_fields_come_back_with_the_same_names_and_types()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        var v10 = ProductMasterContractSchema.V10;

        var answers = new (string Operation, HttpResponseMessage Response)[]
        {
            ("getProduct", await client.GetAsync($"{Base}/products/{world.Product.Id:D}")),
            ("getSku", await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku")),
            ("getSku", await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}?level=Lsku")),
            ("getSku", await client.GetAsync($"{Base}/skus/{world.FinishedGood.Id:D}?level=FinishedGood")),
            ("getSkuUom", await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}/uom")),
            ("validateRefs", await client.PostAsync($"{Base}/validate", Json(new
            {
                refs = new object[] { new { itemId = world.Product.Id.ToString("D") }, new { skuId = world.Lsku.Id.ToString("D"), skuLevel = "Lsku" } }
            })))
        };
        foreach (var (operation, response) in answers)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var failures = v10.ValidateResponse(operation, 200, document.RootElement, strict: false);
            Assert.True(failures.Count == 0, $"{operation}: {string.Join("; ", failures)}");
        }
        var lsku = await OkAsync(await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}?level=Lsku"), "getSku");
        Assert.Equal("C62", lsku.GetProperty("packUomCode").GetString());
        Assert.Equal("16", lsku.GetProperty("packQuantity").GetString());
    }

    // ------------------------------------------------------------------------------------------- audit, header

    // AUD-001 §21: reads are not audited.
    [Fact]
    public async Task S4_Contract_reads_are_not_audited()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        foreach (var path in Paths(world)) Assert.True((await Send(client, path)).IsSuccessStatusCode, path.Path);
        Assert.DoesNotContain(host.Audit.Forwarded, x => x.RequestType.Contains("ProductMaster", StringComparison.Ordinal));
    }

    // §8.5: the incoming token is ~35 KB. Through the whole pipeline (JWT, tenant, permission, controller) it is a 200.
    [Fact]
    public async Task S4_A_35KB_token_is_answered_200()
    {
        var world = await SeedWorldAsync();
        var token = BigToken(35 * 1024);
        Assert.InRange(token.Length, 35 * 1024, 37 * 1024);

        var response = await host.Client(token).GetAsync($"{Base}/items/{world.Item.Id:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The header limit is Kestrel's, which the in-process test server does not apply. MDM's own Kestrel options (read
    // from the real host) are put on a real Kestrel listener and a ~35 KB Authorization header is sent over the wire:
    // 200 with MDM's limit; the default (32 KB) would answer 431 — the same request proves the gauge can see the failure.
    [Fact]
    public async Task S4_Mdm_kestrel_header_limit_takes_a_35KB_token_over_the_wire()
    {
        var mdmLimits = host.Factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits;
        Assert.True(mdmLimits.MaxRequestHeadersTotalSize >= 64 * 1024,
            $"MDM accepts only {mdmLimits.MaxRequestHeadersTotalSize} bytes of headers (§8.5: 64 KB)");

        var token = BigToken(35 * 1024);
        Assert.Equal(HttpStatusCode.OK, await OverTheWireAsync(mdmLimits.MaxRequestHeadersTotalSize, token));
        Assert.Equal(HttpStatusCode.RequestHeaderFieldsTooLarge,
            await OverTheWireAsync(new KestrelServerOptions().Limits.MaxRequestHeadersTotalSize, token));
    }

    private static async Task<HttpStatusCode> OverTheWireAsync(int headersTotalSize, string token)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.Limits.MaxRequestHeadersTotalSize = headersTotalSize);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/", () => Results.Ok());
        // S4 FIX1: every step of the real listener is bounded — it never hangs the run.
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(bound.Token);
        try
        {
            var address = app.Urls.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await client.GetAsync("/", bound.Token)).StatusCode;
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(stop.Token);
        }
    }

    private string BigToken(int targetLength)
    {
        // Permission claims as AuthService writes them, many of them, until the token is the measured size.
        var permissions = new List<string> { ProductMasterContract.ReadPermission };
        var index = 0;
        string token;
        do
        {
            permissions.AddRange(Enumerable.Range(0, 20).Select(_ => $"mdm.padding-{index++:D5}.read"));
            token = host.Token(_tenant, _user, permissions.ToArray());
        } while (token.Length < targetLength);
        return token;
    }

    // ------------------------------------------------------------------------------------------- seed

    private sealed record World(GlobalProduct Product, ProductDefinitionRevision Revision, Gsku Gsku, Lsku Lsku,
        FinishedGood FinishedGood, Item Item);

    private async Task<World> SeedWorldAsync(Guid? tenant = null, string itemName = "Amoxicillin trihydrate")
    {
        var tenantId = tenant ?? _tenant;
        var product = new GlobalProduct
        {
            TenantId = tenantId, CanonicalCode = Code("GP"), GlobalProductName = "Amoxicillin 500 mg " + Guid.NewGuid().ToString("N")[..6],
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        product.GlobalProductNameNormalized = product.GlobalProductName.ToUpperInvariant();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
        var revision = new ProductDefinitionRevision
        {
            TenantId = tenantId, GlobalProductId = product.Id, RevisionIdentifier = "R1",
            CreationCommandId = "rev-" + Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        var gsku = await SeedGskuAsync(revision.Id, 16, complete: true, ProductIdentityLifecycleStatus.IdentityApproved, tenant: tenantId);
        var lsku = await SeedLskuAsync(gsku.Id, "TR", serial: true, tenant: tenantId);
        var finishedGood = new FinishedGood
        {
            TenantId = tenantId, GskuId = gsku.Id, CanonicalCode = Code("FG"), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = "fg-" + Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            UomConversions = [new ItemUomConversion { FromUom = "XCS", ToUom = "XBX", Numerator = "48", Denominator = "1" }],
            StorageConditionCodes = ["AMBIENT_15_25"], LotControlled = true, SerialControlled = true, Stockable = true,
            Gtin = Gtin()
        };
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(finishedGood);
        var item = await SeedItemAsync(itemName, tenant: tenantId);
        return new(product, revision, gsku, lsku, finishedGood, item);
    }

    private async Task<Gsku> SeedGskuAsync(Guid revisionId, decimal packQuantity, bool complete,
        ProductIdentityLifecycleStatus status, bool? storedStockable = null, Guid? tenant = null)
    {
        var gsku = new Gsku
        {
            TenantId = tenant ?? _tenant, ProductDefinitionRevisionId = revisionId, CanonicalCode = Code("GS"),
            CodeReservationId = Guid.NewGuid(), CreationCommandId = "gs-" + Guid.NewGuid().ToString("N"),
            PackApplicabilityCode = "PACK-" + Guid.NewGuid().ToString("N")[..8], PackQuantity = packQuantity, PackUomCode = "C62",
            LifecycleStatus = status,
            BaseUomCode = complete ? "C62" : null,
            ShelfLifeDays = complete ? 730 : null,
            StorageConditionCodes = complete ? ["AMBIENT_15_25"] : null,
            LotControlled = complete ? true : null,
            Stockable = storedStockable ?? complete
        };
        await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        return gsku;
    }

    private async Task<Lsku> SeedLskuAsync(Guid gskuId, string market, bool serial, Guid? tenant = null)
    {
        var lsku = new Lsku
        {
            TenantId = tenant ?? _tenant, GskuId = gskuId, CanonicalCode = Code("LS"), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = "ls-" + Guid.NewGuid().ToString("N"), MarketCode = market,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, SerialControlled = serial
        };
        await host.Database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        return lsku;
    }

    private async Task<Item> SeedItemAsync(string name, Guid? tenant = null, bool stockable = true,
        ItemLifecycleStatus status = ItemLifecycleStatus.Active, ItemActivationKind? activationKind = null)
    {
        var item = new Item
        {
            TenantId = tenant ?? _tenant, CanonicalCode = Code("IT"), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = "it-" + Guid.NewGuid().ToString("N"), Name = name, NameNormalized = name.ToUpperInvariant(),
            NameKey = "ACTIVE_INGREDIENT|" + Guid.NewGuid().ToString("N"), MaterialType = ItemMaterialType.ActiveIngredient,
            BaseUomCode = "KGM",
            UomConversions = [new ItemUomConversion { FromUom = "GRM", ToUom = "KGM", Numerator = "1", Denominator = "1000" }],
            StorageConditionCodes = ["AMBIENT_15_25", "PROTECT_FROM_MOISTURE"],
            StorageConditionKinds = new() { ["AMBIENT_15_25"] = "TEMPERATURE", ["PROTECT_FROM_MOISTURE"] = "PROTECTION" },
            LotControlled = true, Stockable = stockable, ShelfLifeDays = 1825, RetestDays = 365, MinRemainingShelfLifeDays = 90,
            LifecycleStatus = status, ActivationKind = activationKind
        };
        await host.Database.GetCollection<Item>("mdm_items").InsertOneAsync(item);
        return item;
    }

    private async Task EnforceScopeAsync(Guid onlyProduct, bool answerCandidates = true, Guid[]? policyLegalEntities = null)
    {
        var legalEntity = Guid.NewGuid();
        await host.Database.GetCollection<LegalEntity>("mdm_legal_entities").InsertOneAsync(new LegalEntity
        {
            Id = legalEntity, TenantId = _tenant, Code = "LE-" + legalEntity.ToString("N")[..8], LegalName = "Scope",
            OperationalStatus = LegalEntityOperationalStatus.Active, IsDeleted = false
        });
        if (answerCandidates) host.Scopes.Answer(_tenant, legalEntity);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, onlyProduct, Guid.NewGuid(),
                policyLegalEntities is null ? ProductLegalEntityScopeMode.GroupWide : ProductLegalEntityScopeMode.Scoped,
                policyLegalEntities ?? [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private static string Code(string prefix) =>
        $"{prefix}-{Random.Shared.NextInt64(100_000_000_000, 999_999_999_999):D12}";

    // A sales-box GTIN-14 with its GS1 check digit.
    private static string Gtin()
    {
        var body = "0869" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString("D9");
        var sum = body.Reverse().Select((c, i) => (c - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
        return body + (10 - sum % 10) % 10;
    }

    // ------------------------------------------------------------------------------------------- helpers

    private sealed record Call(HttpMethod Method, string Path, object? Body = null);

    private static IEnumerable<Call> Paths(World world) =>
    [
        new(HttpMethod.Get, $"{Base}/products/{world.Item.Id:D}"),
        new(HttpMethod.Get, $"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"),
        new(HttpMethod.Get, $"{Base}/skus/{world.Lsku.Id:D}/product?level=Lsku"),
        new(HttpMethod.Get, $"{Base}/skus/{world.FinishedGood.Id:D}/uom"),
        new(HttpMethod.Get, $"{Base}/items/{world.Item.Id:D}"),
        new(HttpMethod.Get, $"{Base}/stockable-items"),
        new(HttpMethod.Post, $"{Base}/skus/bulk-read", new { skuIds = new[] { world.Item.Id.ToString("D") } }),
        new(HttpMethod.Post, $"{Base}/validate", new { refs = new object[] { new { itemId = world.Item.Id.ToString("D") } } })
    ];

    private static Task<HttpResponseMessage> Send(HttpClient client, Call call)
    {
        var request = new HttpRequestMessage(call.Method, call.Path);
        if (call.Body is not null) request.Content = JsonContent.Create(call.Body);
        return client.SendAsync(request);
    }

    private static JsonContent Json(object value) => JsonContent.Create(value);

    // Every success answer is validated against the v1.1 contract schema of its operation (strict).
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, string operationId)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {text}");
        using var document = JsonDocument.Parse(text);
        var failures = ProductMasterContractSchema.V11.ValidateResponse(operationId, 200, document.RootElement)
            .Concat(ProductMasterContractSchema.StatusFailures(document.RootElement)).ToList();
        Assert.True(failures.Count == 0, $"{operationId} answer is not the contract's: {string.Join("; ", failures)}\n{text}");
        return document.RootElement.Clone();
    }

    // Every refusal is the contract's Error shape; when the operation declares the status, against its schema too.
    private static async Task ErrorAsync(HttpResponseMessage response, int status, string code, string? operationId)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"expected {status} {code}, got {(int)response.StatusCode}: {text}");
        using var document = JsonDocument.Parse(text);
        var schema = ProductMasterContractSchema.V11;
        var failures = operationId is not null
            ? schema.ValidateResponse(operationId, status, document.RootElement)
            : schema.ValidateAgainst("Error", document.RootElement);
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("v1.1", document.RootElement.GetProperty("contractVersion").GetString());
        Assert.True(Guid.TryParse(document.RootElement.GetProperty("error").GetProperty("correlationId").GetString(), out _));
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private static string[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("skuId").GetString()!).ToArray();

    private static JsonElement Row(JsonElement page, string skuId) =>
        page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("skuId").GetString() == skuId);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static string[] Conversions(JsonElement uom) => uom.GetProperty("conversions").EnumerateArray()
        .Select(x => $"{x.GetProperty("fromUom").GetString()}>{x.GetProperty("toUom").GetString()} " +
                     $"{x.GetProperty("numerator").GetString()}/{x.GetProperty("denominator").GetString()}")
        .ToArray();

    private sealed class UnreachableItems : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new TimeoutException("store unreachable");
        public Task<Item?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<Item?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<bool> NameKeyTakenAsync(string nameKey, Guid? exceptItemId, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<ItemPage> GetPageAsync(ItemPageRequest request, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<ItemWriteResult> CreateDraftAsync(Item item, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ItemWriteResult> ReplaceAsync(Item item, int expectedVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnreachableStore : IProductMasterReadStore
    {
        public Task<IReadOnlyList<Item>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<IReadOnlyList<Lsku>> GetLskusByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<IReadOnlyList<FinishedGood>> GetFinishedGoodsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<ProductMasterStockablePage> SearchStockableAsync(ProductMasterStockableSearch search, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
        public Task<ProductMasterProductFlags> GetLiveSkuFlagsOfGlobalProductAsync(Guid globalProductId, CancellationToken cancellationToken = default) =>
            throw new TimeoutException();
    }
}
