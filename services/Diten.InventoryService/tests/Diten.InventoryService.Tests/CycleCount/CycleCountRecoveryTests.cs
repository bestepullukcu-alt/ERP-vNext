using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Persistence.Traceability;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-3 — the correction's recovery and the ledger's refusals (pack §8.6–§8.10; AC-25, AC-26, AC-27, AC-28, AC-34, AC-47 (e)) over the REAL
/// ledger. Only the stock door's NEXT call can be faulted (5xx, cancellation, an exception, or the real posting with its answer lost —
/// <see cref="CountPosterFault"/>); everything else — the key lookup, the reservation, the lot rules, the recall gate — is production code.
/// </summary>
public sealed class CycleCountRecoveryTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static string Status(JsonElement line) => line.GetProperty("status").GetString()!;

    private static JsonElement Adjustment(JsonElement line) => line.GetProperty("adjustment");

    private static string AdjustmentStatus(JsonElement line) => Adjustment(line).GetProperty("status").GetString()!;

    private Task<CycleCountReconcileTick> SweepAsync(CycleCountScenario s)
        => factory.Services.GetRequiredService<CycleCountReconciler>().RunOnceAsync(DateTimeOffset.UtcNow.AddHours(2), CancellationToken.None, [s.Tenant]);

    // ── AC-25 ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CountPosterFault.Status503)]
    [InlineData(CountPosterFault.Status500)]
    [InlineData(CountPosterFault.Status502)]
    [InlineData(CountPosterFault.Status504)]
    [InlineData(CountPosterFault.Cancelled)]
    [InlineData(CountPosterFault.Exception)]
    public async Task AC25_an_unknown_outcome_before_the_commit_stays_Pending_never_Refused_and_the_retry_posts_once_with_the_same_dates(CountPosterFault fault)
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 7);
        factory.PosterFault[s.Tenant] = fault;
        Assert.Equal("COUNT_INVENTORY_UNAVAILABLE", await ErrorCode(s.ApproveAsync(line), 503));

        var pending = await s.LineAsync(countId, LineId(line));
        Assert.Equal("Approved", Status(pending));
        Assert.Equal("Pending", AdjustmentStatus(pending));
        Assert.False(Adjustment(pending).TryGetProperty("refusalCode", out _));
        Assert.Equal(0, s.CountMovements());
        // the failed command's central record is never delivered (BL-501: Abandoned)
        Assert.All(s.CentralRows("ApproveCountVarianceCommand"), r => Assert.Contains(r["Status"].ToString(), new[] { "Abandoned", "4" }));

        var retried = await Expect(s.RetryAsync(pending), 200);
        Assert.Equal("Posted", AdjustmentStatus(retried));
        var movement = Assert.Single(s.CountLedgerMovements());
        Assert.Equal(Adjustment(pending).GetProperty("transactionDate").GetString(), Adjustment(retried).GetProperty("transactionDate").GetString());
        Assert.Equal(Adjustment(pending).GetProperty("postingDate").GetString(), Adjustment(retried).GetProperty("postingDate").GetString());
        Assert.Equal(DateTimeOffset.Parse(Adjustment(retried).GetProperty("postingDate").GetString()!, CultureInfo.InvariantCulture),
            new DateTimeOffset(movement["PostingDate"].ToUniversalTime(), TimeSpan.Zero));
        Assert.Equal(2, Adjustment(retried).GetProperty("attempts").GetInt32());
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC25_a_commit_whose_answer_was_lost_is_found_by_the_sweeper_and_no_second_movement_is_written()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 13);
        factory.PosterFault[s.Tenant] = CountPosterFault.CommitThenException;
        Assert.Equal(503, (int)(await s.ApproveAsync(line)).StatusCode);
        Assert.Equal("Pending", AdjustmentStatus(await s.LineAsync(countId, LineId(line))));
        Assert.Single(s.CountLedgerMovements());

        var tick = await SweepAsync(s);
        Assert.Equal(1, tick.CommandsSent);
        var posted = await s.LineAsync(countId, LineId(line));
        Assert.Equal("Posted", AdjustmentStatus(posted));
        Assert.Equal(Assert.Single(s.CountLedgerMovements())["_id"].AsString, Adjustment(posted).GetProperty("transactionId").GetString());
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Single(s.CentralRows("ReconcilePendingCountAdjustmentCommand"));
        Assert.Equal(0, (await SweepAsync(s)).Scanned); // nothing Pending any more
    }

    [Fact]
    public async Task AC25_a_retry_on_the_next_day_sends_the_first_attempts_dates_byte_for_byte_and_finds_the_committed_movement()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 8);
        factory.PosterFault[s.Tenant] = CountPosterFault.CommitThenException;
        Assert.Equal(503, (int)(await s.ApproveAsync(line)).StatusCode);
        var pending = await s.LineAsync(countId, LineId(line));

        factory.CountClock.Advance(TimeSpan.FromDays(1));
        try
        {
            var retried = await Expect(s.RetryAsync(pending), 200);
            Assert.Equal("Posted", AdjustmentStatus(retried));
            Assert.Equal(Adjustment(pending).GetProperty("postingDate").GetString(), Adjustment(retried).GetProperty("postingDate").GetString());
            Assert.Single(s.CountLedgerMovements());
        }
        finally
        {
            factory.CountClock.Advance(TimeSpan.FromDays(-1));
        }
    }

    // ── AC-26 / AC-47 (e) ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A movement posted through MOD-0173's own endpoint under the line's key BEFORE 0181 posts (a caller's declaration — pack §2.4).</summary>
    private static async Task<JsonElement> SquatKeyAsync(CycleCountScenario s, string countId, JsonElement line, decimal quantity, string transactionDate)
    {
        var body = s.Inventory.Movement("COUNT_GAIN", Q(quantity));
        body["idempotencyKey"] = "cycle-count:" + LineId(line);
        body["toWarehouseId"] = s.Inventory.Warehouse;
        body["toLocationId"] = s.BinA.ToString("D");
        body["sourceModule"] = "MOD-0181";
        body["sourceType"] = "CYCLE_COUNT";
        body["sourceDocumentId"] = countId;
        body["sourceLineId"] = LineId(line);
        body["transactionDate"] = transactionDate;
        return await s.Inventory.PostExpectingAsync(body, 201);
    }

    [Fact]
    public async Task AC26_a_key_held_by_another_payload_is_an_integrity_error_never_a_refusal_and_AcceptHolder_needs_the_same_correction()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 12); // +2
        var squat = await SquatKeyAsync(s, countId, line, 1, "2026-10-01T10:00:00Z");

        var refused = await Expect(s.ApproveAsync(line), 409);
        Assert.Equal("COUNT_ADJUSTMENT_INTEGRITY", refused.GetProperty("error").GetProperty("code").GetString());
        var integrity = await s.LineAsync(countId, LineId(line));
        Assert.Equal("IntegrityError", AdjustmentStatus(integrity));
        Assert.Equal("KeyReused", Adjustment(integrity).GetProperty("integrityReason").GetString());
        Assert.False(Adjustment(integrity).TryGetProperty("refusalCode", out _));
        Assert.Single(s.CountLedgerMovements()); // only the squatter

        // the holder moved 1, the correction is 2 → not this line's correction
        Assert.Equal("COUNT_ADJUSTMENT_INTEGRITY", await ErrorCode(s.ResolveAsync(integrity, "AcceptHolder"), 409));
        Assert.Equal("IntegrityError", AdjustmentStatus(await s.LineAsync(countId, LineId(line))));
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.ResolveAsync(integrity, "Reject", reason: null), 400));
        Assert.Equal(403, (int)(await s.ResolveAsync(integrity, "Reject", token: s.TokenFor(Guid.NewGuid(), Read, VariancesRead, VariancesApprove))).StatusCode);

        var rejected = await Expect(s.ResolveAsync(integrity, "Reject"), 200);
        Assert.Equal("Rejected", Status(rejected));
        Assert.Equal("IntegrityError", AdjustmentStatus(rejected));
        Assert.Equal("Reject", rejected.GetProperty("integrityResolution").GetProperty("kind").GetString());
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Single(s.CountLedgerMovements());
        Assert.Equal(squat.GetProperty("transactionId").GetString(), s.CountLedgerMovements().Single()["_id"].AsString);
    }

    [Fact]
    public async Task AC26_AcceptHolder_takes_the_holder_as_the_correction_when_it_IS_the_same_correction_and_writes_no_new_movement()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 12); // +2
        var squat = await SquatKeyAsync(s, countId, line, 2, "2026-10-01T10:00:00Z"); // same correction, another request (its date)
        Assert.Equal(409, (int)(await s.ApproveAsync(line)).StatusCode);

        var accepted = await Expect(s.ResolveAsync(await s.LineAsync(countId, LineId(line)), "AcceptHolder"), 200);
        Assert.Equal("Approved", Status(accepted));
        Assert.Equal("Posted", AdjustmentStatus(accepted));
        Assert.Equal(squat.GetProperty("transactionId").GetString(), Adjustment(accepted).GetProperty("transactionId").GetString());
        Assert.Equal("AcceptHolder", accepted.GetProperty("integrityResolution").GetProperty("kind").GetString());
        Assert.Single(s.CountLedgerMovements());
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Single(s.CentralRows("ResolveCountAdjustmentIntegrityCommand"));
    }

    [Fact]
    public async Task AC26_a_reversed_holder_is_an_integrity_error_at_the_retry_and_cannot_be_accepted()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 12);
        factory.PosterFault[s.Tenant] = CountPosterFault.CommitThenException;
        Assert.Equal(503, (int)(await s.ApproveAsync(line)).StatusCode);
        var holder = Assert.Single(s.CountLedgerMovements());
        var reversal = s.Inventory.Reversal(new Dictionary<string, object?> { ["quantity"] = "2", ["uomId"] = "EA" }, holder["_id"].AsString);
        await s.Inventory.PostExpectingAsync(reversal, 201);

        var refused = await Expect(s.RetryAsync(await s.LineAsync(countId, LineId(line))), 409);
        Assert.Equal("COUNT_ADJUSTMENT_INTEGRITY", refused.GetProperty("error").GetProperty("code").GetString());
        var integrity = await s.LineAsync(countId, LineId(line));
        Assert.Equal("HolderReversed", Adjustment(integrity).GetProperty("integrityReason").GetString());
        Assert.Equal("COUNT_ADJUSTMENT_INTEGRITY", await ErrorCode(s.ResolveAsync(integrity, "AcceptHolder"), 409));
        Assert.Single(s.CountLedgerMovements());
    }

    // ── AC-27 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC27_a_loss_that_would_uncover_a_reservation_is_Refused_as_the_ledger_said_and_posts_after_the_release_with_the_same_key()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(10, s.BinA);
        var reservation = await s.Inventory.ReserveExpectingAsync(s.Inventory.Reservation("8"), 201);
        var (countId, line) = await s.SubmittedLineAsync(0, 1); // −9

        var answered = await Expect(s.ApproveAsync(line), 200);
        Assert.Equal("Refused", AdjustmentStatus(answered));
        Assert.Equal("INSUFFICIENT_STOCK", Adjustment(answered).GetProperty("refusalCode").GetString());
        Assert.Equal(422, Adjustment(answered).GetProperty("refusalStatus").GetInt32());
        Assert.True(Adjustment(answered).GetProperty("refusalDetails").TryGetProperty("level", out _));
        Assert.Equal(0, s.CountMovements());
        Assert.Equal(10m, await s.OnHandAsync(s.BinA)); // no partial quantity
        var trace = await Expect(s.GetAsync($"/api/warehouse/count-variances/{LineId(line)}", s.ApproverToken), 200);
        Assert.Equal("INSUFFICIENT_STOCK", trace.GetProperty("line").GetProperty("adjustment").GetProperty("refusalCode").GetString());

        using (var released = await s.Inventory.ReleaseAsync(reservation.GetProperty("reservationId").GetString()!, reservation.GetProperty("etag").GetString()))
            Assert.True(released.IsSuccessStatusCode, await released.Content.ReadAsStringAsync());
        var retried = await Expect(s.RetryAsync(answered), 200);
        Assert.Equal("Posted", AdjustmentStatus(retried));
        var movement = Assert.Single(s.CountLedgerMovements());
        Assert.Equal("cycle-count:" + LineId(line), movement["IdempotencyKey"].AsString);
        Assert.Equal(1m, await s.OnHandAsync(s.BinA));
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
    }

    // ── AC-28 ────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task AssertRefusedAsIsAsync(CycleCountScenario s, string countId, JsonElement line, string code)
    {
        var answered = await Expect(s.ApproveAsync(line), 200);
        Assert.Equal("Refused", AdjustmentStatus(answered));
        Assert.Equal(code, Adjustment(answered).GetProperty("refusalCode").GetString());
        foreach (var quantity in new[] { "expectedQuantity", "countedQuantity", "expectedAtCount", "varianceQuantity" })
            Assert.Equal(line.GetProperty(quantity).GetString(), answered.GetProperty(quantity).GetString());
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("Submitted", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
    }

    private static string RegisteredLot(CycleCountScenario s) => s.RegisterLot(s.Inventory.Item, s.Inventory.Sku);

    private static void SetLot(CycleCountScenario s, string lotId, UpdateDefinition<Lot> update)
        => s.Factory.Database.GetCollection<Lot>(TraceCollections.Lots).UpdateOne(Builders<Lot>.Filter.Eq(l => l.Id, Guid.Parse(lotId)), update);

    [Fact]
    public async Task AC28_LOT_BLOCKED_is_Refused_as_the_ledger_said()
    {
        var s = new CycleCountScenario(factory, lotControlled: true);
        var lot = RegisteredLot(s);
        var (countId, line) = await s.SubmittedLineAsync(10, 8, lot: lot);
        SetLot(s, lot, Builders<Lot>.Update.Set(l => l.Status, LotStatus.Blocked));
        await AssertRefusedAsIsAsync(s, countId, line, "LOT_BLOCKED");
    }

    [Fact]
    public async Task AC28_LOT_EXPIRED_is_Refused_as_the_ledger_said()
    {
        var s = new CycleCountScenario(factory, lotControlled: true);
        var lot = RegisteredLot(s);
        var (countId, line) = await s.SubmittedLineAsync(10, 12, lot: lot);
        SetLot(s, lot, Builders<Lot>.Update.Set(l => l.ExpiryDate, DateTimeOffset.UtcNow.AddDays(-2)));
        await AssertRefusedAsIsAsync(s, countId, line, "LOT_EXPIRED");
    }

    [Fact]
    public async Task AC28_SERIAL_COUNT_MISMATCH_is_Refused_as_the_ledger_said()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 8);
        Assert.False(line.GetProperty("serialControlled").GetBoolean());
        factory.Products.AddItem(s.Inventory.Item, serialControlled: true); // PRODUCT-MASTER flips the item after the release
        await AssertRefusedAsIsAsync(s, countId, line, "SERIAL_COUNT_MISMATCH");
    }

    [Fact]
    public async Task AC28_INSUFFICIENT_STOCK_of_the_source_bin_is_Refused_as_the_ledger_said()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 1); // −9
        await s.TransferAsync(5, s.BinA, s.BinB); // after the submit: 5 left in the bin
        await AssertRefusedAsIsAsync(s, countId, line, "INSUFFICIENT_STOCK");
    }

    // ── AC-34 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC34_a_lot_under_recall_is_counted_and_corrected_0181_adds_no_recall_or_FEFO_gate()
    {
        var s = new CycleCountScenario(factory, lotControlled: true);
        var lot = RegisteredLot(s);
        await s.ReceiveAsync(10, s.BinA, lot: lot);
        s.Factory.Database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks).InsertOne(new RecallLotBlock
        {
            Id = Guid.NewGuid(), TenantId = s.Tenant, LegalEntityId = s.LegalEntity, LotId = lot, CaseId = Guid.NewGuid(), CaseNumber = "GMG-RCL-000181",
            Active = true, ActivatedAtUtc = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        });
        var issue = s.Inventory.Issue("1", location: s.BinA.ToString("D"));
        issue["lotId"] = lot;
        await s.Inventory.PostExpectingAsync(issue, 422); // the recall gate is live for an ordinary exit

        var (lossCount, loss) = await s.SubmittedLineAsync(0, 7, lot: lot); // −3
        Assert.Equal("Posted", AdjustmentStatus(await Expect(s.ApproveAsync(loss), 200)));
        Assert.Equal("Closed", (await s.CurrentAsync(lossCount)).GetProperty("status").GetString());
        var (_, gain) = await s.SubmittedLineAsync(0, 9, lot: lot); // +2
        Assert.Equal("Posted", AdjustmentStatus(await Expect(s.ApproveAsync(gain), 200)));
        Assert.Equal(["CountGain", "CountLoss"], s.CountLedgerMovements().Select(m => m["MovementType"].AsString).Order().ToList());

        // FEFO / expiry: the count types are exempt BY TYPE (pack §8.10) — the ledger's own rule, whatever the source module says
        Assert.Equal(ExpiryHookKind.None, ExpiryHookScope.For(MovementType.CountLoss, StockStatus.Available, null, "MOD-0181", hasRegisteredLot: true));
        Assert.Equal(ExpiryHookKind.None, ExpiryHookScope.For(MovementType.CountGain, null, StockStatus.Available, "MOD-0181", hasRegisteredLot: true));
    }
}
