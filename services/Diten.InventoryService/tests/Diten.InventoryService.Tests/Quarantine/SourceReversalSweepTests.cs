using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Persistence.Quarantine;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-1 on the OBSERVER-LESS host (pack §16 kurgu (i): the REVERSAL commits while the record is still closable — what DL-3
/// allows — and the record moves on after it; kurgu (ii): a REVERSAL written straight into the ledger, committed before DL-3). The close
/// runs through the production sweeper (<c>SweepAsync</c>) or the production observer called by the test. AC-2 (b), AC-4, AC-6, AC-7,
/// AC-10 (b), AC-11 (b), AC-15, AC-16 (c), AC-18, AC-20 (b), AC-21, AC-37, AC-40 (c), AC-41.
/// <para>Son bakış 02 F-2 (CT): every lot of these worlds is Released with no retest date change unless the criterion is about the lot
/// step (AC-41) — a Draft lot would turn a Release into a lot-changing decision (CreateLotHandler.cs:166, DecisionHandlers.cs:189).</para>
/// </summary>
public sealed class SourceReversalSweepTests : IClassFixture<ObserverlessQuarantineApiFactory>
{
    private readonly ObserverlessQuarantineApiFactory _factory;

    public SourceReversalSweepTests(ObserverlessQuarantineApiFactory factory) => _factory = factory;

    private static object Release(decimal q) => QuarantineScenario.Line(1, "Release", q);

    private static async Task<string> PendingAsync(ReversalWorld w, string holdId, params object[] lines)
    {
        var draft = await w.S.DecideAsync(holdId, lines, lines.Length > 1 ? "DEVIATION" : null);
        await w.S.SubmitAsync(Id(draft, "decisionId"));
        return Id(draft, "decisionId");
    }

    private static string HoldIdOf(ReversalWorld w, ReversalWorld.Posted p) => w.HoldOfSource(p.Tx)["_id"].AsString;

    /// <summary>AC-2 (b): an unmatched combination is written nothing — record, version, events, history, outbox — logged; the round goes on.</summary>
    [Fact]
    public async Task AC02b_an_unmatched_combination_writes_nothing_logs_and_the_round_goes_on()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var odd = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var oddId = HoldIdOf(w, odd);
        var draft = await w.S.DecideAsync(oddId, [Release(4m)]);
        var normal = await w.ReceiveAsync("3", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        await w.ReverseAsync(odd);      // R2 at commit time: DL-3 would allow it
        await w.ReverseAsync(normal);
        _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", oddId), new BsonDocument("$set",
            new BsonDocument { ["Status"] = "DecisionPending", ["ActiveDecisionId"] = Id(draft, "decisionId") }));
        var before = w.HoldDoc(oddId).ToJson();
        var history = w.History(oddId).Count;

        var round = await w.SweepAsync();

        Assert.Equal(before, w.HoldDoc(oddId).ToJson());
        Assert.Empty(w.EventKinds(Id(draft, "decisionId")));
        Assert.Equal(history, w.History(oddId).Count);
        Assert.Empty(w.ReversalAudit(oddId));
        var number = w.HoldDoc(oddId)["HoldNumber"].AsString;
        Assert.Contains(w.Logs("quarantine.source_reversal.unmatched"), l => l.Contains(number, StringComparison.Ordinal));
        Assert.Contains(round.Results, r => r.RecordNumber == number && r.NoChange == SourceReversalNoChange.Unmatched && r.Rule == "R13");
        Assert.Equal(0, round.Failed);
        Assert.Equal("Voided", w.HoldOfSource(normal.Tx)["Status"].AsString); // the second record still closes
    }

    /// <summary>AC-4 (+ son bakış 02 F-2: Released lots): X (Approved, stopped before its line) and Y (lineless ApplyFailed) both close R5.</summary>
    [Fact]
    public async Task AC04_approved_and_lineless_failed_decisions_are_voided_and_nothing_is_posted()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var y = await w.ReceiveAsync("4", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        var xDecision = await PendingAsync(w, HoldIdOf(w, x), Release(4m));
        var yDecision = await PendingAsync(w, HoldIdOf(w, y), Release(4m));
        await w.ReverseAsync(x);
        await w.ReverseAsync(y);
        var ledger = w.LedgerCount();

        var pause = new Pause();
        _factory.Hooks.After = async c =>
        {
            if (IsApprovedRead(c, xDecision) && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approvingX = Task.Run(() => w.S.OutcomeAsync(xDecision));
            await pause.Reached;
            var y1 = await w.S.OutcomeAsync(yDecision);
            Assert.Equal("ApplyFailed", Id(y1, "state")); // INSUFFICIENT_STOCK, 0 lines
            var round = await w.SweepAsync();
            Assert.Equal(2, round.Voided);
            Assert.All(new[] { x, y }, p => Assert.Equal("Voided", w.HoldOfSource(p.Tx)["Status"].AsString));
            Assert.Equal("Voided", EventsState(w, xDecision));
            Assert.Equal("Voided", EventsState(w, yDecision));
            Assert.Equal("QUARANTINE_DECISION_NOT_APPLICABLE", QuarantineScenario.Code(await w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{yDecision}/apply", new { }, 409)));
            pause.Release();
            await approvingX;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }

        Assert.Equal(ledger, w.LedgerCount()); // only the two REVERSALs, before
        Assert.DoesNotContain("Applied", w.EventKinds(xDecision));
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString.StartsWith($"quarantine:{xDecision}", StringComparison.Ordinal));
        Assert.Equal(["SourceReversed"], w.Actions(HoldIdOf(w, y)).Where(a => a.StartsWith("Source", StringComparison.Ordinal)));
    }

    /// <summary>The ApplyAsync state check (the first events read of the decision that sees Approved).</summary>
    internal static bool IsApprovedRead(HookCall c, string decisionId)
        => c.Service == typeof(IQuarantineDecisionRepository) && c.Method == nameof(IQuarantineDecisionRepository.EventsAsync)
           && c.Arg<IReadOnlyCollection<Guid>>(0).Contains(Guid.Parse(decisionId))
           && c.Result is IReadOnlyList<global::Diten.InventoryService.Domain.Quarantine.Entities.QuarantineDecisionEvent> events
           && events.Any(e => e.Kind == global::Diten.InventoryService.Domain.Quarantine.Enums.DecisionEventKind.Approved);

    internal static string EventsState(ReversalWorld w, string decisionId)
        => w.EventKinds(decisionId).Contains("Voided") ? "Voided" : w.EventKinds(decisionId).LastOrDefault() ?? "Draft";

    /// <summary>AC-6 (+ AC-11 (b), AC-40 (c)): a decided hold of a shared bucket is marked Decided by the terminal feed; the other hold is untouched.</summary>
    [Fact]
    public async Task AC06_AC11b_AC40c_a_decided_hold_is_marked_by_the_terminal_feed_once_and_the_other_hold_is_untouched()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var b = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var reversal = await w.ReverseAsync(a); // B's 4 stay in the bucket
        var aId = HoldIdOf(w, a);
        var applied = await w.S.DecideAndApproveAsync(w.S.DecideAsync(aId, [Release(4m)]));
        Assert.Equal("Applied", Id(applied, "state"));
        var bBefore = w.HoldOfSource(b.Tx).ToJson();

        var round = await w.SweepAsync();

        Assert.Equal(1, round.Conflicts);
        var held = w.HoldDoc(aId);
        Assert.Equal("Decided", held["Status"].AsString);
        Assert.Equal("Decided", held["SourceReversalConflict"]["Cause"].AsString);
        Assert.Single(w.History(aId), h => h["Action"].AsString == "SourceReversalConflict");
        Assert.Equal(bBefore, w.HoldOfSource(b.Tx).ToJson());
        Assert.Single(w.ReversalAudit(aId));

        // AC-11 (b): exactly once — two more rounds change nothing, the observer answers AlreadyRecorded
        Assert.Equal(0, (await w.SweepAsync()).Changes);
        Assert.Equal(0, (await w.SweepAsync()).Changes);
        var again = (await w.ObserveAsync(reversal))[0];
        Assert.Equal((SourceReversalVerdict.NoChange, SourceReversalNoChange.AlreadyRecorded), (again.Data!.Outcome, again.Data.NoChange));
        Assert.Single(w.History(aId), h => h["Action"].AsString == "SourceReversalConflict");
        Assert.Single(w.ReversalAudit(aId));

        // AC-40 (c): the decided record's detail lists the REVERSAL of its mark
        var detail = await w.S.GetHoldAsync(aId);
        Assert.Equal(reversal, Id(detail.GetProperty("sourceReversalConflict"), "reversalTransactionId"));
        Assert.Contains(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "transactionId") == reversal && Id(m, "movementType") == "REVERSAL");
    }

    /// <summary>AC-7: a two-line decision with line 1 posted — (a) stopped before line 2, (b) line 2 refused — is marked LinesPosted (R6), never voided.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC07_a_decision_with_a_posted_line_is_marked_LinesPosted(bool stopBeforeLine2)
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        await w.ReceiveAsync("2", lot, "QUARANTINE");
        var aId = HoldIdOf(w, a);
        var decision = await PendingAsync(w, aId, QuarantineScenario.Line(1, "Release", 2m), QuarantineScenario.Line(2, "Block", 2m));
        await w.ReverseAsync(a); // 2 stay in the bucket

        var pause = new Pause();
        if (stopBeforeLine2)
        {
            _factory.Hooks.Before = async c =>
            {
                if (c.Service == typeof(global::Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository) && c.Method == "FindByIdempotencyKeyAsync"
                    && c.Arg<string>(0) == $"quarantine:{decision}:2" && pause.TryFire()) await pause.HoldAsync();
            };
        }

        try
        {
            var approving = Task.Run(() => w.S.OutcomeAsync(decision));
            if (stopBeforeLine2) await pause.Reached;
            else Assert.Equal("ApplyFailed", Id(await approving, "state"));

            var round = await w.SweepAsync();
            var held = w.HoldDoc(aId);
            Assert.Equal(1, round.Conflicts);
            Assert.Equal("DecisionPending", held["Status"].AsString);
            Assert.Equal("LinesPosted", held["SourceReversalConflict"]["Cause"].AsString);
            Assert.DoesNotContain("Voided", w.EventKinds(decision));
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }

        Assert.DoesNotContain("Voided", w.EventKinds(decision));
        Assert.Equal("DecisionPending", w.HoldDoc(aId)["Status"].AsString);
    }

    /// <summary>AC-8: the engine posts no line of a decision voided between its state check and its first line (the line re-read).</summary>
    [Fact]
    public async Task AC08_the_engine_posts_no_line_of_a_decision_voided_after_its_state_check()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var b = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var aId = HoldIdOf(w, a);
        var decision = await PendingAsync(w, aId, Release(4m));
        await w.ReverseAsync(a); // B's 4 stay

        var pause = new Pause();
        _factory.Hooks.After = async c =>
        {
            if (IsApprovedRead(c, decision) && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = Task.Run(() => w.S.OutcomeAsync(decision));
            await pause.Reached;
            Assert.Equal(1, (await w.SweepAsync()).Voided); // R5
            pause.Release();
            await approving;
        }
        finally
        {
            _factory.Hooks.Clear();
            pause.Release();
        }

        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString == $"quarantine:{decision}:1");
        Assert.DoesNotContain("Applied", w.EventKinds(decision));
        Assert.Equal("Voided", w.HoldDoc(aId)["Status"].AsString);
        Assert.Equal(4m, await w.S.OnHandAsync("QUARANTINE")); // B's stock is where it was
        Assert.Equal("Open", w.HoldOfSource(b.Tx)["Status"].AsString);
    }

    /// <summary>AC-10 (b): an intake blocked record that lost quantity (1 of 3 destroyed) is marked QuantityChanged (R9); Y is untouched.</summary>
    [Fact]
    public async Task AC10b_a_blocked_record_with_a_partial_disposition_is_marked_QuantityChanged()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var x = await w.ReceiveAsync("3", lot, "BLOCKED");
        var y = await w.ReceiveAsync("3", lot, "BLOCKED");
        var xId = w.BlockedOfSource(x.Tx)["_id"].AsString;
        await w.ReverseAsync(x); // R8 at commit time; Y's 3 stay
        var destroy = await w.S.DisposeAsync(xId, new { kind = "Destroy", quantity = 1m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        await w.S.SubmitAsync(Id(destroy, "decisionId"));
        Assert.Equal("Applied", Id(await w.S.OutcomeAsync(Id(destroy, "decisionId")), "state"));
        Assert.Equal(2m, w.BlockedDoc(xId)["Quantity"].ToDecimal());
        var yBefore = w.BlockedOfSource(y.Tx).ToJson();

        await w.SweepAsync();

        var marked = w.BlockedDoc(xId);
        Assert.Equal("Open", marked["Status"].AsString);
        Assert.Equal("QuantityChanged", marked["SourceReversalConflict"]["Cause"].AsString);
        Assert.Equal(yBefore, w.BlockedOfSource(y.Tx).ToJson());
    }

    /// <summary>AC-15 (a) (b): old data — a REVERSAL 30 days old closes an open hold on the first round; a decided hold of a 30-day-old direct REVERSAL is marked.</summary>
    [Fact]
    public async Task AC15ab_old_reversals_are_found_by_record_not_by_a_time_window()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var open = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var reversal = await w.ReverseAsync(open);
        w.Age("inventory_transactions", reversal, TimeSpan.FromDays(30));
        w.Age("inventory_transactions", open.Tx, TimeSpan.FromDays(30));
        var decided = await w.ReceiveAsync("5", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        var decidedId = HoldIdOf(w, decided);
        await w.S.DecideAndApproveAsync(w.S.DecideAsync(decidedId, [Release(5m)]));
        w.DirectReversal(decided.Tx, TimeSpan.FromDays(30)); // kurgu (ii)

        var round = await w.SweepAsync();

        Assert.Equal("Voided", w.HoldOfSource(open.Tx)["Status"].AsString);
        Assert.Equal("Decided", w.HoldDoc(decidedId)["SourceReversalConflict"]["Cause"].AsString);
        Assert.Equal((1, 1), (round.Voided, round.Conflicts));
    }

    /// <summary>AC-15 (c): page size 2, five open holds, the LAST one by id reversed → rounds 1–2 change nothing, round 3 closes it, round 4 starts over.</summary>
    [Fact]
    public async Task AC15c_the_cursor_moves_page_by_page_and_starts_over_after_a_short_page()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var posted = new List<ReversalWorld.Posted>();
        for (var i = 0; i < 5; i++) posted.Add(await w.ReceiveAsync("1", lot, "QUARANTINE"));
        var last = posted.OrderBy(p => HoldIdOf(w, p), StringComparer.Ordinal).Last();
        await w.ReverseAsync(last);
        var sweeper = w.Sweeper(o => o.SourceReversalPageSize = 2);

        var changes = new List<int>();
        for (var round = 1; round <= 4; round++) changes.Add((await w.SweepAsync(sweeper)).Changes);

        Assert.Equal([0, 0, 1, 0], changes);
        Assert.Equal("Voided", w.HoldOfSource(last.Tx)["Status"].AsString);
        Assert.Equal(500, new QuarantineSweeperOptions().SourceReversalPageSize);
        Assert.Equal(500, _factory.Services.GetRequiredService<IOptions<QuarantineSweeperOptions>>().Value.SourceReversalPageSize);
    }

    /// <summary>AC-15 (d): the ledger query of a page runs on ux_tenant_le_reversestransactionid (IXSCAN), never a collection scan.</summary>
    [Fact]
    public async Task AC15d_the_reversal_query_uses_the_tenant_company_reversal_index()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        await w.ReverseAsync(source);
        var filter = QuarantineReversalReads.ReversalsOf(w.S.Tenant, w.S.Ledger.LegalEntity, [Guid.Parse(source.Tx)], DateTimeOffset.UtcNow.AddMinutes(5));
        var rendered = filter.Render(BsonSerializer.SerializerRegistry.GetSerializer<InventoryTransaction>(), BsonSerializer.SerializerRegistry);
        var explain = await _factory.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument { ["find"] = "inventory_transactions", ["filter"] = rendered },
            ["verbosity"] = "queryPlanner"
        });
        var plan = explain["queryPlanner"]["winningPlan"].ToJson();
        Assert.Contains("IXSCAN", plan);
        Assert.Contains("ux_tenant_le_reversestransactionid", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
    }

    /// <summary>AC-16 (c) + AC-20 (b): the sweeper works in the record's own tenant + company; its record is System with the REVERSAL's correlation.</summary>
    [Fact]
    public async Task AC16c_AC20b_the_sweep_closes_in_the_record_scope_as_the_system_actor_with_the_reversal_correlation()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var reversal = await w.ReverseAsync(source);
        var correlation = Guid.Parse(_factory.Collection("inventory_transactions").Find(new BsonDocument("_id", reversal)).Single()["CorrelationId"].AsString);
        var holdId = HoldIdOf(w, source);

        await w.SweepAsync();

        var row = Assert.Single(w.ReversalAudit(holdId));
        SourceReversalCloseTests.AssertSystemRecord(row, "ApplySourceReversalToHoldCommand", "QuarantineHold", correlation);
        Assert.Equal((w.S.Tenant, (Guid?)w.S.Ledger.LegalEntity, (Guid?)Guid.Parse(holdId)), (row.TenantId, row.CompanyId, row.EntityId));
        Assert.Equal("Voided", w.HoldDoc(holdId)["Status"].AsString);
    }

    /// <summary>AC-21: the central record cannot be written → the close rolls back (no status, history, event); the next round closes.</summary>
    [Fact]
    public async Task AC21_a_central_record_that_cannot_be_enlisted_rolls_the_close_back()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var holdId = HoldIdOf(w, source);
        var decision = await PendingAsync(w, holdId, Release(4m));
        await w.ReverseAsync(source);
        _factory.Hooks.Before = c => c.Service == typeof(ICentralAuditOutboxStore) && c.Method == nameof(ICentralAuditOutboxStore.InsertInTransactionAsync)
                                     && c.Arg<CentralAuditOutboxRecord>(1).TenantId == w.S.Tenant
                                     && c.Arg<CentralAuditOutboxRecord>(1).RequestType == "ApplySourceReversalToHoldCommand"
            ? throw new InvalidOperationException("staged K2 enlist failure")
            : Task.CompletedTask;
        try
        {
            var round = await w.SweepAsync();
            Assert.Equal(1, round.Failed);
        }
        finally
        {
            _factory.Hooks.Clear();
        }

        Assert.Equal("DecisionPending", w.HoldDoc(holdId)["Status"].AsString);
        Assert.DoesNotContain("Voided", w.EventKinds(decision));
        Assert.DoesNotContain("SourceReversed", w.Actions(holdId));
        Assert.Empty(w.ReversalAudit(holdId));

        Assert.Equal(1, (await w.SweepAsync()).Voided);
        Assert.Equal("Voided", w.HoldDoc(holdId)["Status"].AsString);
        Assert.Single(w.ReversalAudit(holdId));
    }

    // ── AC-37: MOD-0175 origin ─────────────────────────────────────────────────────────────────────────────

    private static object Bucket(ReversalWorld w, string lot, decimal quantity, string? location = null)
        => new { itemId = w.S.Ledger.Item, skuId = w.S.Ledger.Sku, skuLevel = "Lsku", warehouseId = w.S.Ledger.Warehouse, locationId = location ?? w.S.Ledger.LocationA, quantity, lotId = lot };

    private static readonly string FutureWire = LotLedgerScenario.Midnight(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(400));

    /// <summary>The five MOD-0175 records of AC-37, each with the id of its source movement: (kind, record id, source tx, isBlocked, active decision).</summary>
    private async Task<List<(string Kind, string RecordId, string Source, bool Blocked, string? Decision)>> OwnRecordsAsync(ReversalWorld w)
    {
        var records = new List<(string, string, string, bool, string?)>();
        var lot = await w.S.ReleasedLotAsync();
        await w.ReceiveAsync("100", lot, "AVAILABLE");
        await w.ReceiveAsync("10", lot, "AVAILABLE", location: w.S.Ledger.LocationB);

        // Manual (with a PendingApproval decision: no event may be written on it)
        var key = QuarantineScenario.Key("man");
        var manual = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 10m) } }, 201, key))
            .EnumerateArray().Single();
        var manualDecision = await PendingAsync(w, Id(manual, "holdId"), Release(10m));
        records.Add(("Manual", Id(manual, "holdId"), w.HoldDoc(Id(manual, "holdId"))["SourceMovementId"].AsString, false, manualDecision));

        // Retest (a retest-due lot)
        var due = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: FutureWire);
        await w.S.Lots.ReleaseAsync(due);
        await w.S.Lots.PostOkAsync(w.S.Lots.Receipt("6", due, location: w.S.Ledger.LocationB));
        await w.S.Lots.SetRetestDateAsync(due, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2));
        key = QuarantineScenario.Key("rt");
        var retest = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = due, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key))
            .EnumerateArray().Single();
        records.Add(("Retest", Id(retest, "holdId"), w.HoldDoc(Id(retest, "holdId"))["SourceMovementId"].AsString, false, null));

        // Recall (the MOD-0177 in-process port)
        using (var scope = w.Scope())
        {
            var caseId = Guid.NewGuid();
            var answer = await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>().QuarantineAsync(new RecallQuarantineCall(
                [new RecallQuarantineBucket(w.S.Ledger.Item, w.S.Ledger.Sku, w.S.Ledger.Level, w.S.Ledger.Warehouse, w.S.Ledger.LocationA, 5m, lot)],
                "Geri çağırma RC-2026-0001 · Tedarikci sapma bildirimi", $"recall:{caseId:D}:r1:Stock:{lot}", caseId, "RC-2026-0001"));
            var recall = w.S.Holds().Single(h => h["Source"].AsString == "Recall");
            records.Add(("Recall", recall["_id"].AsString, recall["SourceMovementId"].AsString, false, null));
            Assert.NotNull(answer);
        }

        // ManualBlock + Rework (a rework disposition of a manual block opens a Rework hold; the block is Closed)
        key = QuarantineScenario.Key("blk");
        var manualBlock = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 8m) } }, 201, key)).EnumerateArray().Single();
        records.Add(("ManualBlock", Id(manualBlock, "blockedId"), w.BlockedDoc(Id(manualBlock, "blockedId"))["SourceMovementId"].AsString, true, null));
        key = QuarantineScenario.Key("blk2");
        var toRework = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(w, lot, 3m, w.S.Ledger.LocationB) } }, 201, key)).EnumerateArray().Single();
        var rework = await w.S.DisposeAsync(Id(toRework, "blockedId"), new { kind = "Rework", quantity = 3m });
        await w.S.SubmitAsync(Id(rework, "decisionId"));
        Assert.Equal("Applied", Id(await w.S.OutcomeAsync(Id(rework, "decisionId")), "state"));
        var reworkHold = w.S.Holds().Single(h => h["Source"].AsString == "Rework");
        Assert.Equal("Closed", w.BlockedDoc(Id(toRework, "blockedId"))["Status"].AsString);
        records.Add(("Rework", reworkHold["_id"].AsString, reworkHold["SourceMovementId"].AsString, false, null));
        return records;
    }

    /// <summary>AC-37: a record of a MOD-0175 movement is never voided — through the observer and through the sweep (kurgu ii).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC37_a_record_of_a_MOD_0175_movement_is_only_marked_OwnMovementReversed(bool observer)
    {
        var w = new ReversalWorld(_factory);
        var records = await OwnRecordsAsync(w);
        var before = records.ToDictionary(r => r.RecordId, r => r.Blocked ? w.BlockedDoc(r.RecordId) : w.HoldDoc(r.RecordId));
        var reversals = records.ToDictionary(r => r.RecordId, r => w.DirectReversal(r.Source));

        if (observer)
        {
            foreach (var reversal in reversals.Values) await w.ObserveAsync(reversal);
        }
        else
        {
            await w.SweepAsync();
        }

        foreach (var (kind, recordId, _, blocked, decision) in records)
        {
            var doc = blocked ? w.BlockedDoc(recordId) : w.HoldDoc(recordId);
            Assert.True(doc["Status"] == before[recordId]["Status"], kind);
            Assert.Equal(before[recordId].GetValue("ActiveDecisionId", BsonNull.Value), doc.GetValue("ActiveDecisionId", BsonNull.Value));
            Assert.Equal(before[recordId]["Quantity"], doc["Quantity"]);
            Assert.True(doc.GetValue("Voided", BsonNull.Value).IsBsonNull, kind);
            Assert.Equal("OwnMovementReversed", doc["SourceReversalConflict"]["Cause"].AsString);
            Assert.Single(w.History(recordId), h => h["Action"].AsString == "SourceReversalConflict");
            var row = Assert.Single(w.ReversalAudit(recordId));
            Assert.Equal(("LifecycleTransition", CentralAuditActorTypes.System), (row.Operation, row.ActorType));
            if (decision is not null) Assert.Equal(["Submitted", "WorkflowStarted"], w.EventKinds(decision));
        }
    }

    /// <summary>AC-37 cross-check: a hold whose field says CustomerReturn but whose source movement is MOD-0175's is marked, not voided, and logged.</summary>
    [Fact]
    public async Task AC37_the_source_module_cross_check_fails_closed()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var intake = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var applied = await w.S.DecideAndApproveAsync(w.S.DecideAsync(HoldIdOf(w, intake), [Release(4m)]));
        var line = Assert.Single(await w.S.LedgerMovementsAsync(Id(applied, "decisionNumber"))).GetProperty("transactionId").GetString()!;
        var forged = w.HoldOfSource(intake.Tx).DeepClone().AsBsonDocument;
        forged["_id"] = Guid.NewGuid().ToString("D");
        forged["HoldNumber"] = "KRN-2026-" + Random.Shared.Next(100000, 999999);
        forged["Source"] = "CustomerReturn";
        forged["SourceMovementId"] = line;
        forged["Status"] = "Open";
        forged["ActiveDecisionId"] = BsonNull.Value;
        _factory.Collection("quarantine_holds").InsertOne(forged);
        w.DirectReversal(line);

        await w.SweepAsync();

        var doc = w.HoldDoc(forged["_id"].AsString);
        Assert.Equal("Open", doc["Status"].AsString);
        Assert.Equal("OwnMovementReversed", doc["SourceReversalConflict"]["Cause"].AsString);
        Assert.Contains(w.Logs("quarantine.source_reversal.origin_fail_closed"), l => l.Contains(forged["HoldNumber"].AsString, StringComparison.Ordinal));
    }

    // ── AC-41: the lot step applied ─────────────────────────────────────────────────────────────────────

    /// <summary>AC-41 (a): a lineless ApplyFailed whose lot step ran (Draft → Released, keyed row) is voided AND marked LotChanged in one transaction.</summary>
    [Fact]
    public async Task AC41a_a_lineless_failed_decision_whose_lot_changed_is_voided_and_marked_LotChanged()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z"); // Draft: the Release releases the lot (CT-Q5)
        var source = await w.ReceiveAsync("4", lot, "QUALITY_INSPECTION");
        var holdId = HoldIdOf(w, source);
        var decision = await PendingAsync(w, holdId, Release(4m));
        var reversal = await w.ReverseAsync(source); // R4 at commit time
        Assert.Equal("ApplyFailed", Id(await w.S.OutcomeAsync(decision), "state"));
        Assert.Equal("Released", w.S.Lots.LotDocument(lot)["Status"].AsString);

        var round = await w.SweepAsync();

        var doc = w.HoldDoc(holdId);
        Assert.Equal("Voided", doc["Status"].AsString);
        Assert.Equal(decision, doc["Voided"]["DecisionId"].AsString);
        Assert.Equal(("LotChanged", reversal), (doc["SourceReversalConflict"]["Cause"].AsString, doc["SourceReversalConflict"]["ReversalTransactionId"].AsString));
        Assert.Equal("Voided", EventsState(w, decision));
        Assert.Equal(["SourceReversed", "SourceReversalConflict"], w.Actions(holdId).Where(a => a.StartsWith("Source", StringComparison.Ordinal)));
        Assert.Equal(CentralAuditActorTypes.System, Assert.Single(w.ReversalAudit(holdId)).ActorType);
        Assert.Contains(w.Logs("quarantine.source_reversal.lot_changed "), l => l.Contains(doc["HoldNumber"].AsString) && l.Contains(decision) && l.Contains(lot));
        Assert.Equal("Released", w.S.Lots.LotDocument(lot)["Status"].AsString);
        Assert.Equal(1, round.Voided);
        Assert.Equal(0, (await w.SweepAsync()).Changes);
    }

    /// <summary>AC-41 (c): a Release with the same retest date (ChangesLot, but the lot step writes no row) closes R5, unmarked.</summary>
    [Fact]
    public async Task AC41c_a_lot_step_that_changed_nothing_leaves_the_close_unmarked()
    {
        var w = new ReversalWorld(_factory);
        var retest = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(200);
        var lot = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(retest));
        await w.S.Lots.ReleaseAsync(lot);
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var holdId = HoldIdOf(w, source);
        var key = QuarantineScenario.Key("dec");
        var draft = await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", new
        {
            kind = "HoldDecision", targetId = holdId, lines = new[] { Release(4m) }, reason = QuarantineScenario.Reason, coaChecked = true,
            newRetestDate = retest.ToString("yyyy-MM-dd"), idempotencyKey = key
        }, 201, key);
        var decision = Id(draft, "decisionId");
        await w.S.SubmitAsync(decision);
        await w.ReverseAsync(source);
        Assert.Equal("ApplyFailed", Id(await w.S.OutcomeAsync(decision), "state"));

        await w.SweepAsync();

        var doc = w.HoldDoc(holdId);
        Assert.Equal("Voided", doc["Status"].AsString);
        Assert.True(doc.GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull);
    }

    // ── AC-18: the open predicate ───────────────────────────────────────────────────────────────────────

    /// <summary>AC-18: a voided hold is not counted — KPI, late filter, lot quality status, recall open list, EnteredBefore (each measured).</summary>
    [Fact]
    public async Task AC18_a_voided_hold_is_not_counted_as_open_anywhere()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var voided = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var open = await w.ReceiveAsync("3", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        await w.ReverseAsync(voided);
        await w.SweepAsync();
        var voidedId = HoldIdOf(w, voided);
        var openId = HoldIdOf(w, open);
        Assert.Equal("Voided", w.HoldDoc(voidedId)["Status"].AsString);
        foreach (var id in new[] { voidedId, openId }) w.Age("quarantine_holds", id, TimeSpan.FromDays(40));
        _factory.Collection("quarantine_holds").UpdateMany(new BsonDocument("_id", new BsonDocument("$in", new BsonArray { voidedId, openId })),
            new BsonDocument("$set", new BsonDocument("EnteredAtUtc", DateTime.UtcNow.AddDays(-40))));

        // KPI cards
        var list = await w.S.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds", null, 200);
        Assert.Equal(1, list.GetProperty("kpis").GetProperty("openCount").GetInt32());
        Assert.Equal(3m, QuarantineScenario.D(list.GetProperty("kpis").GetProperty("quantityByUom")[0].GetProperty("quantity")));
        Assert.Equal(1, list.GetProperty("kpis").GetProperty("lateCount").GetInt32());
        // late=true
        var late = await w.S.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds?lateOnly=true", null, 200);
        Assert.Equal([openId], late.GetProperty("items").EnumerateArray().Select(h => Id(h, "holdId")));
        // the voided row is listed by default, sorted with the closed ones, not late
        var row = list.GetProperty("items").EnumerateArray().Single(h => Id(h, "holdId") == voidedId);
        Assert.False(row.GetProperty("late").GetBoolean());
        Assert.Equal(openId, Id(list.GetProperty("items")[0], "holdId"));
        // lot quality status
        var quality = await w.S.ExpectAsync(HttpMethod.Get, $"/api/quarantine/lots/{lot}/quality-status", null, 200);
        Assert.DoesNotContain(voidedId, quality.ToString());
        Assert.Contains(openId, quality.ToString());
        // the MOD-0177 in-process recall requester's open holds; the repository's EnteredBefore filter
        using var scope = w.Scope();
        var openOfLot = await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>().OpenHoldsOfLotAsync(lot);
        Assert.DoesNotContain(openOfLot, h => h.HoldId == voidedId);
        Assert.Contains(openOfLot, h => h.HoldId == openId);
        var entered = await scope.ServiceProvider.GetRequiredService<IQuarantineRepository>()
            .ListHoldsAsync(new HoldQuery(null, null, null, null, null, null, null, DateTimeOffset.UtcNow, 1, 100));
        Assert.Equal([openId], entered.Items.Select(h => h.Id.ToString("D")));
    }
}
