using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-3 — the ledger guard (pack §8.8, Q-06 = A; AC-31 … AC-35). Every REVERSAL goes through the REAL <c>postMovement</c>
/// (Gateway-shaped API call, production <c>InternalMovementPostingService</c> + <c>QuarantineReversalPrecondition</c>); the record moves on FIRST
/// and the REVERSAL is asked AFTER (the natural order). A refusal is 422 <c>REVERSAL_QUALITY_RECORD_LOCKED</c> with its cause, writes nothing to the
/// ledger (transactions, balances) or the record, and leaves one <c>inventory_audit</c> row with <c>Outcome Failed</c>. The guard is never
/// staged to Allow; the DL-1 criteria keep their own kurgu (pack §16) and run unchanged in the full suite (AC-33).
/// </summary>
public sealed class SourceReversalGuardTests : IClassFixture<HookedQuarantineApiFactory>
{
    private const string Locked = "REVERSAL_QUALITY_RECORD_LOCKED";
    private readonly HookedQuarantineApiFactory _factory;

    public SourceReversalGuardTests(HookedQuarantineApiFactory factory) => _factory = factory;

    private static object Release(decimal q) => QuarantineScenario.Line(1, "Release", q);

    private static string HoldIdOf(ReversalWorld w, Posted p) => w.HoldOfSource(p.Tx)["_id"].AsString;

    /// <summary>What a refusal must leave untouched: ledger transactions, the balances at both bins in every status, the records.</summary>
    private static async Task<string> SnapshotAsync(ReversalWorld w)
    {
        var parts = new List<string> { w.LedgerCount().ToString(CultureInfo.InvariantCulture) };
        foreach (var location in new[] { w.S.Ledger.LocationA, w.S.Ledger.LocationB })
        {
            parts.Add((await w.S.Ledger.OnHandAsync(location)).ToString(CultureInfo.InvariantCulture));
            foreach (var status in new[] { "AVAILABLE", "QUARANTINE", "QUALITY_INSPECTION", "BLOCKED" })
                parts.Add((await w.S.Ledger.OnHandAsync(location, status)).ToString(CultureInfo.InvariantCulture));
        }

        parts.AddRange(w.S.Holds().OrderBy(h => h["_id"].AsString).Select(h => h.ToJson()));
        parts.AddRange(w.S.Blocked().OrderBy(b => b["_id"].AsString).Select(b => b.ToJson()));
        return string.Join("|", parts);
    }

    private static List<BsonDocument> RefusalRows(ReversalWorld w, string originalTx)
        => w.S.Ledger.Persisted("inventory_audit")
            .Where(a => a.GetValue("Action", BsonNull.Value) == QuarantineReversalRefusalAuditRecord.RefusedAction && a["OriginalTransactionId"].AsString == originalTx).ToList();

    /// <summary>The refusal answer + its audit row; <paramref name="recordNumber"/> null = the original is no record's source.</summary>
    private static void AssertLocked(ReversalWorld w, JsonElement refusal, string originalTx, string cause, string? recordNumber, string? recordType)
    {
        Assert.Equal(Locked, QuarantineScenario.Code(refusal));
        var details = refusal.GetProperty("error").GetProperty("details");
        Assert.Equal(cause, details.GetProperty("cause").GetString());
        if (recordNumber is null)
        {
            Assert.False(details.TryGetProperty("recordNumber", out _));
            Assert.False(details.TryGetProperty("recordType", out _));
        }
        else
        {
            Assert.Equal(recordNumber, details.GetProperty("recordNumber").GetString());
            Assert.Equal(recordType, details.GetProperty("recordType").GetString());
        }

        var row = Assert.Single(RefusalRows(w, originalTx));
        Assert.Equal(("Failed", Locked, cause), (row["Outcome"].AsString, row["ErrorCode"].AsString, row["Cause"].AsString));
        Assert.Equal(recordNumber is null ? BsonNull.Value : (BsonValue)recordNumber, row.GetValue("RecordNumber", BsonNull.Value));
        Assert.Equal(w.S.Ledger.User.ToString("D"), row["UserId"].AsString);
    }

    private static async Task<JsonElement> RefusedAsync(ReversalWorld w, Posted original, string cause, string recordNumber, string recordType)
    {
        var before = await SnapshotAsync(w);
        var refusal = await w.ReverseExpectingAsync(original, 422);
        AssertLocked(w, refusal, original.Tx, cause, recordNumber, recordType);
        Assert.Equal(before, await SnapshotAsync(w));
        return refusal;
    }

    // ── AC-31: the REVERSAL that would leave a record out of step is refused ─────────────────────────────────────

    /// <summary>AC-31 (R7): the AC-6 bucket — A decided (Release applied), B's 4 in the same bucket → A's source REVERSAL 422 Decided.</summary>
    [Fact]
    public async Task AC31a_the_source_of_a_decided_hold_is_locked_Decided()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        await w.ReceiveAsync("4", lot, "QUARANTINE");
        var aId = HoldIdOf(w, a);
        Assert.Equal("Applied", Id(await w.S.DecideAndApproveAsync(w.S.DecideAsync(aId, [Release(4m)])), "state"));

        await RefusedAsync(w, a, "Decided", w.HoldDoc(aId)["HoldNumber"].AsString, "QuarantineHold");
        Assert.True(w.HoldDoc(aId).GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull); // nothing to mark: nothing was reversed
    }

    /// <summary>AC-31 (R6): the AC-7 bucket — a two-line decision with line 1 posted (stopped before line 2) → 422 LinesPosted.</summary>
    [Fact]
    public async Task AC31b_the_source_of_a_hold_whose_decision_posted_a_line_is_locked_LinesPosted()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        await w.ReceiveAsync("2", lot, "QUARANTINE");
        var aId = HoldIdOf(w, a);
        var draft = await w.S.DecideAsync(aId, [QuarantineScenario.Line(1, "Release", 2m), QuarantineScenario.Line(2, "Block", 2m)], "DEVIATION");
        var decision = Id(draft, "decisionId");
        await w.S.SubmitAsync(decision);

        var pause = new Pause();
        _factory.Hooks.Before = async c =>
        {
            if (c.Service == typeof(global::Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository) && c.Method == "FindByIdempotencyKeyAsync"
                && c.Arg<string>(0) == $"quarantine:{decision}:2" && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = ReversalHooks.Detached(() => w.S.OutcomeAsync(decision));
            await pause.Reached; // line 1 is in the ledger, line 2 not yet
            await RefusedAsync(w, a, "LinesPosted", w.HoldDoc(aId)["HoldNumber"].AsString, "QuarantineHold");
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }
    }

    /// <summary>AC-31 (R9): the AC-10 bucket — X lost 1 of its 3 to a Destroy disposition → X's source REVERSAL 422 QuantityChanged.</summary>
    [Fact]
    public async Task AC31c_the_source_of_a_blocked_record_that_lost_quantity_is_locked_QuantityChanged()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("3", lot, "BLOCKED");
        await w.ReceiveAsync("3", lot, "BLOCKED");
        var xId = w.BlockedOfSource(x.Tx)["_id"].AsString;
        var destroy = await w.S.DisposeAsync(xId, new { kind = "Destroy", quantity = 1m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        await w.S.SubmitAsync(Id(destroy, "decisionId"));
        Assert.Equal("Applied", Id(await w.S.OutcomeAsync(Id(destroy, "decisionId")), "state"));

        await RefusedAsync(w, x, "QuantityChanged", w.BlockedDoc(xId)["BlockedNumber"].AsString, "BlockedStock");
    }

    /// <summary>AC-31 (R5): an Approved decision stopped before its line (the AC-4 X) → 422 DecisionInProgress; the approval then applies.</summary>
    [Fact]
    public async Task AC31d_the_source_of_a_hold_whose_decision_is_being_applied_is_locked_DecisionInProgress()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var xId = HoldIdOf(w, x);
        var draft = await w.S.DecideAsync(xId, [Release(4m)]);
        var decision = Id(draft, "decisionId");
        await w.S.SubmitAsync(decision);

        var pause = new Pause();
        _factory.Hooks.After = async c =>
        {
            if (SourceReversalSweepTests.IsApprovedRead(c, decision) && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = ReversalHooks.Detached(() => w.S.OutcomeAsync(decision));
            await pause.Reached; // DecisionPending + Approved, 0 lines
            await RefusedAsync(w, x, "DecisionInProgress", w.HoldDoc(xId)["HoldNumber"].AsString, "QuarantineHold");
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }

        Assert.Equal("Decided", w.HoldDoc(xId)["Status"].AsString); // the refusal changed nothing the approval needed
    }

    /// <summary>AC-31 (R13, fail-closed): an intake hold written DecisionPending + a Draft active decision (outside the table) → 422 Unmatched.</summary>
    [Fact]
    public async Task AC31e_the_source_of_a_hold_in_a_state_outside_the_table_is_locked_Unmatched()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var odd = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var oddId = HoldIdOf(w, odd);
        var draft = await w.S.DecideAsync(oddId, [Release(4m)]);
        _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", oddId), new BsonDocument("$set",
            new BsonDocument { ["Status"] = "DecisionPending", ["ActiveDecisionId"] = Id(draft, "decisionId") }));

        await RefusedAsync(w, odd, "Unmatched", w.HoldDoc(oddId)["HoldNumber"].AsString, "QuarantineHold");
    }

    private static string RuleOf(ReversalWorld w, string originalTx) => Assert.Single(RefusalRows(w, originalTx))["Rule"].AsString;

    /// <summary>
    /// AC-31 (R5a, WP-0175-FU01-DL3-FIX-01 ORTA-2): a Draft lot's Release (the decision changes the lot) stopped right before its first line —
    /// the lot step has run (lot Released, keyed row), no line posted → 422 DecisionInProgress (rule R5a); the approval then applies.
    /// </summary>
    [Fact]
    public async Task AC31f_the_source_of_a_hold_whose_decision_changed_the_lot_is_locked_DecisionInProgress()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z"); // Draft: the Release releases the lot (CT-Q5)
        var source = await w.ReceiveAsync("4", lot, "QUALITY_INSPECTION");
        var holdId = HoldIdOf(w, source);
        var decision = Id(await w.S.DecideAsync(holdId, [Release(4m)]), "decisionId");
        await w.S.SubmitAsync(decision);

        var pause = new Pause();
        _factory.Hooks.Before = async c =>
        {
            if (c.Service == typeof(global::Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository) && c.Method == "FindByIdempotencyKeyAsync"
                && c.Arg<string>(0) == $"quarantine:{decision}:1" && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = ReversalHooks.Detached(() => w.S.OutcomeAsync(decision));
            await pause.Reached; // the lot step ran, line 1 not posted
            Assert.Equal("Released", w.S.Lots.LotDocument(lot)["Status"].AsString);
            await RefusedAsync(w, source, "DecisionInProgress", w.HoldDoc(holdId)["HoldNumber"].AsString, "QuarantineHold");
            Assert.Equal("R5a", RuleOf(w, source.Tx));
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }
    }

    /// <summary>
    /// AC-31 (R5, ApplyFailed — ORTA-2): the quarantined 4 left the bucket by a ledger movement after the submit, so the approval's only line is
    /// refused (0 lines, ApplyFailed) → the source REVERSAL is 422 DecisionInProgress (rule R5).
    /// </summary>
    [Fact]
    public async Task AC31g_the_source_of_a_hold_whose_decision_failed_without_a_line_is_locked_DecisionInProgress()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var holdId = HoldIdOf(w, source);
        var decision = Id(await w.S.DecideAsync(holdId, [Release(4m)]), "decisionId");
        await w.S.SubmitAsync(decision);
        var away = w.S.Lots.StatusTransfer("4", lot);
        away["fromStockStatus"] = "QUARANTINE";
        away["toStockStatus"] = "BLOCKED";
        await w.S.Lots.PostOkAsync(away);
        Assert.Equal("ApplyFailed", Id(await w.S.OutcomeAsync(decision), "state"));
        Assert.Equal("DecisionPending", w.HoldDoc(holdId)["Status"].AsString);
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString.StartsWith($"quarantine:{decision}:", StringComparison.Ordinal));

        await RefusedAsync(w, source, "DecisionInProgress", w.HoldDoc(holdId)["HoldNumber"].AsString, "QuarantineHold");
        Assert.Equal("R5", RuleOf(w, source.Tx));
    }

    /// <summary>
    /// AC-31 (R10 — ORTA-2): an intake blocked record whose Destroy line is in the ledger but not yet recorded Applied (stopped right after the
    /// posting) → 422 LinesPosted.
    /// </summary>
    [Fact]
    public async Task AC31h_the_source_of_a_blocked_record_whose_decision_posted_a_line_is_locked_LinesPosted()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("3", lot, "BLOCKED");
        await w.ReceiveAsync("3", lot, "BLOCKED");
        var xId = w.BlockedOfSource(x.Tx)["_id"].AsString;
        var destroy = Id(await w.S.DisposeAsync(xId, new { kind = "Destroy", quantity = 1m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }), "decisionId");
        await w.S.SubmitAsync(destroy);

        var pause = new Pause();
        _factory.Hooks.After = async c =>
        {
            if (c.Service == typeof(global::Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository) && c.Method == "PostAsync"
                && c.Arg<global::Diten.InventoryService.Domain.Rules.LedgerPosting>(0).Transaction.IdempotencyKey.StartsWith($"quarantine:{destroy}:", StringComparison.Ordinal)
                && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = ReversalHooks.Detached(() => w.S.OutcomeAsync(destroy));
            await pause.Reached; // the SCRAP line is in the ledger, the record still Open with its 3
            Assert.Equal(("Open", 3m), (w.BlockedDoc(xId)["Status"].AsString, w.BlockedDoc(xId)["Quantity"].ToDecimal()));
            await RefusedAsync(w, x, "LinesPosted", w.BlockedDoc(xId)["BlockedNumber"].AsString, "BlockedStock");
            Assert.Equal("R10", RuleOf(w, x.Tx));
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }
    }

    /// <summary>AC-31 (R11 — ORTA-2): an intake blocked record whose Destroy of all 3 was applied (Closed) → 422 Decided.</summary>
    [Fact]
    public async Task AC31i_the_source_of_a_closed_blocked_record_is_locked_Decided()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("3", lot, "BLOCKED");
        await w.ReceiveAsync("3", lot, "BLOCKED");
        var xId = w.BlockedOfSource(x.Tx)["_id"].AsString;
        var destroy = Id(await w.S.DisposeAsync(xId, new { kind = "Destroy", quantity = 3m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }), "decisionId");
        await w.S.SubmitAsync(destroy);
        Assert.Equal("Applied", Id(await w.S.OutcomeAsync(destroy), "state"));
        Assert.Equal("Closed", w.BlockedDoc(xId)["Status"].AsString);

        await RefusedAsync(w, x, "Decided", w.BlockedDoc(xId)["BlockedNumber"].AsString, "BlockedStock");
        Assert.Equal("R11", RuleOf(w, x.Tx));
    }

    // ── AC-32: a MOD-0175 movement is never reversed around the quality decision ─────────────────────────────────

    private static object Bucket(ReversalWorld w, string lot, decimal quantity, string? location = null)
        => new { itemId = w.S.Ledger.Item, skuId = w.S.Ledger.Sku, skuLevel = "Lsku", warehouseId = w.S.Ledger.Warehouse, locationId = location ?? w.S.Ledger.LocationA, quantity, lotId = lot };

    private static Posted Persisted(ReversalWorld w, string tx)
    {
        var doc = w.S.Ledger.Persisted("inventory_transactions").Single(t => t["_id"].AsString == tx);
        Assert.Equal("MOD-0175", doc["SourceModule"].AsString);
        return new Posted(tx, new Dictionary<string, object?>
        {
            ["quantity"] = doc["Quantity"].ToDecimal().ToString("0.######", CultureInfo.InvariantCulture),
            ["uomId"] = doc["UomId"].AsString
        });
    }

    /// <summary>
    /// AC-32: "Karantinaya Al" (Manual, Retest), "Stoğu bloke et" (ManualBlock), a decision line (Release), Rework and the sample move of a
    /// MOD-0175 decision — each REVERSAL is 422 OwnMovementReversed, recordNumber filled where the movement is a record's source; the stock
    /// does not go back to AVAILABLE outside a quality decision.
    /// </summary>
    [Fact]
    public async Task AC32_a_reversal_of_a_MOD_0175_movement_is_locked_OwnMovementReversed()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        await w.ReceiveAsync("100", lot, "AVAILABLE");
        await w.ReceiveAsync("40", lot, "AVAILABLE", location: w.S.Ledger.LocationB);
        var cases = new List<(string Kind, string Tx, string? Number, string? Type)>();

        var key = QuarantineScenario.Key("man");
        var manual = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 10m) } }, 201, key))
            .EnumerateArray().Single();
        var manualDoc = w.HoldDoc(Id(manual, "holdId"));
        cases.Add(("Manual", manualDoc["SourceMovementId"].AsString, manualDoc["HoldNumber"].AsString, "QuarantineHold"));

        var due = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(400)));
        await w.S.Lots.ReleaseAsync(due);
        await w.S.Lots.PostOkAsync(w.S.Lots.Receipt("6", due, location: w.S.Ledger.LocationB));
        await w.S.Lots.SetRetestDateAsync(due, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2));
        key = QuarantineScenario.Key("rt");
        var retest = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = due, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key))
            .EnumerateArray().Single();
        var retestDoc = w.HoldDoc(Id(retest, "holdId"));
        cases.Add(("Retest", retestDoc["SourceMovementId"].AsString, retestDoc["HoldNumber"].AsString, "QuarantineHold"));

        key = QuarantineScenario.Key("blk");
        var manualBlock = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 8m) } }, 201, key)).EnumerateArray().Single();
        var blockDoc = w.BlockedDoc(Id(manualBlock, "blockedId"));
        cases.Add(("ManualBlock", blockDoc["SourceMovementId"].AsString, blockDoc["BlockedNumber"].AsString, "BlockedStock"));

        // a decision line (Release of an intake hold): a MOD-0175 STATUS_TRANSFER that is no record's source
        var intake = await w.ReceiveAsync("5", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        var released = await w.S.DecideAndApproveAsync(w.S.DecideAsync(HoldIdOf(w, intake), [Release(5m)]));
        var line = Assert.Single(await w.S.LedgerMovementsAsync(Id(released, "decisionNumber"))).GetProperty("transactionId").GetString()!;
        cases.Add(("ReleaseLine", line, null, null));

        // Rework: the rework disposition's BLOCKED → QUARANTINE move is the Rework hold's source
        key = QuarantineScenario.Key("blk2");
        var toRework = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 3m, w.S.Ledger.LocationB) } }, 201, key)).EnumerateArray().Single();
        var rework = await w.S.DisposeAsync(Id(toRework, "blockedId"), new { kind = "Rework", quantity = 3m });
        await w.S.SubmitAsync(Id(rework, "decisionId"));
        Assert.Equal("Applied", Id(await w.S.OutcomeAsync(Id(rework, "decisionId")), "state"));
        var reworkHold = w.S.Holds().Single(h => h["Source"].AsString == "Rework");
        cases.Add(("Rework", reworkHold["SourceMovementId"].AsString, reworkHold["HoldNumber"].AsString, "QuarantineHold"));

        // Sample: the sample disposition's LOCATION_TRANSFER (no record's source)
        key = QuarantineScenario.Key("blk3");
        var toSample = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 4m) } }, 201, key)).EnumerateArray().Single();
        await w.S.SaveSettingsAsync(new
        {
            approverCandidates = new[] { "position:" + Guid.NewGuid().ToString("D") },
            sampleLocations = new Dictionary<string, string> { [w.S.Ledger.Warehouse] = w.S.Ledger.LocationB }
        });
        var sample = await w.S.DecideAndApproveAsync(w.S.DisposeAsync(Id(toSample, "blockedId"), new { kind = "Sample", quantity = 4m }));
        var sampleMove = Assert.Single(await w.S.LedgerMovementsAsync(Id(sample, "decisionNumber")));
        Assert.Equal("LOCATION_TRANSFER", sampleMove.GetProperty("movementType").GetString());
        cases.Add(("Sample", sampleMove.GetProperty("transactionId").GetString()!, null, null));

        foreach (var (kind, tx, number, type) in cases)
        {
            var before = await SnapshotAsync(w);
            var refusal = await w.ReverseExpectingAsync(Persisted(w, tx), 422);
            AssertLocked(w, refusal, tx, "OwnMovementReversed", number, type);
            Assert.True(before == await SnapshotAsync(w), kind + ": the refusal changed the ledger or a record");
        }
    }

    /// <summary>
    /// AC-32, Recall (WP-0175-FU01-DL3-FIX-01 ORTA-1): "Karantinaya Al" of a recall — the MOD-0177 in-process port puts the stock on a Recall
    /// hold with a MOD-0175 STATUS_TRANSFER; its REVERSAL is 422 OwnMovementReversed with the hold's number, and the stock does not go back to
    /// AVAILABLE.
    /// </summary>
    [Fact]
    public async Task AC32b_a_reversal_of_the_recall_hold_movement_is_locked_OwnMovementReversed()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        await w.ReceiveAsync("20", lot, "AVAILABLE");
        using (var scope = w.Scope())
        {
            var caseId = Guid.NewGuid();
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>().QuarantineAsync(new RecallQuarantineCall(
                [new RecallQuarantineBucket(w.S.Ledger.Item, w.S.Ledger.Sku, w.S.Ledger.Level, w.S.Ledger.Warehouse, w.S.Ledger.LocationA, 5m, lot)],
                "Geri çağırma RC-2026-0002 · Tedarikci sapma bildirimi", $"recall:{caseId:D}:r1:Stock:{lot}", caseId, "RC-2026-0002")));
        }

        var recall = w.S.Holds().Single(h => h["Source"].AsString == "Recall");
        var tx = recall["SourceMovementId"].AsString;
        Assert.Equal(5m, await w.S.OnHandAsync("QUARANTINE"));
        var before = await SnapshotAsync(w);

        var refusal = await w.ReverseExpectingAsync(Persisted(w, tx), 422);

        AssertLocked(w, refusal, tx, "OwnMovementReversed", recall["HoldNumber"].AsString, "QuarantineHold");
        Assert.Equal(before, await SnapshotAsync(w));
        Assert.Equal((5m, 15m), (await w.S.OnHandAsync("QUARANTINE"), await w.S.OnHandAsync("AVAILABLE")));
    }

    // ── AC-33: the guard does not stand in the way of a closable record ─────────────────────────────────────────

    /// <summary>
    /// AC-33: an Open intake hold and a PendingApproval one — their source REVERSAL is 201 and DL-1 closes the record (Voided); an unrelated
    /// MOD-0173 movement's REVERSAL is 201. No refusal row. (The submit window, R3, is AC-13 (b): its REVERSAL goes through postMovement
    /// during the window and stays 201 with the guard on — measured unchanged in the full suite.)
    /// </summary>
    [Fact]
    public async Task AC33_closable_records_and_unrelated_movements_are_reversed_as_before()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var open = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var pending = await w.ReceiveAsync("3", lot, "QUALITY_INSPECTION", location: w.S.Ledger.LocationB);
        var unrelated = await w.ReceiveAsync("6", lot, "AVAILABLE");
        var draft = await w.S.DecideAsync(HoldIdOf(w, pending), [Release(3m)]);
        await w.S.SubmitAsync(Id(draft, "decisionId"));
        Assert.Equal("DecisionPending", w.HoldOfSource(pending.Tx)["Status"].AsString);

        foreach (var p in new[] { open, pending, unrelated }) await w.ReverseAsync(p);

        Assert.All(new[] { open, pending }, p => Assert.Equal("Voided", w.HoldOfSource(p.Tx)["Status"].AsString));
        Assert.All(new[] { open, pending, unrelated }, p => Assert.Empty(RefusalRows(w, p.Tx)));
    }

    // ── AC-34: fail-closed ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-34: the guard's quarantine read throws → 503 INVENTORY_UNAVAILABLE; nothing is written (ledger, record, refusal row).</summary>
    [Fact]
    public async Task AC34_a_failed_guard_read_answers_503_and_writes_nothing()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var before = await SnapshotAsync(w);
        _factory.Hooks.Before = c => c.Service == typeof(IQuarantineRepository) && c.Method == nameof(IQuarantineRepository.FindSourceRecordAsync)
                                     && c.Arg<string>(0) == source.Tx
            ? throw new InvalidOperationException("staged guard read failure")
            : Task.CompletedTask;
        try
        {
            var refusal = await w.ReverseExpectingAsync(source, 503);
            Assert.Equal("INVENTORY_UNAVAILABLE", QuarantineScenario.Code(refusal));
        }
        finally
        {
            _factory.Hooks.Clear();
        }

        Assert.Equal(before, await SnapshotAsync(w));
        Assert.Empty(RefusalRows(w, source.Tx));
        Assert.Equal("Open", w.HoldOfSource(source.Tx)["Status"].AsString);
    }

    // ── AC-35: tenant + company scope ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-35: a Decided hold of T2 (same company id) and one of T1's other company, both carrying the id of a T1 / LE1 movement that has no record
    /// in T1 / LE1 — the guard never reaches them: T1's REVERSAL is 201, no refusal row.
    /// </summary>
    [Fact]
    public async Task AC35_a_decided_record_of_another_tenant_or_company_does_not_lock_the_reversal()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var decided = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var decidedId = HoldIdOf(w, decided);
        await w.S.DecideAndApproveAsync(w.S.DecideAsync(decidedId, [Release(4m)]));
        var available = await w.ReceiveAsync("5", lot, "AVAILABLE"); // no record in T1 / LE1
        var other = new ReversalWorld(_factory);
        var hold = w.HoldDoc(decidedId);
        Assert.Equal("Decided", hold["Status"].AsString);
        BsonDocument Clone(Guid tenant, Guid company)
        {
            var copy = hold.DeepClone().AsBsonDocument;
            copy["_id"] = Guid.NewGuid().ToString("D");
            copy["TenantId"] = tenant.ToString("D");
            copy["LegalEntityId"] = company.ToString("D");
            copy["SourceMovementId"] = available.Tx;
            copy["HoldNumber"] = "KRN-2026-" + Random.Shared.Next(100000, 999999);
            return copy;
        }

        _factory.Collection("quarantine_holds").InsertMany([Clone(other.S.Tenant, w.S.Ledger.LegalEntity), Clone(w.S.Tenant, Guid.NewGuid())]);
        _factory.TrackTenant(other.S.Tenant);

        await w.ReverseAsync(available);

        Assert.Empty(RefusalRows(w, available.Tx));
    }
}

/// <summary>
/// The MOD-0175 host whose <see cref="IReversalPrecondition"/> is the PRODUCTION <c>QuarantineReversalPrecondition</c> wrapped by a counter
/// (WP-0175-FU01-DL3-FIX-01 ORTA-3): every <c>CheckAsync</c> call is recorded with the original's id, then passed on unchanged.
/// </summary>
public sealed class GuardCountingQuarantineApiFactory : HookedQuarantineApiFactory
{
    public ConcurrentQueue<Guid> GuardCalls { get; } = new();

    public int CallsFor(string originalTx) => GuardCalls.Count(id => id == Guid.Parse(originalTx));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            var registered = services.Last(d => d.ServiceType == typeof(IReversalPrecondition));
            services.Remove(registered);
            services.Add(new ServiceDescriptor(typeof(IReversalPrecondition),
                sp => new CountingReversalPrecondition((IReversalPrecondition)ActivatorUtilities.CreateInstance(sp, registered.ImplementationType!), GuardCalls),
                registered.Lifetime));
        });
    }

    private sealed class CountingReversalPrecondition(IReversalPrecondition inner, ConcurrentQueue<Guid> calls) : IReversalPrecondition
    {
        public Task<ReversalPreconditionResult> CheckAsync(InventoryTransaction original, CancellationToken cancellationToken)
        {
            calls.Enqueue(original.Id);
            return inner.CheckAsync(original, cancellationToken);
        }

        public Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default) => inner.FlushRefusalAuditAsync(cancellationToken);
    }
}

/// <summary>
/// WP-0175-FU01-DL3-FIX-01 ORTA-3 (pack §8.8) — the seam's order and its single call: the guard is asked AFTER the "already reversed" check
/// and exactly ONCE per REVERSAL request. A second REVERSAL of a source already reversed (kurgu (ii): the first one committed before DL-3) answers
/// ALREADY_REVERSED even though its record is decided — no REVERSAL_QUALITY_RECORD_LOCKED, no refusal row, no guard call.
/// </summary>
public sealed class SourceReversalGuardSeamTests : IClassFixture<GuardCountingQuarantineApiFactory>
{
    private readonly GuardCountingQuarantineApiFactory _factory;

    public SourceReversalGuardSeamTests(GuardCountingQuarantineApiFactory factory) => _factory = factory;

    private static object Release(decimal q) => QuarantineScenario.Line(1, "Release", q);

    private static List<BsonDocument> RefusalRows(ReversalWorld w, string originalTx)
        => w.S.Ledger.Persisted("inventory_audit")
            .Where(a => a.GetValue("Action", BsonNull.Value) == QuarantineReversalRefusalAuditRecord.RefusedAction && a["OriginalTransactionId"].AsString == originalTx).ToList();

    [Fact]
    public async Task ORTA3_the_guard_is_asked_after_the_already_reversed_check_and_exactly_once_per_reversal()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var decided = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var open = await w.ReceiveAsync("3", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        var locked = await w.ReceiveAsync("2", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        foreach (var p in new[] { decided, locked })
        {
            var holdId = w.HoldOfSource(p.Tx)["_id"].AsString;
            Assert.Equal("Applied", Id(await w.S.DecideAndApproveAsync(w.S.DecideAsync(holdId, [Release(p == decided ? 4m : 2m)])), "state"));
        }

        w.DirectReversal(decided.Tx); // kurgu (ii): the first REVERSAL committed before DL-3
        var ledger = w.LedgerCount();

        // a second REVERSAL of the reversed source: ALREADY_REVERSED — the guard is not reached
        var again = await w.ReverseExpectingAsync(decided, 422);
        Assert.Equal("ALREADY_REVERSED", QuarantineScenario.Code(again));
        Assert.Empty(RefusalRows(w, decided.Tx));
        Assert.Equal(0, _factory.CallsFor(decided.Tx));
        Assert.Equal(ledger, w.LedgerCount());

        // one request, one guard call — allowed (201) and refused (422) alike
        await w.ReverseAsync(open);
        Assert.Equal(1, _factory.CallsFor(open.Tx));
        var refusal = await w.ReverseExpectingAsync(locked, 422);
        Assert.Equal("REVERSAL_QUALITY_RECORD_LOCKED", QuarantineScenario.Code(refusal));
        Assert.Equal(1, _factory.CallsFor(locked.Tx));
        Assert.Single(RefusalRows(w, locked.Tx));
    }
}
