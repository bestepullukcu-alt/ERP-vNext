using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-33 — the three requirements of MOD-0178 S2 together, in one test: the port reserves a lot in a warehouse (batch), the
/// reserved stock is moved bin → staging with the reservation named (it is not consumed), another source cannot issue it from staging,
/// and the shipment issues it from staging by its reservationId.
/// </summary>
public sealed class ReservationGoldenFlowTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ReservationGoldenFlowTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>S-23: a LOCATION_TRANSFER with reservationId counted as a consumption → R closes on staging and the last issue is 422, red.</summary>
    [Fact]
    public async Task Reserve_move_protect_consume()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot); // bin A
        var staging = w.Ledger.LocationB;

        var batch = await new PortWorld(_factory, w).Use(p => p.CreateBatchAsync([PortWorld.Line(w, "30", w.W, lot)], w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal(201, batch.StatusCode);
        var r = batch.Lines[0].Reservation!.ReservationId;

        await w.PostAsync(ScopedReservationWorld.Naming(w.Lots.Transfer("30", lot), r), 201); // A → staging (B)

        var other = w.Issue("21", lot, staging, sourceModule: "MOD-0172", sourceDocumentId: "SO-9", sourceLineId: "1");
        Assert.Equal("INSUFFICIENT_STOCK", ScopedReservationWorld.Code(await w.PostAsync(other, 422)));

        await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("30", lot, staging, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1"), r), 201);

        var view = await w.GetReservationAsync(r, 200);
        Assert.Equal(("RELEASED", "Consumed"), (view.GetProperty("status").GetString(), view.GetProperty("closedReason").GetString()));
        Assert.Equal((0m, 20m), (await w.Ledger.OnHandAsync(staging), await w.Ledger.OnHandAsync(w.Ledger.LocationA)));
        Assert.Equal((0m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        w.AssertInvariant("AC-33");
    }
}
