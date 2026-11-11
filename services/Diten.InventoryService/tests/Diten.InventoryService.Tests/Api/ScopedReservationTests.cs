using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 DL-1 — scoped reservations through the real API (§8.2). GUARDS UNDER TEST: the create guard on every level of the scope
/// (LOT → WAREHOUSE → SKU), the decrease guard on the warehouse / lot levels, the lot / warehouse rules at creation, the fingerprint with
/// the scope, release / expiry on every level. Fixture rule (§17, F-4): where one level is measured, the levels above it have MORE free
/// quantity (a second lot L2 in W, stock of the SKU in W2), so only the measured level can refuse.
/// </summary>
public sealed class ScopedReservationTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedReservationTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>AC-1 — sabotage: the reader / parser drops lotId → the reservation is SKU level, the lot fields are absent, red.</summary>
    [Fact]
    public async Task Lot_reservation_is_created_and_read_back_with_its_scope()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);

        var created = await w.ReserveAsync(w.Reservation("30", w.W, lot), 201);
        Assert.Equal((w.W, lot, "30.000"), (created.GetProperty("warehouseId").GetString(), created.GetProperty("lotId").GetString(),
            created.GetProperty("remainingQuantity").GetString()));

        var id = created.GetProperty("reservationId").GetString()!;
        var view = await w.GetReservationAsync(id, 200);
        Assert.Equal(("ACTIVE", w.W, lot, "30.000", "W/\"1\""), (view.GetProperty("status").GetString(), view.GetProperty("warehouseId").GetString(),
            view.GetProperty("lotId").GetString(), view.GetProperty("remainingQuantity").GetString(), view.GetProperty("etag").GetString()));
        Assert.False(view.TryGetProperty("closedReason", out _));

        var doc = w.ReservationDocument(id);
        Assert.Equal((w.W, lot), (doc["WarehouseId"].AsString, doc["LotId"].AsString));
        Assert.Equal((50m, 30m), w.Scope(w.W, lot));
        Assert.Equal((50m, 30m), w.Scope(w.W));
        Assert.Equal((50m, 30m), w.SkuPosition());
        w.AssertInvariant("AC-1");
    }

    /// <summary>AC-2 — sabotage: the LOT level guard is dropped → the 11 is created, red.</summary>
    [Fact]
    public async Task Lot_guard_refuses_beyond_the_lot_free_quantity_with_level_LOT()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("30", lot);
        await w.ReceiveAsync("20", lot, w.Ledger.LocationB);
        await w.ReceiveAsync("50", lot2);
        await w.ReceiveAsync("50", lot2, w.Ledger.LocationC); // the SKU's W2 stock: W free 100, SKU free 150 — only the lot limits

        await w.ReserveAsync(w.Reservation("40", w.W, lot), 201);
        var positions = w.PositionsSnapshot();

        var refused = await w.ReserveAsync(w.Reservation("11", w.W, lot), 422);
        Assert.Equal("INSUFFICIENT_AVAILABLE", ScopedReservationWorld.Code(refused));
        Assert.Equal(("LOT", "10.000", "11.000"), (ScopedReservationWorld.Detail(refused, "level"), ScopedReservationWorld.Detail(refused, "available"),
            ScopedReservationWorld.Detail(refused, "requested")));
        Assert.Single(w.Reservations());
        Assert.Equal(positions, w.PositionsSnapshot());

        await w.ReserveAsync(w.Reservation("10", w.W, lot), 201);
        w.AssertInvariant("AC-2");
    }

    /// <summary>AC-3 (a) — sabotage: the WAREHOUSE level increase is dropped → the lot reservation of 1 is created, red.</summary>
    [Fact]
    public async Task Warehouse_guard_refuses_a_lot_reservation_when_the_warehouse_is_held()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot, w.Ledger.LocationC);

        await w.ReserveAsync(w.Reservation("50", w.W), 201);
        var refused = await w.ReserveAsync(w.Reservation("1", w.W, lot), 422);
        Assert.Equal(("INSUFFICIENT_AVAILABLE", "WAREHOUSE"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));
        Assert.Single(w.Reservations());
        w.AssertInvariant("AC-3 a");
    }

    /// <summary>AC-3 (b) — sabotage: the SKU level increase is dropped → the unscoped reservation of 1 is created, red.</summary>
    [Fact]
    public async Task Sku_guard_refuses_an_unscoped_reservation_when_the_scoped_ones_hold_everything()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot, w.Ledger.LocationC);

        await w.ReserveAsync(w.Reservation("50", w.W), 201);
        await w.ReserveAsync(w.Reservation("50", w.W2), 201);
        var refused = await w.ReserveAsync(w.Reservation("1"), 422);
        Assert.Equal(("INSUFFICIENT_AVAILABLE", "SKU"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));
        Assert.Equal(2, w.Reservations().Count);
        w.AssertInvariant("AC-3 b");
    }

    /// <summary>AC-4 (a) — sabotage: the LOT level guard on decreases is dropped → the 21 is issued, red.</summary>
    [Fact]
    public async Task Reserved_lot_stock_is_not_issued_to_someone_else()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot2);
        await w.ReceiveAsync("50", lot2, w.Ledger.LocationC);
        var r = await w.ReserveOkAsync("30", w.W, lot);
        var before = w.ReservationDocument(r).ToJson();
        var movements = w.Count("inventory_transactions");

        var refused = await w.PostAsync(w.Issue("21", lot), 422);
        Assert.Equal(("INSUFFICIENT_STOCK", "LOT"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));
        Assert.Equal(movements, w.Count("inventory_transactions"));
        Assert.Equal(before, w.ReservationDocument(r).ToJson());

        await w.PostAsync(w.Issue("20", lot), 201);
        Assert.Equal("30.000", (await w.GetReservationAsync(r, 200)).GetProperty("remainingQuantity").GetString());
        w.AssertInvariant("AC-4 a");
    }

    /// <summary>AC-4 (b) — sabotage: the WAREHOUSE level guard on decreases is dropped → the 21 is issued, red.</summary>
    [Fact]
    public async Task Reserved_warehouse_stock_is_not_issued_to_someone_else()
    {
        var w = new ScopedReservationWorld(_factory, lotControlled: false);
        await w.ReceiveAsync("50", null);
        await w.ReceiveAsync("50", null, w.Ledger.LocationC);
        await w.ReserveOkAsync("30", w.W);

        var refused = await w.PostAsync(w.Issue("21", null), 422);
        Assert.Equal(("INSUFFICIENT_STOCK", "WAREHOUSE"), (ScopedReservationWorld.Code(refused), ScopedReservationWorld.Detail(refused, "level")));
        await w.PostAsync(w.Issue("20", null), 201);
        w.AssertInvariant("AC-4 b");
    }

    /// <summary>AC-5 — sabotage: a LOCATION_TRANSFER counted as a decrease + consumption → the reservation changes, red.</summary>
    [Fact]
    public async Task Location_transfer_keeps_the_reservation_and_the_scope_positions()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("30", lot);
        var r = await w.ReserveOkAsync("30", w.W, lot, sourceDocumentId: "OB-1", sourceLineId: "1");
        var positions = w.PositionsSnapshot();
        var transfer = w.Lots.Transfer("30", lot); // A → B; it carries the reservation's own source triple, so a "consumption" would match it
        transfer["sourceModule"] = "MOD-0178";
        transfer["sourceDocumentId"] = "OB-1";
        transfer["sourceLineId"] = "1";

        await w.PostAsync(transfer, 201);

        var doc = w.ReservationDocument(r);
        Assert.Equal(("Active", 30m, 1), (doc["Status"].AsString, doc["RemainingQuantity"].ToDecimal(), doc["Version"].AsInt32));
        Assert.Equal(positions, w.PositionsSnapshot());
        w.AssertInvariant("AC-5");
    }

    /// <summary>
    /// AC-6 — each refusal its own fixture, nothing written in any of them. S-1: the lot status branches (Blocked / Expired / retest / recall)
    /// are skipped at creation → at least LOT_BLOCKED answers 201, red.
    /// </summary>
    [Fact]
    public async Task Create_refuses_each_lot_and_location_state()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("100", lot);

        async Task Refused(Dictionary<string, object?> body, int status, string code)
        {
            var reservations = w.Reservations().Count;
            var positions = w.PositionsSnapshot();
            var refused = await w.ReserveAsync(body, status);
            Assert.Equal(code, ScopedReservationWorld.Code(refused));
            Assert.Equal(reservations, w.Reservations().Count);
            Assert.Equal(positions, w.PositionsSnapshot());
        }

        await Refused(w.Reservation("1", w.W, Guid.NewGuid().ToString("D")), 422, "UNKNOWN_LOT");
        await Refused(w.Reservation("1", w.W, "LOT-TYPED-1"), 422, "UNKNOWN_LOT");

        var otherSku = await w.Lots.CreateLotAsync(sku: w.Lots.AddSecondSku(), level: SkuLevel.Gsku);
        await Refused(w.Reservation("1", w.W, otherSku), 422, "LOT_SKU_MISMATCH");

        var blocked = await w.LotAsync();
        await w.ReceiveAsync("10", blocked);
        await w.Lots.ReleaseAsync(blocked);
        await w.Lots.BlockAsync(blocked);
        await Refused(w.Reservation("1", w.W, blocked), 422, "LOT_BLOCKED");

        var expired = await w.Lots.CreateLotAsync(expiryDate: "2026-09-15", manufacturingDate: "2026-01-10");
        await Refused(w.Reservation("1", w.W, expired), 422, "LOT_EXPIRED");

        var retest = await w.Lots.CreateLotAsync(retestDate: "2026-10-01T00:00:00Z");
        await Refused(w.Reservation("1", w.W, retest), 422, "LOT_RETEST_DUE");

        var recalled = await w.LotAsync();
        await RecallBlock.AddAsync(_factory, w.Ledger.Tenant, w.Ledger.LegalEntity, recalled);
        await Refused(w.Reservation("1", w.W, recalled), 422, "LOT_UNDER_RECALL");
        await RecallBlock.RemoveAsync(_factory, w.Ledger.Tenant);

        var (foreignWarehouse, _) = w.Ledger.AddForeignCompanyBin();
        await Refused(w.Reservation("1", foreignWarehouse), 422, "UNKNOWN_LOCATION");
        var inactive = w.Ledger.AddLocation(LocationType.Warehouse, w.Ledger.SiteNode, status: LocationStatus.Inactive);
        await Refused(w.Reservation("1", inactive), 422, "UNKNOWN_LOCATION");
        await Refused(w.Reservation("1", w.Ledger.LocationA), 422, "UNKNOWN_LOCATION");
        await Refused(w.Reservation("1", lotId: lot), 400, "INVALID_REQUEST");

        // a Draft lot is accepted (ASSUMPTION-0174-07)
        Assert.Equal("Draft", (await w.Lots.GetTraceAsync($"/api/trace/lots/{lot}")).GetProperty("status").GetString());
        await w.ReserveAsync(w.Reservation("1", w.W, lot), 201);
        w.AssertInvariant("AC-6");
    }

    /// <summary>AC-8 — S-3: lotId is left out of the create fingerprint → the second call answers 200, red.</summary>
    [Fact]
    public async Task Same_key_different_lot_is_IDEMPOTENCY_KEY_REUSED()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot2);
        var body = w.Reservation("10", w.W, lot);

        var first = await w.ReserveAsync(body, 201);
        var positions = w.PositionsSnapshot();
        var replay = await w.ReserveAsync(body, 200);
        Assert.Equal(first.GetProperty("reservationId").GetString(), replay.GetProperty("reservationId").GetString());
        Assert.Equal(positions, w.PositionsSnapshot());

        var otherLot = new Dictionary<string, object?>(body) { ["lotId"] = lot2 };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ScopedReservationWorld.Code(await w.ReserveAsync(otherLot, 400)));
        Assert.Single(w.Reservations());
        Assert.Equal(positions, w.PositionsSnapshot());
    }

    /// <summary>AC-10 — S-5: release / expiry free only the SKU level (W: / L: skipped) → the scope positions keep 30, red.</summary>
    [Fact]
    public async Task Release_and_expiry_free_all_three_levels()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("100", lot);

        var r = await w.ReserveOkAsync("30", w.W, lot);
        Assert.Equal((30m, 30m, 30m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        var released = await w.ReleaseAsync(r, "W/\"1\"", 200);
        Assert.Equal("RELEASED", released.GetProperty("status").GetString());
        Assert.Equal((0m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        Assert.Equal("Released", (await w.GetReservationAsync(r, 200)).GetProperty("closedReason").GetString());

        // expiry, in-transaction path: a due reservation is closed by the next create of the SKU
        var due = await w.ReserveOkAsync("30", w.W, lot, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await w.ReserveOkAsync("1");
        var view = await w.GetReservationAsync(due, 200);
        Assert.Equal(("EXPIRED", "Expired"), (view.GetProperty("status").GetString(), view.GetProperty("closedReason").GetString()));
        Assert.Equal((1m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        w.AssertInvariant("AC-10");
    }
}
