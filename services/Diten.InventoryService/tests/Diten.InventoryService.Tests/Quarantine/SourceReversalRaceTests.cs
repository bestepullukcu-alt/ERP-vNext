using System.Text.Json;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-1 — the close against the decision writers (pack §8.6 W1–W8, the lot step) and against itself (AC-12), on the
/// observer-less host with the test decorators (no hook in production code). Each race is staged deterministically with a one-shot
/// <see cref="Pause"/> on a decorated call; the close runs through the production sweep while the writer is held. AC-12, AC-13
/// (a) (a2) (b1) (b2) (c) (d) (f) (g) (g2) — (e) withdraw is in <see cref="SourceReversalWorkflowTests"/>.
/// </summary>
public sealed class SourceReversalRaceTests : IClassFixture<ObserverlessQuarantineApiFactory>
{
    private readonly ObserverlessQuarantineApiFactory _factory;

    public SourceReversalRaceTests(ObserverlessQuarantineApiFactory factory) => _factory = factory;

    private ReversalHooks Hooks => _factory.Hooks;

    private static object Release(decimal q) => QuarantineScenario.Line(1, "Release", q);

    private static async Task<string> PendingAsync(ReversalWorld w, string holdId, decimal quantity)
    {
        var draft = await w.S.DecideAsync(holdId, [Release(quantity)]);
        await w.S.SubmitAsync(Id(draft, "decisionId"));
        return Id(draft, "decisionId");
    }

    private static bool Is<T>(HookCall c, string method) => c.Service == typeof(T) && c.Method == method;

    private static void NoApprovedOrAppliedAfterVoided(ReversalWorld w, string decisionId)
    {
        var kinds = w.EventKinds(decisionId);
        var voided = kinds.IndexOf("Voided");
        if (voided >= 0)
        {
            Assert.DoesNotContain(kinds.Skip(voided), k => k is "Approved" or "Applied");
        }
    }

    private async Task StagedAsync(Pause pause, Func<Task> whileHeld)
    {
        try
        {
            await pause.Reached;
            await whileHeld();
        }
        finally
        {
            pause.Release();
        }
    }

    /// <summary>AC-12: the observer and the sweep at once, both past their first read — one Voided, one NoChange (LostRace); one row of each kind.</summary>
    [Fact]
    public async Task AC12_concurrent_observer_and_sweep_close_once_and_the_loser_is_LostRace()
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var reversal = await w.ReverseAsync(source);
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var version = w.HoldDoc(holdId)["Version"].AsInt32;
        var gate = new CountdownGate(2);
        var reads = 0;
        Hooks.Before = async c =>
        {
            if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.FindHoldBySourceMovementAsync)) && c.Arg<string>(0) == source.Tx
                && Interlocked.Increment(ref reads) <= 2)
            {
                await gate.ArriveAsync();
            }
        };
        try
        {
            var observer = Task.Run(() => w.ObserveAsync(reversal));
            var sweep = Task.Run(() => w.SweepAsync());
            await Task.WhenAll(observer, sweep);
            var outcomes = new[] { (await observer)[0].Data!, Assert.Single((await sweep).Results) }
                .Select(r => (r.Outcome, r.NoChange)).OrderBy(r => r.Outcome).ToList();
            Assert.Equal([(SourceReversalVerdict.Voided, (SourceReversalNoChange?)null), (SourceReversalVerdict.NoChange, SourceReversalNoChange.LostRace)], outcomes);
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Single(w.History(holdId), h => h["Action"].AsString == "SourceReversed");
        Assert.Single(w.ReversalAudit(holdId));
        Assert.Equal(version + 1, w.HoldDoc(holdId)["Version"].AsInt32);
    }

    /// <summary>AC-13 (a) W3: the close commits inside the approval's transaction (after its events read) — Voided wins, nothing approved or posted; reverse order: Applied, then marked.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC13a_close_and_approval_one_of_them_wins_never_both(bool closeInside)
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        await w.ReceiveAsync("4", lot, "QUARANTINE");
        var aId = w.HoldOfSource(a.Tx)["_id"].AsString;
        var decision = await PendingAsync(w, aId, 4m);
        await w.ReverseAsync(a); // B's 4 stay in the bucket
        if (closeInside)
        {
            var pause = new Pause();
            Hooks.After = async c =>
            {
                if (Is<IQuarantineWriteSession>(c, nameof(IQuarantineWriteSession.DecisionEventsAsync)) && c.Arg<Guid>(0) == Guid.Parse(decision) && pause.TryFire())
                    await pause.HoldAsync();
            };
            try
            {
                var approving = Task.Run(() => w.S.OutcomeAsync(decision));
                await StagedAsync(pause, async () => Assert.Equal(1, (await w.SweepAsync()).Voided));
                Assert.Equal("Voided", Id(await approving, "state"));
            }
            finally
            {
                Hooks.Clear();
            }

            Assert.DoesNotContain(w.EventKinds(decision), k => k is "Approved" or "Applied");
            Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString == $"quarantine:{decision}:1");
            Assert.Equal("Voided", w.HoldDoc(aId)["Status"].AsString);
        }
        else
        {
            Assert.Equal("Applied", Id(await w.S.OutcomeAsync(decision), "state"));
            Assert.Equal(1, (await w.SweepAsync()).Conflicts);
            Assert.Equal(("Decided", "Decided"), (w.HoldDoc(aId)["Status"].AsString, w.HoldDoc(aId)["SourceReversalConflict"]["Cause"].AsString));
            Assert.DoesNotContain("Voided", w.EventKinds(decision));
        }

        NoApprovedOrAppliedAfterVoided(w, decision);
    }

    private async Task<(ReversalWorld W, string HoldId, string Decision)> ManualPendingAsync()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        await w.ReceiveAsync("10", lot, "AVAILABLE");
        var key = QuarantineScenario.Key("man");
        var hold = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new
        {
            mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key,
            buckets = new[] { new { itemId = w.S.Ledger.Item, skuId = w.S.Ledger.Sku, skuLevel = "Lsku", warehouseId = w.S.Ledger.Warehouse, locationId = w.S.Ledger.LocationA, quantity = 4m, lotId = lot } }
        }, 201, key)).EnumerateArray().Single();
        var holdId = Id(hold, "holdId");
        var decision = await PendingAsync(w, holdId, 4m);
        w.DirectReversal(w.HoldDoc(holdId)["SourceMovementId"].AsString); // kurgu (ii): the balances are untouched
        return (w, holdId, decision);
    }

    private static bool BeforeApproval(ReversalWorld w, string decision) => !w.EventKinds(decision).Contains("Approved");

    /// <summary>AC-13 (a2) W3: the record's version moves (R1 mark) between the approval's read and its write — the second attempt approves, applies; the mark stays.</summary>
    [Fact]
    public async Task AC13a2_a_version_moved_by_another_writer_is_read_again_and_the_approval_goes_on()
    {
        var (w, holdId, decision) = await ManualPendingAsync();
        var pause = new Pause();
        var attempts = 0;
        Hooks.After = async c =>
        {
            if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.FindHoldAsync)) && c.Arg<Guid>(0) == Guid.Parse(holdId) && pause.TryFire())
                await pause.HoldAsync();
            if (Is<IQuarantineWriteSession>(c, nameof(IQuarantineWriteSession.DecisionEventsAsync)) && c.Arg<Guid>(0) == Guid.Parse(decision)
                && c.Result is IReadOnlyList<QuarantineDecisionEvent> events && events.All(e => e.Kind is not (DecisionEventKind.Approved or DecisionEventKind.Applied)))
                Interlocked.Increment(ref attempts);
        };
        try
        {
            var approving = Task.Run(() => w.S.OutcomeAsync(decision));
            await StagedAsync(pause, async () => Assert.Equal(1, (await w.SweepAsync()).Conflicts)); // R1
            Assert.Equal("Applied", Id(await approving, "state"));
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal(2, attempts);
        Assert.Single(w.EventKinds(decision), k => k == "Approved");
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Decided", "OwnMovementReversed"), (hold["Status"].AsString, hold["SourceReversalConflict"]["Cause"].AsString));
    }

    /// <summary>AC-13 (a2) second run: the version moves on every attempt → three attempts, the conflict goes up, nothing approved.</summary>
    [Fact]
    public async Task AC13a2_three_moved_versions_raise_the_conflict_and_write_no_approval()
    {
        var (w, holdId, decision) = await ManualPendingAsync();
        var bumps = 0;
        Hooks.After = c =>
        {
            if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.FindHoldAsync)) && c.Arg<Guid>(0) == Guid.Parse(holdId) && BeforeApproval(w, decision))
            {
                Interlocked.Increment(ref bumps);
                _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", holdId), new BsonDocument("$inc", new BsonDocument("Version", 1)));
            }

            return Task.CompletedTask;
        };
        try
        {
            var (status, _, _) = await w.S.SendAsync(HttpMethod.Post, $"/api/quarantine/test-approval/decisions/{decision}",
                new { outcome = "Approved", approverUserId = w.S.Approver.ToString("D") }, token: w.S.ApproverToken);
            Assert.Equal(500, status);
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal(3, bumps);
        Assert.DoesNotContain("Approved", w.EventKinds(decision));
        Assert.DoesNotContain("Approved", w.Actions(holdId));
    }

    /// <summary>AC-13 (b1) W2: the close commits while the approval starts (Submitted window, R3) — the hold is not turned DecisionPending; a later approval changes nothing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC13b_the_submit_second_transaction_does_not_reopen_a_voided_hold(bool duringStart)
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var draft = Id(await w.S.DecideAsync(holdId, [Release(4m)]), "decisionId");
        var pause = new Pause();
        var started = 0;
        if (duringStart)
        {
            Hooks.Before = async c =>
            {
                if (Is<IQuarantineApprovalPort>(c, nameof(IQuarantineApprovalPort.StartAsync)) && pause.TryFire()) await pause.HoldAsync();
            };
        }
        else
        {
            Hooks.After = async c =>
            {
                if (Is<IQuarantineApprovalPort>(c, nameof(IQuarantineApprovalPort.StartAsync))) Interlocked.Increment(ref started);
                if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.FindHoldAsync)) && Volatile.Read(ref started) == 1 && pause.TryFire()) await pause.HoldAsync();
            };
        }

        JsonElement submitted;
        try
        {
            var submitting = Task.Run(() => w.S.SubmitAsync(draft));
            await StagedAsync(pause, async () =>
            {
                await w.ReverseAsync(source);
                Assert.Equal(1, (await w.SweepAsync()).Voided); // R3
            });
            submitted = await submitting; // 202
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal("Voided", Id(submitted, "state"));
        Assert.Equal("Voided", w.HoldDoc(holdId)["Status"].AsString);
        Assert.Contains("WorkflowStarted", w.EventKinds(draft));
        Assert.Equal("Voided", Id(await w.S.OutcomeAsync(draft), "state"));
        Assert.DoesNotContain("Approved", w.EventKinds(draft));
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString.StartsWith($"quarantine:{draft}", StringComparison.Ordinal));
    }

    /// <summary>AC-13 (c) W4: the close commits inside the rejection's transaction → Voided, no Rejected; reverse order: Rejected, the hold reopened, then R2.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC13c_close_and_rejection(bool closeInside)
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var decision = await PendingAsync(w, holdId, 4m);
        await w.ReverseAsync(source); // R4 at commit time
        if (closeInside)
        {
            var pause = new Pause();
            Hooks.After = async c =>
            {
                if (Is<IQuarantineWriteSession>(c, nameof(IQuarantineWriteSession.DecisionEventsAsync)) && c.Arg<Guid>(0) == Guid.Parse(decision) && pause.TryFire())
                    await pause.HoldAsync();
            };
            try
            {
                var rejecting = Task.Run(() => w.S.OutcomeAsync(decision, "Rejected"));
                await StagedAsync(pause, async () => Assert.Equal(1, (await w.SweepAsync()).Voided));
                Assert.Equal("Voided", Id(await rejecting, "state"));
            }
            finally
            {
                Hooks.Clear();
            }

            Assert.DoesNotContain("Rejected", w.EventKinds(decision));
            Assert.Contains("Voided", w.EventKinds(decision));
            Assert.DoesNotContain("Rejected", w.Actions(holdId));
        }
        else
        {
            Assert.Equal("Rejected", Id(await w.S.OutcomeAsync(decision, "Rejected"), "state"));
            Assert.Equal("Open", w.HoldDoc(holdId)["Status"].AsString);
            Assert.Equal(1, (await w.SweepAsync()).Voided); // R2
            Assert.DoesNotContain("Voided", w.EventKinds(decision));
        }

        Assert.Equal("Voided", w.HoldDoc(holdId)["Status"].AsString);
    }

    /// <summary>AC-13 (d) W1: the close commits between the submit's record read and its lock → 409 ALREADY_PENDING, no start; the next submit → HOLD_CLOSED.</summary>
    [Fact]
    public async Task AC13d_a_lock_on_a_record_voided_after_the_read_is_refused()
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var draft = Id(await w.S.DecideAsync(holdId, [Release(4m)]), "decisionId");
        var pause = new Pause();
        var starts = 0;
        Hooks.Before = async c =>
        {
            if (Is<IQuarantineApprovalPort>(c, nameof(IQuarantineApprovalPort.StartAsync))) Interlocked.Increment(ref starts);
            if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.InTransactionAsync)) && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var submitting = Task.Run(() => w.S.SendAsync(HttpMethod.Post, $"/api/quarantine/decisions/{draft}/submit", new { idempotencyKey = "sub-race-01" }, "sub-race-01"));
            await StagedAsync(pause, async () =>
            {
                await w.ReverseAsync(source);
                Assert.Equal(1, (await w.SweepAsync()).Voided); // R2
            });
            var (status, body, _) = await submitting;
            Assert.Equal((409, "QUARANTINE_DECISION_ALREADY_PENDING"), (status, QuarantineScenario.Code(body)));
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal(0, starts);
        Assert.DoesNotContain("Submitted", w.EventKinds(draft));
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold["ActiveDecisionId"].IsBsonNull));
        Assert.DoesNotContain("DecisionSubmitted", w.Actions(holdId));
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(await w.S.SubmitAsync(draft, 409)));
    }

    /// <summary>AC-13 (f) W8: the start fails, the close commits after the failure branch's fresh read — no leak, the port's error, two attempts.</summary>
    [Fact]
    public async Task AC13f_a_start_failure_on_a_record_voided_meanwhile_answers_the_start_error()
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var draft = Id(await w.S.DecideAsync(holdId, [Release(4m)]), "decisionId");
        var pause = new Pause();
        var failed = 0;
        var freshReads = 0;
        Hooks.Instead = c =>
        {
            if (!Is<IQuarantineApprovalPort>(c, nameof(IQuarantineApprovalPort.StartAsync))) return null;
            Interlocked.Increment(ref failed);
            return ApprovalStart.Failed("QUARANTINE_WORKFLOW_UNAVAILABLE", 503, "staged start failure");
        };
        Hooks.After = async c =>
        {
            if (Is<IQuarantineRepository>(c, nameof(IQuarantineRepository.FindHoldAsync)) && Volatile.Read(ref failed) == 1)
            {
                Interlocked.Increment(ref freshReads);
                if (pause.TryFire()) await pause.HoldAsync();
            }
        };
        try
        {
            var submitting = Task.Run(() => w.S.SendAsync(HttpMethod.Post, $"/api/quarantine/decisions/{draft}/submit", new { idempotencyKey = "sub-fail-01" }, "sub-fail-01"));
            await StagedAsync(pause, async () =>
            {
                await w.ReverseAsync(source);
                Assert.Equal(1, (await w.SweepAsync()).Voided); // R3
            });
            var (status, body, _) = await submitting;
            Assert.Equal((503, "QUARANTINE_WORKFLOW_UNAVAILABLE"), (status, QuarantineScenario.Code(body)));
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal(2, freshReads);
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold["ActiveDecisionId"].IsBsonNull));
        Assert.Contains("StartFailed", w.EventKinds(draft));
        Assert.Equal("Voided", Id(await w.S.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{draft}", null, 200), "state"));
    }

    // ── the lot step (r5) ───────────────────────────────────────────────────────────────────────────────

    private async Task<(ReversalWorld W, string Lot, string HoldId, string Decision, ReversalWorld.Posted Source)> LotChangingAsync()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z"); // Draft: the Release releases it (CT-Q5) — ChangesLot
        var source = await w.ReceiveAsync("4", lot, "QUALITY_INSPECTION");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var decision = await PendingAsync(w, holdId, 4m);
        await w.ReverseAsync(source); // R4 at commit time
        return (w, lot, holdId, decision, source);
    }

    private bool LotKeyRow(string lot, string decision)
        => _factory.Collection("trace_lot_history").Find(new BsonDocument { ["LotId"] = lot, ["IdempotencyKey"] = ApplyApprovedLotStatusChangeHandler.Key(Guid.Parse(decision)) }).Any();

    /// <summary>AC-13 (g): the close commits before the lot command reads the decision → 409 LOT_APPROVAL_REQUIRED, the lot stays Draft, nothing written.</summary>
    [Fact]
    public async Task AC13g_a_lot_command_after_the_close_changes_nothing()
    {
        var (w, lot, holdId, decision, _) = await LotChangingAsync();
        var pause = new Pause();
        Hooks.Before = async c =>
        {
            if (Is<IApprovedLotChanges>(c, nameof(IApprovedLotChanges.FindApprovedAsync)) && pause.TryFire()) await pause.HoldAsync();
        };
        try
        {
            var approving = Task.Run(() => w.S.OutcomeAsync(decision));
            await StagedAsync(pause, async () => Assert.Equal(1, (await w.SweepAsync()).Voided)); // R5 (no lot key yet)
            Assert.Equal("Voided", Id(await approving, "state"));
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal("Draft", w.S.Lots.LotDocument(lot)["Status"].AsString);
        Assert.False(LotKeyRow(lot, decision));
        Assert.DoesNotContain(w.EventKinds(decision), k => k is "ApplyFailed" or "Applied");
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold.GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull));
        Assert.DoesNotContain(w.Logs("lot_changed_after_void"), l => l.Contains(decision, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-13 (g2) — the accepted limit, both orders (son bakış 02 F-1): (1) the close commits after the lot command read the decision and
    /// before its lot transaction → the line re-read sees Voided; (2) the close read the lot key before the lot step and commits after the
    /// line was refused → W6 sees Voided. Either way the lot changed for a Voided decision, the record is unmarked, the log names it.
    /// </summary>
    [Theory]
    [InlineData("line-reread")]
    [InlineData("w6")]
    public async Task AC13g2_a_lot_changed_for_a_voided_decision_is_logged_by_name(string order)
    {
        var (w, lot, holdId, decision, _) = await LotChangingAsync();
        if (order == "line-reread")
        {
            var pause = new Pause();
            Hooks.After = async c =>
            {
                if (Is<IApprovedLotChanges>(c, nameof(IApprovedLotChanges.FindApprovedAsync)) && c.Result is not null && pause.TryFire()) await pause.HoldAsync();
            };
            try
            {
                var approving = Task.Run(() => w.S.OutcomeAsync(decision));
                await StagedAsync(pause, async () => Assert.Equal(1, (await w.SweepAsync()).Voided)); // R5
                Assert.Equal("Voided", Id(await approving, "state"));
            }
            finally
            {
                Hooks.Clear();
            }
        }
        else
        {
            var beforeLot = new Pause();   // the engine, before its lot step
            var closeRead = new Pause();   // the close, right after it read the (absent) lot key
            var refused = new Pause();     // the engine, after its line was refused, before W6
            Hooks.Before = async c =>
            {
                if (Is<IApprovedLotChanges>(c, nameof(IApprovedLotChanges.FindApprovedAsync)) && beforeLot.TryFire()) await beforeLot.HoldAsync();
            };
            Hooks.After = async c =>
            {
                if (Is<ILotRepository>(c, nameof(ILotRepository.FindStatusChangeByKeyAsync)) && beforeLot.Fired && closeRead.TryFire()) await closeRead.HoldAsync();
                if (Is<IInventoryLedgerRepository>(c, nameof(IInventoryLedgerRepository.PostAsync))
                    && c.Arg<LedgerPosting>(0).Transaction.IdempotencyKey == $"quarantine:{decision}:1" && refused.TryFire())
                    await refused.HoldAsync();
            };
            try
            {
                var approving = Task.Run(() => w.S.OutcomeAsync(decision));
                await beforeLot.Reached;
                var closing = Task.Run(() => w.SweepAsync());
                await closeRead.Reached;             // the close read no lot key
                beforeLot.Release();                 // the lot step commits, the line is refused (bucket empty)
                await refused.Reached;
                closeRead.Release();                 // the close commits R5 (unmarked)
                Assert.Equal(1, (await closing).Voided);
                refused.Release();                   // W6 sees Voided and writes nothing
                Assert.Equal("Voided", Id(await approving, "state"));
            }
            finally
            {
                Hooks.Clear();
                beforeLot.Release();
                closeRead.Release();
                refused.Release();
            }

            Assert.DoesNotContain("ApplyFailed", w.EventKinds(decision));
        }

        Assert.Equal("Released", w.S.Lots.LotDocument(lot)["Status"].AsString); // the limit: the lot changed for a Voided decision
        Assert.True(LotKeyRow(lot, decision));
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold.GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull));
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString == $"quarantine:{decision}:1");
        var log = Assert.Single(w.Logs("quarantine.source_reversal.lot_changed_after_void"), l => l.Contains(decision, StringComparison.Ordinal));
        Assert.Contains(hold["HoldNumber"].AsString, log);
        Assert.Contains(lot, log);
    }
}
