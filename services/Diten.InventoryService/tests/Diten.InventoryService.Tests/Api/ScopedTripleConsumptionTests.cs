using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-36 (§8.3) — the source-triple consumption without <c>reservationId</c> (ebeveyn ASSUMPTION-0173-15) on a SCOPED
/// reservation: it consumes only when the scope matches (the movement's source warehouse, its registered lot) and then frees every level of
/// the reservation's scope; a reservation whose scope does not match is not consumed and stays byte-equal.
/// </summary>
public sealed class ScopedTripleConsumptionTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedTripleConsumptionTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>(a) — S-28: the triple consumption frees Reserved on the SKU level only → ghost Reserved on W: / L:, the invariant is red.</summary>
    [Fact]
    public async Task Triple_consumption_frees_all_levels()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var r = await w.ReserveOkAsync("30", w.W, lot, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1");

        await w.PostAsync(w.Issue("30", lot, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1"), 201);

        var doc = w.ReservationDocument(r);
        Assert.Equal(("Released", "Consumed", 0m), (doc["Status"].AsString, doc["ClosedReason"].AsString, doc["RemainingQuantity"].ToDecimal()));
        Assert.Equal((0m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        w.AssertInvariant("AC-36 a");
    }

    /// <summary>(b) — S-27: the scope condition dropped from the triple match → the W1 reservation is consumed by a W2 issue, red.</summary>
    [Fact]
    public async Task Triple_match_with_other_warehouse_does_not_consume()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot, w.Ledger.LocationC);
        var r = await w.ReserveOkAsync("30", w.W, lot, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1");
        var before = w.ReservationDocument(r).ToJson();
        var w1 = (w.Scope(w.W), w.Scope(w.W, lot));

        await w.PostAsync(w.Issue("30", lot, w.Ledger.LocationC, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1"), 201);

        Assert.Equal(before, w.ReservationDocument(r).ToJson());
        Assert.Equal(w1, (w.Scope(w.W), w.Scope(w.W, lot)));
        w.AssertInvariant("AC-36 b");
    }
}
