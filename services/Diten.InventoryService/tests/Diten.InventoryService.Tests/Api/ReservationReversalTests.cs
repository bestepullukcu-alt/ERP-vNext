using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-35 (§8.3.1, CT-08 with the r3 boundary) — a REVERSAL with <c>restoreReservation: true</c> of a movement that consumed a
/// reservation by <c>reservationId</c> gives the quantity back in the same posting transaction; without the flag (or with false) the
/// reservation stays closed. Each case its own fixture. Q-09 = A (MVP-6): INVENTORY behaves the same either way — measured here.
/// </summary>
public sealed class ReservationReversalTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ReservationReversalTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>R1 = L / W 30 (MOD-0178); L / W on-hand 50; W also holds L2 20.</summary>
    private async Task<(ScopedReservationWorld W, string Lot, string R)> FixtureAsync(TimeSpan? expiresIn = null, bool releaseLot = false)
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        if (releaseLot) await w.Lots.ReleaseAsync(lot);
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("20", lot2);
        var r = await w.ReserveOkAsync("30", w.W, lot, sourceModule: "MOD-0178", sourceDocumentId: "OB-1", sourceLineId: "1",
            expiresAt: expiresIn is { } span ? ScopedReservationWorld.Later(span) : null);
        return (w, lot, r);
    }

    private static Dictionary<string, object?> Keyed(Dictionary<string, object?> body, string key)
    {
        body["idempotencyKey"] = key;
        return body;
    }

    private static string Id(JsonElement posted) => posted.GetProperty("transactionId").GetString()!;

    private static List<BsonDocument> Rows(ScopedReservationWorld w, string action)
        => w.Persisted("inventory_audit").Where(d => d.GetValue("Action", "").AsString == action).ToList();

    private static (decimal, decimal, decimal) Reserved(ScopedReservationWorld w, string lot)
        => (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, lot).Reserved);

    private static (decimal, decimal, decimal) OnHand(ScopedReservationWorld w, string lot)
        => (w.SkuPosition().OnHand, w.Scope(w.W).OnHand, w.Scope(w.W, lot).OnHand);

    /// <summary>
    /// (a) MOD-0183 §23.4 G4 (a) — line 1 consumes R1, line 2 is refused, the compensation reverses line 1 with restoreReservation: true and
    /// the new attempt (new keys, a first write) issues line 1 by the same reservationId. S-24: the restore step removed → the new attempt
    /// answers 422 RESERVATION_NOT_ACTIVE, red.
    /// </summary>
    [Fact]
    public async Task Shipment_compensation_restores_reservation_and_retry_succeeds()
    {
        var (w, lot, r1) = await FixtureAsync();
        var line1 = Keyed(ScopedReservationWorld.Naming(w.Issue("30", lot, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1"), r1), "S:1:a1");
        var line1Id = Id(await w.PostAsync(line1, 201));
        Assert.Equal(("Released", "Consumed"), (w.ReservationDocument(r1)["Status"].AsString, w.ReservationDocument(r1)["ClosedReason"].AsString));
        var line2 = Keyed(ScopedReservationWorld.Naming(w.Issue("5", lot, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "2"), r1), "S:2:a1");
        await w.PostAsync(line2, 422);
        var version = w.ReservationDocument(r1)["Version"].AsInt32;

        var reversal = await w.PostAsync(Keyed(w.Reversal(line1, line1Id, restore: true), "S:1:reversal:a1"), 201);

        var view = await w.GetReservationAsync(r1, 200);
        Assert.Equal(("ACTIVE", "30.000"), (view.GetProperty("status").GetString(), view.GetProperty("remainingQuantity").GetString()));
        Assert.False(view.TryGetProperty("closedReason", out _));
        Assert.Equal(version + 1, w.ReservationDocument(r1)["Version"].AsInt32);
        Assert.Equal((30m, 30m, 30m), Reserved(w, lot));
        var restored = Assert.Single(Rows(w, "InventoryReservationRestored"));
        Assert.Equal((Id(reversal), 30m, "Succeeded"), (restored["TransactionId"].AsString, restored["ChangedQuantity"].ToDecimal(), restored["Outcome"].AsString));
        Assert.False(reversal.TryGetProperty("reservationConsumption", out _)); // a REVERSAL never carries reservationId
        w.AssertInvariant("AC-35 a restored");

        var retry = Keyed(ScopedReservationWorld.Naming(w.Issue("30", lot, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1"), r1), "S:1:a2");
        await w.PostAsync(retry, 201);
        Assert.Equal(("Released", "Consumed"), (w.ReservationDocument(r1)["Status"].AsString, w.ReservationDocument(r1)["ClosedReason"].AsString));
        w.AssertInvariant("AC-35 a retried");
    }

    /// <summary>(b) a partly consumed reservation gets the consumed part back.</summary>
    [Fact]
    public async Task Partial_consumption_reversal_restores_the_remainder()
    {
        var (w, lot, r) = await FixtureAsync();
        var issue = ScopedReservationWorld.Naming(w.Issue("20", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        await w.PostAsync(w.Reversal(issue, id, restore: true), 201);
        Assert.Equal(("Active", 30m), (w.ReservationDocument(r)["Status"].AsString, w.ReservationDocument(r)["RemainingQuantity"].ToDecimal()));
        Assert.Equal((30m, 30m, 30m), Reserved(w, lot));
        w.AssertInvariant("AC-35 b");
    }

    /// <summary>(c) — S-26: a reservation the holder released (ClosedReason Released) restored too → it comes back, red.</summary>
    [Fact]
    public async Task Holder_released_reservation_is_not_restored()
    {
        var (w, lot, r) = await FixtureAsync();
        var issue = ScopedReservationWorld.Naming(w.Issue("20", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        await w.ReleaseAsync(r, "W/\"2\"", 200);
        var before = w.ReservationDocument(r).ToJson();

        await w.PostAsync(w.Reversal(issue, id, restore: true), 201);

        Assert.Equal(before, w.ReservationDocument(r).ToJson());
        Assert.Empty(Rows(w, "InventoryReservationRestored"));
        Assert.Equal((0m, 0m, 0m), Reserved(w, lot));
        w.AssertInvariant("AC-35 c");
    }

    /// <summary>(d) — S-25: no expiry after the restore → the past-due reservation stays ACTIVE, red.</summary>
    [Fact]
    public async Task Restored_past_due_reservation_expires_in_same_transaction()
    {
        var (w, lot, r) = await FixtureAsync(expiresIn: TimeSpan.FromSeconds(2));
        var issue = ScopedReservationWorld.Naming(w.Issue("30", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        var reserved = Reserved(w, lot);

        var reversal = Id(await w.PostAsync(w.Reversal(issue, id, restore: true), 201));

        Assert.Equal(("Expired", "Expired"), (w.ReservationDocument(r)["Status"].AsString, w.ReservationDocument(r)["ClosedReason"].AsString));
        // inventory_audit is insert-only: its natural order is the write order of the transaction (Restored, then the expiry it triggered)
        var rows = w.Persisted("inventory_audit").Where(d => d.GetValue("Action", "").AsString is "InventoryReservationRestored" or "InventoryReservationExpired").ToList();
        Assert.Equal(["InventoryReservationRestored", "InventoryReservationExpired"], rows.Select(d => d["Action"].AsString));
        Assert.Equal(reversal, rows[0]["TransactionId"].AsString);
        Assert.Equal(reserved, Reserved(w, lot));
        w.AssertInvariant("AC-35 d");
    }

    /// <summary>(e) the same REVERSAL again replays without a second restore; another key on the same original is ALREADY_REVERSED.</summary>
    [Fact]
    public async Task Replay_and_second_reversal_restore_nothing_more()
    {
        var (w, lot, r) = await FixtureAsync();
        var issue = ScopedReservationWorld.Naming(w.Issue("30", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        var reversal = w.Reversal(issue, id, restore: true);
        await w.PostAsync(reversal, 201);
        var after = w.ReservationDocument(r).ToJson();

        var replay = await w.PostAsync(reversal, 200);
        Assert.True(replay.GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal("ALREADY_REVERSED", ScopedReservationWorld.Code(await w.PostAsync(w.Reversal(issue, id, restore: true), 422)));

        Assert.Single(Rows(w, "InventoryReservationRestored"));
        Assert.Equal(after, w.ReservationDocument(r).ToJson());
        w.AssertInvariant("AC-35 e");
    }

    /// <summary>(f) a source-triple consumption (no reservationId) is not restored — the flag has no effect, the REVERSAL posts.</summary>
    [Fact]
    public async Task Triple_consumption_reversal_restores_nothing()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var r = await w.ReserveOkAsync("30", w.W, lot, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1");
        var issue = w.Issue("30", lot, sourceModule: "MOD-0183", sourceDocumentId: "D1", sourceLineId: "1");
        var id = Id(await w.PostAsync(issue, 201));
        var before = w.ReservationDocument(r).ToJson();

        await w.PostAsync(w.Reversal(issue, id, restore: true), 201);

        Assert.Equal(before, w.ReservationDocument(r).ToJson());
        Assert.Empty(Rows(w, "InventoryReservationRestored"));
        w.AssertInvariant("AC-35 f");
    }

    /// <summary>
    /// (g) — S-32: the restore moved inside the position branch (<c>lot is null || !HoldsStock(lot)</c>) → on a held lot it is skipped (R stays
    /// RELEASED, Reserved does not rise), red. The REVERSAL passes the Blocked lot (CT-D4); AvailableOnHand does not move on any level.
    /// </summary>
    [Fact]
    public async Task Restore_on_held_lot_keeps_reserved_invariant()
    {
        var (w, lot, r) = await FixtureAsync(releaseLot: true);
        var issue = ScopedReservationWorld.Naming(w.Issue("30", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        await w.Lots.BlockAsync(lot);
        var onHand = OnHand(w, lot);
        Assert.Equal((20m, 20m, 0m), onHand); // L's 20 left availability; L2's 20 stays

        await w.PostAsync(w.Reversal(issue, id, restore: true), 201);

        Assert.Equal(("Active", 30m), (w.ReservationDocument(r)["Status"].AsString, w.ReservationDocument(r)["RemainingQuantity"].ToDecimal()));
        Assert.Equal((30m, 30m, 30m), Reserved(w, lot));
        Assert.Equal(onHand, OnHand(w, lot));
        w.AssertInvariant("AC-35 g");
    }

    /// <summary>(h) — S-33: restoreReservation defaulting to true → the REVERSAL without the member restores R, red. Without it and with false alike.</summary>
    [Fact]
    public async Task Reversal_without_restore_flag_keeps_reservation_closed()
    {
        foreach (var flag in new bool?[] { null, false })
        {
            var (w, lot, r1) = await FixtureAsync();
            var line1 = ScopedReservationWorld.Naming(w.Issue("30", lot), r1);
            var id = Id(await w.PostAsync(line1, 201));
            var before = w.ReservationDocument(r1).ToJson();
            var reserved = Reserved(w, lot);

            await w.PostAsync(w.Reversal(line1, id, restore: flag), 201);

            Assert.Equal(before, w.ReservationDocument(r1).ToJson());
            Assert.Equal(reserved, Reserved(w, lot));
            Assert.Empty(Rows(w, "InventoryReservationRestored"));
            Assert.Equal("RESERVATION_NOT_ACTIVE", ScopedReservationWorld.Code(await w.PostAsync(ScopedReservationWorld.Naming(w.Issue("30", lot), r1), 422)));
            w.AssertInvariant($"AC-35 h ({flag?.ToString() ?? "absent"})");
        }
    }

    /// <summary>(i) the flag belongs to a REVERSAL only and is a boolean; true and false are different requests under one key.</summary>
    [Fact]
    public async Task Restore_flag_is_a_reversal_only_boolean()
    {
        var (w, lot, r) = await FixtureAsync();
        var movements = w.Count("inventory_transactions");
        var onIssue = ScopedReservationWorld.Naming(w.Issue("5", lot), r);
        onIssue["restoreReservation"] = true;
        Assert.Equal("INVALID_REQUEST", ScopedReservationWorld.Code(await w.Lots.PostAsync(onIssue, 400)));
        var issue = ScopedReservationWorld.Naming(w.Issue("5", lot), r);
        var id = Id(await w.PostAsync(issue, 201));
        var notBoolean = w.Reversal(issue, id);
        notBoolean["restoreReservation"] = "yes";
        Assert.Equal("INVALID_REQUEST", ScopedReservationWorld.Code(await w.Lots.PostAsync(notBoolean, 400)));
        Assert.Equal(movements + 1, w.Count("inventory_transactions"));

        var withTrue = w.Reversal(issue, id, restore: true);
        await w.PostAsync(withTrue, 201);
        var withFalse = new Dictionary<string, object?>(withTrue) { ["restoreReservation"] = false };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ScopedReservationWorld.Code(await w.Lots.PostAsync(withFalse, 400)));
    }
}
