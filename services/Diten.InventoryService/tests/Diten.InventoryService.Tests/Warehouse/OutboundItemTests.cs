using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-4 AC-33 (i), (iii), (iv) (pack §8.24, W-6) — an Item-level outbound: the item's own SKU (<c>skuId = itemId</c>), allocated by the same
/// rules, FU03 reservations at <c>skuLevel Item</c>; every resolution refusal writes nothing anywhere (outbound, reservation, central row, local
/// event); Item and other levels never mix in one outbound. Fixture item K (lot controlled, Active), KL1 (2027-01-31) 20 on A-01, KL2
/// (2027-03-31) 20 on A-02. (The suite's product stub gives the item base unit <c>EA</c>; the pack's <c>KG</c> example is the same rule.)
/// </summary>
public sealed class OutboundItemTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundItemTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(OutboundScenario S, string KL1, string KL2)> WorldAsync()
    {
        var s = await new OutboundScenario(_factory, level: SkuLevel.Item).ReadyAsync();
        var kl1 = s.Lot("2027-01-31");
        var kl2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(20, s.A01, kl1);
        await s.ReceiveAsync(20, s.A02, kl2);
        return (s, kl1, kl2);
    }

    private static void NothingWritten(OutboundScenario s)
    {
        Assert.Empty(s.StoredOutbounds());
        Assert.Empty(s.OutboundEvents());
        Assert.Empty(s.Reservations());
        Assert.Empty(s.CentralRows("CreateOutboundShipmentCommand"));
    }

    [Fact]
    public async Task AC33_i_an_item_line_without_skuId_resolves_to_the_item_itself_and_reserves_at_Item_level()
    {
        var (s, kl1, kl2) = await WorldAsync();

        var outbound = await s.CreatedAsync(Line("K-1", 30, s.Item, null, SkuLevel.Item));

        var line = outbound.GetProperty("lines")[0];
        Assert.Equal(s.Item.ToString("D"), line.GetProperty("item").GetProperty("skuId").GetString());
        Assert.Equal("Item", line.GetProperty("item").GetProperty("skuLevel").GetString());
        Assert.Equal([(kl1, 20m, 1), (kl2, 10m, 2)], AllocationSummary(outbound));
        Assert.True(outbound.GetProperty("itemLevel").GetBoolean());
        var reservations = s.Reservations(OutboundScenario.Id(outbound));
        Assert.Equal(2, reservations.Count);
        Assert.All(reservations, r =>
        {
            Assert.Equal("Item", r["SkuLevel"].AsString);
            Assert.Equal(s.Item.ToString("D"), r["SkuId"].ToString());
        });
    }

    /// <summary>Sabotage (cp): the given skuId used as is (no D-SKU-LINK equality) → the SkuIdMismatch outbound opens (red).</summary>
    [Fact]
    public async Task AC33_iii_each_resolution_refusal_writes_nothing()
    {
        var (s, _, _) = await WorldAsync();

        async Task Refused(Dictionary<string, object?> line, string reason)
        {
            var error = await Expect(s.CreateAsync(s.Body(line)), 422);
            Assert.Equal("WMS_ITEM_NOT_RESOLVABLE", Code(error));
            Assert.Equal(reason, Details(error).GetProperty("reason").GetString());
            NothingWritten(s);
        }

        await Refused(Line("K-1", 5, s.Item, Guid.NewGuid(), SkuLevel.Item), "SkuIdMismatch");
        await Refused(Line("K-1", 5, Guid.NewGuid(), null, SkuLevel.Item), "UnknownItemSku");

        _factory.Products.SetStockable(s.Item, false);
        await Refused(Line("K-1", 5, s.Item, null, SkuLevel.Item), "NotStockable");
        _factory.Products.SetStockable(s.Item, null);

        _factory.Products.SetLifecycle(s.Item, "Retired");
        await Refused(Line("K-1", 5, s.Item, null, SkuLevel.Item), "LifecycleBlocked");
        _factory.Products.SetLifecycle(s.Item, "Active");

        Assert.Empty(s.P.W.Stored(WarehouseCollections.OutboundEvents));
    }

    [Fact]
    public async Task AC33_iv_item_and_gsku_lines_never_share_an_outbound()
    {
        var (s, _, _) = await WorldAsync();
        var gskuItem = Guid.NewGuid();
        var gsku = Guid.NewGuid();
        _factory.Products.AddItem(gskuItem, lotControlled: true);
        _factory.Products.AddSku(gsku, SkuLevel.Gsku, gskuItem);

        var error = await Expect(s.CreateAsync(s.Body(Line("K-1", 5, s.Item, null, SkuLevel.Item), Line("G-1", 5, gskuItem, gsku, SkuLevel.Gsku))), 422);

        Assert.Equal("WMS_OUTBOUND_LEVEL_MIXED", Code(error));
        NothingWritten(s);
    }
}
