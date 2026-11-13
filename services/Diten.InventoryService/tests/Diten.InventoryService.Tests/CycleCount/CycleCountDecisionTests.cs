using System.Text.Json;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-3 — the decisions (pack §8.3, §8.5, §8.6, §8.9; AC-21, AC-22, AC-23, AC-24, AC-31, AC-33, AC-48, AC-52 (b) (c) DL-3 half) over the
/// REAL host and the REAL ledger: the corrections are read back from the ledger's own collection. The scenario's user counts AND submits; a second
/// user (<see cref="CycleCountScenario.Approver"/>) decides.
/// </summary>
public sealed class CycleCountDecisionTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static string Status(JsonElement line) => line.GetProperty("status").GetString()!;

    private static string AdjustmentStatus(JsonElement line) => line.GetProperty("adjustment").GetProperty("status").GetString()!;

    // ── AC-21 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC21_without_a_tolerance_every_variance_awaits_a_decision_nothing_posts_and_an_unapproved_line_takes_no_retry()
    {
        var s = new CycleCountScenario(factory);
        var defaults = await Expect(s.SettingsAsync(), 200);
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());
        var (countId, line) = await s.SubmittedLineAsync(10, 9.5m);
        Assert.Equal("AwaitingApproval", Status(line));
        Assert.False(line.TryGetProperty("decision", out _));
        Assert.Equal(0, s.CountMovements());

        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RetryAsync(line), 409));
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("Submitted", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC21_within_the_absolute_AND_percent_tolerance_the_system_approves_and_posts_above_it_nothing_posts()
    {
        var s = new CycleCountScenario(factory);
        await s.ToleranceAsync("2", "15");

        // −1 of 10 (10 %): within both → Approved by the tolerance, posted, the count closes
        var (inside, insideLine) = await s.SubmittedLineAsync(10, 9);
        Assert.Equal("Approved", Status(insideLine));
        Assert.Equal("Tolerance", insideLine.GetProperty("decision").GetProperty("source").GetString());
        Assert.False(insideLine.GetProperty("decision").TryGetProperty("by", out _));
        Assert.Equal("Posted", AdjustmentStatus(insideLine));
        Assert.Equal("Closed", (await s.CurrentAsync(inside)).GetProperty("status").GetString());
        var posted = Assert.Single(s.CountLedgerMovements());
        Assert.Equal("CountLoss", posted["MovementType"].AsString); // the stored enum name (wire COUNT_LOSS)

        // −3 of 10: above the absolute → awaits, nothing more posts
        var (_, above) = await s.SubmittedLineAsync(10, 7, bin: s.Node());
        Assert.Equal("AwaitingApproval", Status(above));
        // −2 of 10 (20 %): within the absolute, above the percent → awaits
        var (_, percent) = await s.SubmittedLineAsync(10, 8, bin: s.Node());
        Assert.Equal("AwaitingApproval", Status(percent));
        Assert.Single(s.CountLedgerMovements());
    }

    // ── AC-22 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC22_the_counter_and_the_submitter_may_not_approve_and_nothing_posts()
    {
        var s = new CycleCountScenario(factory);
        var counter = Guid.NewGuid();
        var submitter = Guid.NewGuid();
        await s.ReceiveAsync(10, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        var countId = Id(released);
        var open = Assert.Single(await s.LinesAsync(countId));
        await Expect(s.RecordAsync(countId, LineId(open), "8", ETag(open), token: s.TokenFor(counter, Read, CountsRecord)), 200);
        await Expect(s.SubmitAsync(countId, ETag(await s.CurrentAsync(countId)), token: s.TokenFor(submitter, Read, CountsRecord)), 200);
        var line = await s.LineAsync(countId, LineId(open));

        Assert.Equal("COUNT_SELF_APPROVAL_FORBIDDEN", await ErrorCode(s.ApproveAsync(line, token: s.TokenFor(counter, ApproverKeys)), 403));
        Assert.Equal("COUNT_SELF_APPROVAL_FORBIDDEN", await ErrorCode(s.ApproveAsync(line, token: s.TokenFor(submitter, ApproverKeys)), 403));
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("AwaitingApproval", Status(await s.LineAsync(countId, LineId(open))));

        var approved = await Expect(s.ApproveAsync(line), 200);
        Assert.Equal(s.Approver.ToString("D"), approved.GetProperty("decision").GetProperty("by").GetString());
        Assert.Single(s.CountLedgerMovements());
    }

    // ── AC-23 ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(12, "CountGain")]
    [InlineData(8, "CountLoss")]
    public async Task AC23_an_approval_posts_ONE_count_movement_with_the_line_as_its_source_and_closes_the_count(int counted, string type)
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, counted);
        var approved = await Expect(s.ApproveAsync(line), 200);

        Assert.Equal("Approved", Status(approved));
        Assert.Equal("Posted", AdjustmentStatus(approved));
        var movement = Assert.Single(s.CountLedgerMovements());
        var lineId = LineId(line);
        Assert.Equal(type, movement["MovementType"].AsString);
        Assert.Equal(2m, decimal.Parse(movement["Quantity"].ToString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("EA", movement["UomId"].AsString);
        Assert.Equal("CYCLE_COUNT", movement["SourceType"].AsString);
        Assert.Equal(countId, movement["SourceDocumentId"].AsString);
        Assert.Equal(lineId, movement["SourceLineId"].AsString);
        Assert.Equal("cycle-count:" + lineId, movement["IdempotencyKey"].AsString);
        Assert.Equal("CYCLE_COUNT_VARIANCE", movement["MovementReason"].AsString);
        var bin = s.BinA.ToString("D");
        if (type == "CountGain")
        {
            Assert.Equal(bin, movement["ToLocationId"].AsString);
            Assert.Equal("Available", movement["ToStockStatus"].AsString);
            Assert.True(movement["FromLocationId"].IsBsonNull);
        }
        else
        {
            Assert.Equal(bin, movement["FromLocationId"].AsString);
            Assert.True(movement["ToLocationId"].IsBsonNull);
        }

        var adjustment = approved.GetProperty("adjustment");
        Assert.Equal(movement["_id"].AsString, adjustment.GetProperty("transactionId").GetString());
        Assert.Equal(movement["TransactionNumber"].AsString, adjustment.GetProperty("transactionNumber").GetString());
        Assert.Equal(counted, await s.OnHandAsync(s.BinA));
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.All(s.Claims(countId), c => Assert.False(c["Open"].AsBoolean));
        var kinds = s.Events(countId).Select(e => e["Kind"].AsString).ToList();
        Assert.Equal(["Approved", "AdjustmentPending", "AdjustmentPosted", "Closed"], kinds.SkipWhile(k => k != "Approved").ToList());
        Assert.Single(s.CentralRows("ApproveCountVarianceCommand"));
    }

    // ── AC-24 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC24_approve_twice_approve_then_retry_and_two_concurrent_retries_leave_ONE_movement()
    {
        var s = new CycleCountScenario(factory);
        var (_, line) = await s.SubmittedLineAsync(10, 7);
        var key = NewKey();
        await Expect(s.ApproveAsync(line, key), 200);
        var replay = await Expect(s.ApproveAsync(line, key), 200); // same key, same body: the line as it is now
        Assert.Equal("Posted", AdjustmentStatus(replay));
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.ApproveAsync(line), 409)); // another key, the old ETag: stale (rule 6)
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.ApproveAsync(replay), 409)); // the current ETag: the count is closed
        Assert.Single(s.CountLedgerMovements());
        Assert.Single(s.CentralRows("ApproveCountVarianceCommand"));

        // approve whose answer is lost AFTER the ledger committed → Pending; the retry finds the key and posts nothing more
        var (countId2, line2) = await s.SubmittedLineAsync(10, 12, bin: s.Node());
        factory.PosterFault[s.Tenant] = CountPosterFault.CommitThenException;
        Assert.Equal("COUNT_INVENTORY_UNAVAILABLE", await ErrorCode(s.ApproveAsync(line2), 503));
        var pending = await s.LineAsync(countId2, LineId(line2));
        Assert.Equal("Pending", AdjustmentStatus(pending));
        Assert.Equal(2, s.CountLedgerMovements().Count);
        Assert.Equal("Posted", AdjustmentStatus(await Expect(s.RetryAsync(pending), 200)));
        Assert.Equal(2, s.CountLedgerMovements().Count);

        // two concurrent retries of a Pending correction whose posting never reached the ledger → exactly one movement
        var (countId3, line3) = await s.SubmittedLineAsync(10, 9, bin: s.Node());
        factory.PosterFault[s.Tenant] = CountPosterFault.Status503;
        Assert.Equal(503, (int)(await s.ApproveAsync(line3)).StatusCode);
        var pending3 = await s.LineAsync(countId3, LineId(line3));
        var answers = await Task.WhenAll(s.RetryAsync(pending3), s.RetryAsync(pending3));
        Assert.Contains(answers, a => (int)a.StatusCode == 200);
        Assert.All(answers, a => Assert.Contains((int)a.StatusCode, new[] { 200, 409 }));
        Assert.Equal(3, s.CountLedgerMovements().Count);
        Assert.Single(s.CountLedgerMovements(), m => m["SourceLineId"].AsString == LineId(line3));
        Assert.Equal("Posted", AdjustmentStatus(await s.LineAsync(countId3, LineId(line3))));
    }

    // ── AC-31 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC31_a_reject_needs_a_reason_and_posts_nothing_and_the_last_final_line_closes_the_count()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 6);
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.RejectAsync(line, reason: null), 400));
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.RejectAsync(line, reason: "   "), 400));
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.RejectAsync(line, reason: new string('r', 501)), 400));
        Assert.Equal("AwaitingApproval", Status(await s.LineAsync(countId, LineId(line))));

        var rejected = await Expect(s.RejectAsync(line), 200);
        Assert.Equal("Rejected", Status(rejected));
        Assert.Equal(Reason, rejected.GetProperty("decision").GetProperty("reason").GetString());
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Single(s.CentralRows("RejectCountVarianceCommand"));
    }

    // ── AC-33 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC33_a_closed_count_takes_no_write()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 6);
        await Expect(s.RejectAsync(line), 200);
        var closed = await s.CurrentAsync(countId);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        var now = await s.LineAsync(countId, LineId(line));

        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RecordAsync(countId, LineId(now), "6", ETag(now)), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.CancelAsync(countId, ETag(closed)), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.SubmitAsync(countId, ETag(closed)), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.AddLineAsync(countId, s.FoundLine(s.BinA, s.AddSku())), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.ApproveAsync(now), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RejectAsync(now), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RecountAsync(countId, ETag(closed), [LineId(now)]), 409));
        Assert.Equal(closed.GetProperty("version").GetInt32(), (await s.CurrentAsync(countId)).GetProperty("version").GetInt32());
    }

    // ── AC-30 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC30_held_buckets_are_no_line_and_no_correction_touches_them_a_gain_goes_to_AVAILABLE()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        await s.ReceiveAsync(3, s.BinA, status: "QUARANTINE");
        await s.ReceiveAsync(1, s.BinA, status: "QUALITY_INSPECTION");
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        var open = Assert.Single(await s.LinesAsync(Id(released)));
        Assert.Equal("AVAILABLE", open.GetProperty("stockStatus").GetString());
        await s.CountedAsync(Id(released), open, 7);
        await s.SubmittedAsync(Id(released));
        Assert.Equal("Posted", AdjustmentStatus(await Expect(s.ApproveAsync(await s.LineAsync(Id(released), LineId(open))), 200)));

        var gain = Assert.Single(s.CountLedgerMovements());
        Assert.Equal("Available", gain["ToStockStatus"].AsString);
        Assert.Equal(3m, await s.Inventory.OnHandAsync(s.BinA.ToString("D"), "QUARANTINE"));
        Assert.Equal(1m, await s.Inventory.OnHandAsync(s.BinA.ToString("D"), "QUALITY_INSPECTION"));
        Assert.Equal(7m, await s.Inventory.OnHandAsync(s.BinA.ToString("D"), "AVAILABLE"));
    }

    // ── AC-48 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC48_a_serial_line_has_its_variance_but_never_posts_and_takes_reject_or_recount()
    {
        var s = new CycleCountScenario(factory);
        await s.ToleranceAsync("5", "50"); // within any tolerance — a serial line still awaits
        var serial = s.AddSku(serialControlled: true);
        await s.ReceiveAsync(3, s.BinA, serial, serials: ["SN-1", "SN-2", "SN-3"]);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(skus: [serial.Sku])));
        var countId = Id(released);
        var open = Assert.Single(await s.LinesAsync(countId));
        Assert.True(open.GetProperty("serialControlled").GetBoolean());
        // DL-2 rule, measured here with REAL serial stock: a serial-controlled SKU is counted whole
        Assert.Equal("COUNT_QUANTITY_INVALID", await ErrorCode(s.RecordAsync(countId, LineId(open), "2.5", ETag(open)), 400));
        await s.CountedAsync(countId, open, 2);
        await s.SubmittedAsync(countId);
        var line = await s.LineAsync(countId, LineId(open));
        Assert.Equal("AwaitingApproval", Status(line));
        Assert.Equal("-1.000", line.GetProperty("varianceQuantity").GetString());
        var trace = await Expect(s.GetAsync($"/api/warehouse/count-variances/{LineId(line)}", s.ApproverToken), 200);
        Assert.Equal("-1.000", trace.GetProperty("line").GetProperty("varianceQuantity").GetString());

        Assert.Equal("COUNT_SERIAL_NOT_POSTABLE", await ErrorCode(s.ApproveAsync(line), 422));
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("Rejected", Status(await Expect(s.RejectAsync(line), 200)));

        // recount works on a serial line too
        var released2 = await s.ReleasedAsync(await s.CreatedAsync(s.Form(skus: [serial.Sku])));
        var open2 = Assert.Single(await s.LinesAsync(Id(released2)));
        await s.CountedAsync(Id(released2), open2, 1);
        var submitted2 = await s.SubmittedAsync(Id(released2));
        var recount = await Expect(s.RecountAsync(Id(released2), ETag(submitted2), [LineId(open2)]), 201);
        Assert.True(Assert.Single(await s.LinesAsync(Id(recount))).GetProperty("serialControlled").GetBoolean());
        Assert.Equal(0, s.CountMovements());
    }

    // ── AC-52 (b) (c), the DL-3 half ─────────────────────────────────────────────────────────────────────

    private async Task<(CycleCountScenario S, string CountId, JsonElement Line)> DriftAsync(decimal counted)
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        factory.CountClock.Advance(TimeSpan.FromMinutes(15));
        try
        {
            var released = await s.ReleasedAsync(await s.CreatedAsync());
            await s.ReceiveAsync(2, s.BinA);
            await s.CountedAsync(Id(released), Assert.Single(await s.LinesAsync(Id(released))), counted);
            await s.SubmittedAsync(Id(released));
            var line = Assert.Single(await s.LinesAsync(Id(released), s.ApproverToken));
            Assert.True(line.GetProperty("snapshotDrift").GetBoolean());
            return (s, Id(released), line);
        }
        finally
        {
            factory.CountClock.Advance(TimeSpan.FromMinutes(-15));
        }
    }

    [Fact]
    public async Task AC52b_a_drift_line_is_never_approved_or_posted_and_a_recount_takes_it()
    {
        var (s, countId, line) = await DriftAsync(7);
        Assert.Equal("COUNT_SNAPSHOT_DRIFT", await ErrorCode(s.ApproveAsync(line), 409));
        Assert.Equal(0, s.CountMovements());
        Assert.Equal("AwaitingApproval", Status(await s.LineAsync(countId, LineId(line))));

        var recount = await Expect(s.RecountAsync(countId, ETag(await s.CurrentAsync(countId)), [LineId(line)]), 201);
        Assert.Equal("Recounted", Status(await s.LineAsync(countId, LineId(line))));
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Equal("7.000", Assert.Single(await s.LinesAsync(Id(recount), s.ApproverToken)).GetProperty("expectedQuantity").GetString());
        Assert.Equal(0, s.CountMovements());
    }

    [Fact]
    public async Task AC52c_a_variance_zero_drift_line_is_not_approved_only_reject_or_recount_makes_it_final()
    {
        var (s, countId, line) = await DriftAsync(5);
        Assert.Equal("0.000", line.GetProperty("varianceQuantity").GetString());
        Assert.Equal("COUNT_SNAPSHOT_DRIFT", await ErrorCode(s.ApproveAsync(line), 409));
        Assert.Equal("Submitted", (await s.CurrentAsync(countId)).GetProperty("status").GetString());

        Assert.Equal("Rejected", Status(await Expect(s.RejectAsync(line), 200)));
        Assert.Equal("Closed", (await s.CurrentAsync(countId)).GetProperty("status").GetString());
        Assert.Equal(0, s.CountMovements());
    }
}
