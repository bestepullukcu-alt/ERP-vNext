using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0173-FU03 AC-18 (§8.6) — MOD-0175's code is unchanged: "Karantinaya Al" of stock of a lot that a LOT reservation holds entirely is
/// refused by the ledger's LOT guard with the same INSUFFICIENT_STOCK, which MOD-0175 maps to 409 QUARANTINE_STOCK_RESERVED with the SKU's
/// reservations listed. Fixture (F-4): W holds L2 too, so neither the warehouse nor the SKU free quantity would refuse — only the lot level.
/// </summary>
public sealed class ReservationQuarantineInteractionTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public ReservationQuarantineInteractionTests(QuarantineApiFactory factory) => _factory = factory;

    /// <summary>S-11: the LOT level dropped from the decrease guard → the quarantine posts (201, a hold exists), red.</summary>
    [Fact]
    public async Task Quarantine_of_lot_reserved_stock_is_409()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var lot2 = await s.ReleasedLotAsync();
        await s.ReceiveAsync("50", lot, "AVAILABLE");
        await s.ReceiveAsync("50", lot2, "AVAILABLE");
        var reservation = s.Ledger.Reservation("50");
        reservation["warehouseId"] = s.Ledger.Warehouse;
        reservation["lotId"] = lot;
        using (var created = await s.Ledger.ReserveAsync(reservation, token: s.Lots.Token))
        {
            Assert.Equal(201, (int)created.StatusCode);
        }

        var before = await s.AllMovementCountAsync();
        var key = QuarantineScenario.Key("hold");
        var body = new
        {
            mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key,
            buckets = new[] { new { itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Lsku", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA, quantity = 50m, lotId = lot } }
        };

        var refused = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", body, 409, key);
        Assert.Equal("QUARANTINE_STOCK_RESERVED", QuarantineScenario.Code(refused));
        var listed = refused.GetProperty("error").GetProperty("details").GetProperty("reservations");
        Assert.Equal(1, listed.GetArrayLength());
        Assert.Equal(before, await s.AllMovementCountAsync());
        Assert.Empty(s.Holds());
        Assert.Equal(100m, await s.OnHandAsync("AVAILABLE"));
    }
}
