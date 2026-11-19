using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 DL-2 (§8.3, INVENTORY 1.7.0) — a movement that names a reservation by <c>reservationId</c>: the issue consumes exactly that
/// reservation in its posting transaction (no source-triple match), a LOCATION_TRANSFER validates it and consumes nothing, every refusal is a
/// 422 that writes nothing, the request fingerprint carries the id, and the ETag follows the consumption.
/// </summary>
public sealed class ReservationConsumptionTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ReservationConsumptionTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>R = L / W 30 of MOD-0178; L / W on-hand 50; W also holds L2 50 and the SKU has 50 in W2 (the guards never limit it).</summary>
    private async Task<(ScopedReservationWorld W, string Lot, string R)> FixtureAsync(string quantity = "30")
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot2);
        await w.ReceiveAsync("50", lot2, w.Ledger.LocationC);
        var r = await w.ReserveOkAsync(quantity, w.W, lot, sourceModule: "MOD-0178", sourceDocumentId: "OB-1", sourceLineId: "1");
        return (w, lot, r);
    }

    private static (string, string, string, string, string?) Consumption(JsonElement posted)
    {
        var c = posted.GetProperty("reservationConsumption");
        return (c.GetProperty("reservationId").GetString()!, c.GetProperty("consumedQuantity").GetString()!, c.GetProperty("remainingQuantity").GetString()!,
            c.GetProperty("status").GetString()!, c.TryGetProperty("closedReason", out var reason) ? reason.GetString() : null);
    }

    /// <summary>AC-20 — sabotage: reservationId ignored → the issue meets the LOT guard as someone else's (422 INSUFFICIENT_STOCK), red.</summary>
    [Fact]
    public async Task Reservation_id_consumes_the_named_reservation()
    {
        var (w, lot, r) = await FixtureAsync();
        // L holds 50, 30 of it reserved: the same issue by someone else would leave L at 20 − 30 < 0
        var issue = ScopedReservationWorld.Naming(w.Issue("30", lot, sourceModule: "MOD-0183"), r);

        var posted = await w.PostAsync(issue, 201);

        Assert.Equal((r, "30.000", "0.000", "RELEASED", "Consumed"), Consumption(posted));
        var view = await w.GetReservationAsync(r, 200);
        Assert.Equal(("RELEASED", "Consumed", "0.000"), (view.GetProperty("status").GetString(), view.GetProperty("closedReason").GetString(),
            view.GetProperty("remainingQuantity").GetString()));
        Assert.Equal(0m, w.Scope(w.W, lot).Reserved);
        var transaction = Assert.Single(w.Persisted("inventory_transactions"), d => d["_id"].AsString == posted.GetProperty("transactionId").GetString());
        Assert.Equal(r, transaction["ReservationId"].AsString);
        var consumed = Assert.Single(w.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationConsumed");
        Assert.Equal((posted.GetProperty("transactionId").GetString(), "MOD-0183"), (consumed["TransactionId"].AsString, consumed["SourceModule"].AsString));
        var listed = await w.Ledger.ReadAsync(await w.Ledger.GetAsync($"/api/inventory/transactions?itemId={w.Ledger.Item}", w.Lots.Token));
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), i => i.TryGetProperty("reservationId", out var id) && id.GetString() == r);
        w.AssertInvariant("AC-20");
    }

    /// <summary>AC-21 — S-13: the If-Match / Version comparison removed from release → the stale ETag releases (200), red.</summary>
    [Fact]
    public async Task Release_with_stale_etag_is_409()
    {
        var (w, lot, r) = await FixtureAsync();
        var posted = await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("20", lot), r), 201);
        Assert.Equal((r, "20.000", "10.000", "ACTIVE", (string?)null), Consumption(posted));
        var view = await w.GetReservationAsync(r, 200);
        Assert.Equal(("ACTIVE", "10.000", "W/\"2\""), (view.GetProperty("status").GetString(), view.GetProperty("remainingQuantity").GetString(), view.GetProperty("etag").GetString()));
        Assert.Equal(2, w.ReservationDocument(r)["Version"].AsInt32);

        Assert.Equal("RESERVATION_CONFLICT", ScopedReservationWorld.Code(await w.ReleaseAsync(r, "W/\"1\"", 409)));
        Assert.Equal((10m, 10m, 10m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        await w.ReleaseAsync(r, "W/\"2\"", 200);
        Assert.Equal((0m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved));
        w.AssertInvariant("AC-21");
    }

    /// <summary>
    /// AC-22 — S-14: the reservation read of the consumption made without the tenant / company filter → another company's id is consumed /
    /// answered differently, red. Unknown, non-UUID and foreign ids give one code and one message.
    /// </summary>
    [Fact]
    public async Task Foreign_reservation_id_is_RESERVATION_NOT_FOUND_with_same_message()
    {
        var (w, lot, _) = await FixtureAsync();
        var other = new ScopedReservationWorld(_factory);
        var otherLot = await other.LotAsync();
        await other.ReceiveAsync("50", otherLot);
        var foreignTenant = await other.ReserveOkAsync("10", other.W, otherLot);
        var foreignCompany = Guid.NewGuid().ToString();
        _factory.Collection("inventory_reservations").InsertOne(new BsonDocument
        {
            { "_id", foreignCompany }, { "TenantId", w.Ledger.Tenant.ToString() }, { "LegalEntityId", Guid.NewGuid().ToString() }, { "IsDeleted", false },
            { "CreatedAt", DateTime.UtcNow }, { "Version", 1 }, { "ItemId", w.Ledger.Item.ToString() }, { "SkuId", w.Ledger.Sku.ToString() },
            { "SkuLevel", "Lsku" }, { "WarehouseId", w.W }, { "LotId", lot }, { "Quantity", new BsonDecimal128(30m) }, { "RemainingQuantity", new BsonDecimal128(30m) },
            { "Status", "Active" }, { "ReservationType", "PICK" }, { "SourceModule", "MOD-0178" }, { "IdempotencyKey", "foreign-1" }, { "RequestFingerprint", "x" }
        });

        var messages = new List<string>();
        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-reservation", foreignTenant, foreignCompany })
        {
            var refused = await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("5", lot), id), 422);
            Assert.Equal("RESERVATION_NOT_FOUND", ScopedReservationWorld.Code(refused));
            messages.Add(refused.GetProperty("error").GetProperty("message").GetString()!);
        }

        Assert.Single(messages.Distinct());
        var foreign = _factory.Collection("inventory_reservations").Find(new BsonDocument("_id", foreignCompany)).Single();
        Assert.Equal((30m, "Active", 1), (foreign["RemainingQuantity"].ToDecimal(), foreign["Status"].AsString, foreign["Version"].AsInt32));
        Assert.Equal("10.000", (await other.GetReservationAsync(foreignTenant, 200)).GetProperty("remainingQuantity").GetString());
        Assert.DoesNotContain(w.Persisted("inventory_transactions"), d => d.GetValue("MovementType", "").AsString == "GoodsIssueSales");
    }

    /// <summary>AC-22 — every refusal its own fixture: 422, no movement, the reservation byte-equal.</summary>
    [Fact]
    public async Task Each_reservation_refusal_writes_nothing()
    {
        async Task Refused(ScopedReservationWorld w, string r, Dictionary<string, object?> body, string code, string? field = null)
        {
            var before = w.ReservationDocument(r).ToJson();
            var movements = w.Count("inventory_transactions");
            var refused = await w.PostAsync(body, 422);
            Assert.Equal(code, ScopedReservationWorld.Code(refused));
            if (field is not null) Assert.Equal(field, ScopedReservationWorld.Detail(refused, "field"));
            Assert.Equal(movements, w.Count("inventory_transactions"));
            Assert.Equal(before, w.ReservationDocument(r).ToJson());
        }

        // released → NOT_ACTIVE (status, closedReason)
        var (a, lotA, ra) = await FixtureAsync();
        await a.ReleaseAsync(ra, "W/\"1\"", 200);
        var notActive = await a.PostAsync(ScopedReservationWorld.Naming(a.Issue("5", lotA), ra), 422);
        Assert.Equal(("RESERVATION_NOT_ACTIVE", "RELEASED", "Released"), (ScopedReservationWorld.Code(notActive), ScopedReservationWorld.Detail(notActive, "status"),
            ScopedReservationWorld.Detail(notActive, "closedReason")));

        // expired → NOT_ACTIVE (the due one is expired first in the transaction)
        var (b, lotB, _) = await FixtureAsync();
        var due = await b.ReserveOkAsync("5", b.W, lotB, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        var expired = await b.PostAsync(ScopedReservationWorld.Naming(b.Issue("5", lotB), due), 422);
        Assert.Equal(("RESERVATION_NOT_ACTIVE", "EXPIRED"), (ScopedReservationWorld.Code(expired), ScopedReservationWorld.Detail(expired, "status")));

        // mismatch: sku / warehouse / lot
        var (c, lotC, rc) = await FixtureAsync();
        var secondSku = c.Lots.AddSecondSku();
        var secondLot = await c.Lots.CreateLotAsync(sku: secondSku, level: Diten.InventoryService.Domain.Enums.SkuLevel.Gsku);
        Dictionary<string, object?> OfSecondSku(Dictionary<string, object?> body)
        {
            body["skuId"] = secondSku.ToString();
            body["skuLevel"] = "Gsku";
            return body;
        }

        await c.Lots.PostOkAsync(OfSecondSku(c.Lots.Receipt("5", secondLot)));
        await Refused(c, rc, ScopedReservationWorld.Naming(OfSecondSku(c.Issue("5", secondLot)), rc), "RESERVATION_MISMATCH", "sku");
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Issue("5", lotC, c.Ledger.LocationC), rc), "RESERVATION_MISMATCH", "warehouse");
        var otherLot = await c.LotAsync();
        await c.ReceiveAsync("10", otherLot);
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Issue("5", otherLot), rc), "RESERVATION_MISMATCH", "lot");

        // more than the remaining
        var exceeded = await c.PostAsync(ScopedReservationWorld.Naming(c.Issue("31", lotC), rc), 422);
        Assert.Equal(("RESERVATION_QUANTITY_EXCEEDED", "30.000", "31.000"), (ScopedReservationWorld.Code(exceeded), ScopedReservationWorld.Detail(exceeded, "remaining"),
            ScopedReservationWorld.Detail(exceeded, "requested")));

        // not applicable: WAREHOUSE_TRANSFER, STATUS_TRANSFER, SCRAP, REVERSAL, an issue out of QUARANTINE
        await Refused(c, rc, ScopedReservationWorld.Naming(c.WarehouseTransfer("1", lotC), rc), "RESERVATION_NOT_APPLICABLE");
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Lots.StatusTransfer("1", lotC), rc), "RESERVATION_NOT_APPLICABLE");
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Lots.Issue("1", lotC, movementType: "SCRAP"), rc), "RESERVATION_NOT_APPLICABLE");
        var receipt = c.Lots.Receipt("1", lotC);
        var receiptId = await c.Lots.PostOkAsync(receipt);
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Reversal(receipt, receiptId), rc), "RESERVATION_NOT_APPLICABLE");
        await Refused(c, rc, ScopedReservationWorld.Naming(c.Lots.Issue("1", lotC, fromStatus: "QUARANTINE"), rc), "RESERVATION_NOT_APPLICABLE");
        c.AssertInvariant("AC-22");
    }

    /// <summary>AC-23 — S-15: the source-triple match still runs with reservationId → R2 (same triple) is consumed too, red.</summary>
    [Fact]
    public async Task Reservation_id_consumes_only_named_reservation()
    {
        var (w, lot, r1) = await FixtureAsync("20");
        var r2 = await w.ReserveOkAsync("20", w.W, lot, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1");
        var r2Before = w.ReservationDocument(r2).ToJson();

        await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("20", lot, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1"), r1), 201);

        Assert.Equal("Released", w.ReservationDocument(r1)["Status"].AsString);
        Assert.Equal(r2Before, w.ReservationDocument(r2).ToJson());
        w.AssertInvariant("AC-23");
    }

    /// <summary>AC-24 — S-16: a LOCATION_TRANSFER with reservationId counted as a consumption → R loses 30, red.</summary>
    [Fact]
    public async Task Transfer_with_reservation_id_does_not_consume()
    {
        var (w, lot, r) = await FixtureAsync();
        var before = w.ReservationDocument(r);

        var posted = await w.PostAsync(ScopedReservationWorld.Naming(w.Lots.Transfer("30", lot), r), 201);

        Assert.Equal((r, "0.000", "30.000", "ACTIVE", (string?)null), Consumption(posted));
        var after = w.ReservationDocument(r);
        Assert.Equal((before["RemainingQuantity"].ToDecimal(), before["Version"].AsInt32), (after["RemainingQuantity"].ToDecimal(), after["Version"].AsInt32));
        Assert.Equal(r, w.Persisted("inventory_transactions").Single(d => d["_id"].AsString == posted.GetProperty("transactionId").GetString())["ReservationId"].AsString);

        var otherLot = await w.LotAsync();
        await w.ReceiveAsync("5", otherLot);
        Assert.Equal("RESERVATION_MISMATCH", ScopedReservationWorld.Code(await w.PostAsync(ScopedReservationWorld.Naming(w.Lots.Transfer("5", otherLot), r), 422)));
        w.AssertInvariant("AC-24");
    }

    /// <summary>AC-26 — S-18: reservationId taken out of the movement fingerprint → the other reservation's request replays (200), red.</summary>
    [Fact]
    public async Task Same_key_different_reservation_is_IDEMPOTENCY_KEY_REUSED()
    {
        var (w, lot, r) = await FixtureAsync();
        var r2 = await w.ReserveOkAsync("10", w.W, lot);
        var issue = ScopedReservationWorld.Naming(w.Issue("10", lot), r);

        var first = await w.PostAsync(issue, 201);
        var positions = w.PositionsSnapshot();
        var replay = await w.PostAsync(issue, 200);
        Assert.True(replay.GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(Consumption(first), Consumption(replay));
        Assert.Equal(positions, w.PositionsSnapshot());
        Assert.Equal(20m, w.ReservationDocument(r)["RemainingQuantity"].ToDecimal());

        var otherReservation = new Dictionary<string, object?>(issue) { ["reservationId"] = r2 };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ScopedReservationWorld.Code(await w.Lots.PostAsync(otherReservation, 400)));
        Assert.Equal(10m, w.ReservationDocument(r2)["RemainingQuantity"].ToDecimal());
    }
}
