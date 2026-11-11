using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-37 (§8.2 "Net 0 erken dönüşü yalnız SKU düzeyi içindir", §8.4) — a WAREHOUSE_TRANSFER nets 0 at SKU level but moves
/// stock between warehouses: the source warehouse (and lot) guard still runs, so reserved stock cannot leave its warehouse. Fixture: W1 50
/// (warehouse reservation 40), W2 0 (the target), W3 50 (so the SKU is never the limit).
/// </summary>
public sealed class WarehouseTransferGuardTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public WarehouseTransferGuardTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>S-30: the SKU's net-0 early return applied to every level → the 11 is transferred, red.</summary>
    [Fact]
    public async Task Transfer_out_of_reserved_warehouse_is_refused()
    {
        var w = new ScopedReservationWorld(_factory, lotControlled: false);
        await w.ReceiveAsync("50", null);
        var (w3, w3Bin) = w.AddWarehouse();
        await w.ReceiveIntoAsync("50", null, w3, w3Bin);
        await w.ReserveOkAsync("40", w.W);
        var w1 = w.Scope(w.W);
        var w2 = w.Scope(w.W2);
        var sku = w.SkuPosition();

        var refused = await w.PostAsync(w.WarehouseTransfer("11", null), 422);
        Assert.Equal(("INSUFFICIENT_STOCK", "WAREHOUSE"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));
        Assert.Equal((w1, w2), (w.Scope(w.W), w.Scope(w.W2)));

        await w.PostAsync(w.WarehouseTransfer("10", null), 201);
        Assert.Equal((w1.OnHand - 10m, w2.OnHand + 10m), (w.Scope(w.W).OnHand, w.Scope(w.W2).OnHand));
        Assert.Equal(sku, w.SkuPosition());
        w.AssertInvariant("AC-37");
    }

    [Fact]
    public async Task Transfer_of_a_reserved_lot_is_refused_at_LOT_and_moves_the_lot_positions()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var (w3, w3Bin) = w.AddWarehouse();
        await w.ReceiveIntoAsync("50", lot, w3, w3Bin);
        await w.ReserveOkAsync("40", w.W, lot);

        var refused = await w.PostAsync(w.WarehouseTransfer("11", lot), 422);
        Assert.Equal(("INSUFFICIENT_STOCK", "LOT"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));

        await w.PostAsync(w.WarehouseTransfer("10", lot), 201);
        Assert.Equal((40m, 10m), (w.Scope(w.W, lot).OnHand, w.Scope(w.W2, lot).OnHand));
        w.AssertInvariant("AC-37 lot");
    }

    /// <summary>AC-37: a due (unclosed) reservation of W1 does not stop the transfer — the expiry runs first.</summary>
    [Fact]
    public async Task Transfer_expires_a_due_reservation_of_the_source_first()
    {
        var w = new ScopedReservationWorld(_factory, lotControlled: false);
        await w.ReceiveAsync("50", null);
        var due = await w.ReserveOkAsync("40", w.W, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        await w.PostAsync(w.WarehouseTransfer("11", null), 201);
        Assert.Equal("Expired", w.ReservationDocument(due)["Status"].AsString);
        w.AssertInvariant("AC-37 due");
    }
}
