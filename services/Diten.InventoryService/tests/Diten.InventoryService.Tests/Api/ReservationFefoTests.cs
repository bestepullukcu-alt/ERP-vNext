using Diten.InventoryService.Tests.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-29 (§8.3 "FEFO") — consuming a reservation is not exempt from the MOD-0176 FEFO hook: with the earlier-expiring lot L1
/// FREE (reserved by no one) and the company policy "Gerekçe iste" (the default), the issue of L2 that consumes R needs a bypass reason
/// exactly as in 1.3.0. Runs on the expiry host (its tenant clean-up covers the FEFO snapshot / exception documents).
/// </summary>
public sealed class ReservationFefoTests : IClassFixture<ExpiryApiFactory>
{
    private const string Reason = "Musteri talebi: uzun raf omurlu lot istendi";
    private readonly ExpiryApiFactory _factory;

    public ReservationFefoTests(ExpiryApiFactory factory) => _factory = factory;

    /// <summary>S-20: the FEFO check skipped for a movement carrying reservationId → the unreasoned issue posts (201), red.</summary>
    [Fact]
    public async Task Fefo_bypass_reason_still_required_with_reservation()
    {
        var s = new ExpiryScenario(_factory);
        var w = ScopedReservationWorld.Of(s.Lots);
        var l1 = await s.LotAsync("2027-01-31T00:00:00Z");
        var l2 = await s.LotAsync("2027-06-30T00:00:00Z");
        await w.ReceiveAsync("10", l1);
        await w.ReceiveAsync("10", l2);
        var r = await w.ReserveOkAsync("10", w.W, l2);

        var refused = await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("5", l2), r), 422);
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", ScopedReservationWorld.Code(refused));
        Assert.Equal(l1, ScopedReservationWorld.Detail(refused, "firstEligibleLotId"));
        Assert.Equal(10m, w.ReservationDocument(r)["RemainingQuantity"].ToDecimal());

        var reasoned = ScopedReservationWorld.Naming(w.Issue("5", l2), r);
        reasoned["fefoBypassReason"] = Reason;
        var posted = await w.PostAsync(reasoned, 201);
        Assert.Equal("Bypassed", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Equal("5.000", posted.GetProperty("reservationConsumption").GetProperty("remainingQuantity").GetString());
        w.AssertInvariant("AC-29");
    }
}
