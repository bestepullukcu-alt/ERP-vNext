using System.Text.Json;
using Diten.InventoryService.Domain.Expiry.Enums;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>MOD-0178 DL-5 — the remaining AC-29 / AC-72 / AC-77 cases on the REAL host (split packages, the short pick shipped, the reasoned override).</summary>
public sealed class OutboundFlowEdgeTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundFlowEdgeTests(WarehouseApiFactory factory) => _factory = factory;

    /// <summary>AC-29: a 30 picked allocation split over two packages (20 + 10) → both close → Packed; the frozen detail lists both packages.</summary>
    [Fact]
    public async Task AC29_a_split_over_two_packages_is_accepted()
    {
        var (s, f, _, _) = await PickTaskTests.FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        await f.PickAllAsync(id);
        var a = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 20))), 201);
        var b = await Expect(f.CreatePackageAsync(id, OutboundFlow.Contents((1, 10))), 201);
        await Expect(f.ClosePackageAsync(a.GetProperty("packageId").GetString()!, OutboundFlow.ETag(a)), 200);
        Assert.Equal("Picked", (await f.ViewAsync(id)).GetProperty("status").GetString());
        await Expect(f.ClosePackageAsync(b.GetProperty("packageId").GetString()!, OutboundFlow.ETag(b)), 200);
        Assert.Equal("Packed", (await f.ViewAsync(id)).GetProperty("status").GetString());
        await Expect(f.ReleaseAsync(id), 200);
        var frozen = JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement;
        Assert.Equal(2, frozen.GetProperty("packages").GetArrayLength());
    }

    /// <summary>AC-72: 20 of 30 picked + short close → a 20 package → Packed → ReadyToShip with quantity 20 → Shipped → the remaining reservation RELEASED.</summary>
    [Fact]
    public async Task AC72_a_short_pick_ships_20_and_the_rest_of_the_reservation_is_released()
    {
        var (s, f, l1, _) = await PickTaskTests.FS2Async(_factory);
        var outbound = await s.CreatedAsync(s.Line(30));
        var id = OutboundScenario.Id(outbound);
        var task = (await f.PickTasksAsync(id)).Single();
        await Expect(f.CloseShortAsync(await f.ConfirmedAsync(task, 20)), 200);
        await f.PackAllAsync(id);
        await Expect(f.ReleaseAsync(id), 200);
        var line = Assert.Single(JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal("20.000", line.GetProperty("quantity").GetString());
        var reservationId = OutboundFlow.Allocs(outbound).Single().GetProperty("reservationId").GetString()!;
        await f.IssueAsync(20, s.S01, l1, reservationId);
        f.Dispatched((s.Sku, l1, s.S01, 20m));
        await Expect(f.SyncAsync(id), 200);
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(reservationId))!.Status);
        Assert.Empty(f.PutawayTasks("ShortDispatch"));                                   // 20 picked, 20 issued
    }

    /// <summary>AC-77: an allocation whose own lot override carried a reason is not asked again at release, even with an earlier free lot.</summary>
    [Fact]
    public async Task AC77_a_reasoned_lot_override_is_not_asked_again_at_release()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var f = new OutboundFlow(s);
        s.Policy(FefoOutboundMode.RequireReason);
        var l1 = s.Lot("2027-01-31");
        var l2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(30, s.A02, l1);
        await s.ReceiveAsync(30, s.A01, l2);
        var id = await f.PackedAsync(s.Line(30, lotOverride: l2, reason: "Musteri L2 istedi"));
        await Expect(f.ReleaseAsync(id), 200);                                            // L1 is still earlier and free — no second question
        var line = Assert.Single(JsonDocument.Parse(await f.FrozenTextAsync(id)).RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal("Musteri L2 istedi", line.GetProperty("fefoBypassReason").GetString());
    }
}
