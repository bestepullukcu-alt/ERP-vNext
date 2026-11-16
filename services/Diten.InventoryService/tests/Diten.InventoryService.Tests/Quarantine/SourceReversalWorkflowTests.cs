using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Infrastructure.Eventing;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-1 on the PRODUCTION Workflow port (MOD-0023 faked at the HTTP edge), observer-less (kurgu i): AC-13 (e) withdraw and
/// AC-39 — a line of the decision voided with its record posted after the close is marked by the internal command (R0a), through every
/// entry of an approval: refresh (a1), the completion consumer (a2), read-time reconciliation (a3) and W6 (a4); the failed dispatch and
/// the void feed (b), its limit (c), the repeat (d). AC-20 (d): that command's record is System, its own; the outer command keeps its actor.
/// </summary>
public sealed class SourceReversalWorkflowTests : IClassFixture<ObserverlessWorkflowQuarantineApiFactory>
{
    private readonly ObserverlessWorkflowQuarantineApiFactory _factory;

    public SourceReversalWorkflowTests(ObserverlessWorkflowQuarantineApiFactory factory) => _factory = factory;

    private ReversalHooks Hooks => _factory.Hooks;

    private static bool Is<T>(HookCall c, string method) => c.Service == typeof(T) && c.Method == method;

    private async Task<ReversalWorld> WorldAsync()
    {
        var w = new ReversalWorld(_factory);
        await w.S.SaveSettingsAsync(new { approverCandidates = new[] { "user:" + w.S.Approver.ToString("D") }, reconcileAfterSeconds = 0 });
        var key = QuarantineScenario.Key("tpl");
        await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/settings/workflow-template", new { idempotencyKey = key }, 200, key);
        return w;
    }

    private static async Task<(string Decision, string Instance)> SubmittedAsync(ReversalWorld w, string holdId, params object[] lines)
    {
        var draft = await w.S.DecideAsync(holdId, lines, lines.Length > 1 ? "DEVIATION" : null);
        var submitted = await w.S.SubmitAsync(Id(draft, "decisionId"));
        return (Id(draft, "decisionId"), Id(submitted.GetProperty("workflow"), "instanceId"));
    }

    private static EventTransportMessage Completed(Guid tenant, string decisionId, string instance, Guid by)
        => new(Guid.NewGuid(), QuarantineWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(), null, tenant, "platform",
            DateTimeOffset.UtcNow, JsonSerializer.Serialize(new
            {
                tenantId = tenant, workflowInstanceId = instance, objectType = "quarantine.decision", objectId = decisionId, outcome = "approved",
                completedAt = DateTimeOffset.UtcNow, completedBy = by.ToString("D"), reasonCode = (string?)null
            }));

    // ── AC-13 (e): withdraw (W7) ────────────────────────────────────────────────────────────────────────

    /// <summary>AC-13 (e): the close commits after the task was cancelled and before the withdraw's transaction → 200 Voided, nothing withdrawn; reverse order: Withdrawn, then R2.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC13e_close_and_withdraw(bool closeAfterCancel)
    {
        var w = await WorldAsync();
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var (decision, _) = await SubmittedAsync(w, holdId, QuarantineScenario.Line(1, "Release", 4m));
        await w.ReverseAsync(source); // R4 at commit time
        var key = QuarantineScenario.Key("wd");
        var body = new { reason = "Karar geri cekildi - kaynak geri alindi", idempotencyKey = key };
        if (closeAfterCancel)
        {
            var pause = new Pause();
            Hooks.After = async c =>
            {
                if (Is<IQuarantineWorkflowClient>(c, nameof(IQuarantineWorkflowClient.CancelTaskAsync)) && pause.TryFire()) await pause.HoldAsync();
            };
            try
            {
                var withdrawing = Task.Run(() => w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decision}/withdraw", body, 200, key));
                await pause.Reached;
                Assert.Equal(1, (await w.SweepAsync()).Voided); // R4
                pause.Release();
                Assert.Equal("Voided", Id(await withdrawing, "state"));
            }
            finally
            {
                Hooks.Clear();
            }

            Assert.DoesNotContain("Withdrawn", w.EventKinds(decision));
            Assert.DoesNotContain("Withdrawn", w.Actions(holdId));
        }
        else
        {
            Assert.Equal("Withdrawn", Id(await w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decision}/withdraw", body, 200, key), "state"));
            Assert.Equal("Open", w.HoldDoc(holdId)["Status"].AsString);
            Assert.Equal(1, (await w.SweepAsync()).Voided); // R2
            Assert.DoesNotContain("Voided", w.EventKinds(decision));
        }

        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold["ActiveDecisionId"].IsBsonNull));
    }

    /// <summary>AC-13 (e) separate row: a withdraw after the close → 409 QUARANTINE_DECISION_NOT_PENDING and no task is cancelled.</summary>
    [Fact]
    public async Task AC13e_a_withdraw_after_the_close_is_refused_and_cancels_nothing()
    {
        var w = await WorldAsync();
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var (decision, _) = await SubmittedAsync(w, w.HoldOfSource(source.Tx)["_id"].AsString, QuarantineScenario.Line(1, "Release", 4m));
        await w.ReverseAsync(source);
        await w.SweepAsync();
        var cancels = 0;
        Hooks.Before = c =>
        {
            if (Is<IQuarantineWorkflowClient>(c, nameof(IQuarantineWorkflowClient.CancelTaskAsync))) Interlocked.Increment(ref cancels);
            return Task.CompletedTask;
        };
        try
        {
            var key = QuarantineScenario.Key("wd");
            var refused = await w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decision}/withdraw",
                new { reason = "Karar geri cekildi - kaynak geri alindi", idempotencyKey = key }, 409, key);
            Assert.Equal("QUARANTINE_DECISION_NOT_PENDING", QuarantineScenario.Code(refused));
        }
        finally
        {
            Hooks.Clear();
        }

        Assert.Equal(0, cancels);
    }

    // ── AC-39: a line posted after the close ───────────────────────────────────────────────────────────

    private sealed record LateLine(ReversalWorld W, string HoldA, string HoldB, string Decision, string Instance, string Reversal, Guid ReversalCorrelation, string ReversalNumber);

    /// <summary>
    /// Kurgu (i), shared bucket: intake hold A (4, t1, Release 4 PendingApproval) and intake hold B (4 — or 2 for the two-line W6 case);
    /// t1's REVERSAL posted by U1 (the decider) while A waits (R4); the outcome is approved in MOD-0023 by U2 (the approver).
    /// </summary>
    private async Task<LateLine> LateLineAsync(bool twoLines = false)
    {
        var w = await WorldAsync();
        var lot = await w.S.ReleasedLotAsync();
        var a = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var b = await w.ReceiveAsync(twoLines ? "2" : "4", lot, "QUARANTINE");
        var holdA = w.HoldOfSource(a.Tx)["_id"].AsString;
        var (decision, instance) = twoLines
            ? await SubmittedAsync(w, holdA, QuarantineScenario.Line(1, "Release", 2m), QuarantineScenario.Line(2, "Block", 2m))
            : await SubmittedAsync(w, holdA, QuarantineScenario.Line(1, "Release", 4m));
        var reversal = await w.ReverseAsync(a);
        var doc = _factory.Collection("inventory_transactions").Find(new BsonDocument("_id", reversal)).Single();
        _factory.Workflow.Approve(instance, w.S.Approver);
        return new LateLine(w, holdA, w.HoldOfSource(b.Tx)["_id"].AsString, decision, instance, reversal,
            Guid.Parse(doc["CorrelationId"].AsString), doc["TransactionNumber"].AsString);
    }

    /// <summary>Holds the engine after its line re-read, before line <paramref name="lineNo"/> is posted; runs the close (R5) meanwhile.</summary>
    private Pause BeforeLine(string decision, int lineNo = 1)
    {
        var pause = new Pause();
        Hooks.Before = async c =>
        {
            if (Is<IInventoryLedgerRepository>(c, nameof(IInventoryLedgerRepository.FindByIdempotencyKeyAsync))
                && c.Arg<string>(0) == $"quarantine:{decision}:{lineNo}" && pause.TryFire())
                await pause.HoldAsync();
        };
        return pause;
    }

    private void AssertMarkedByTheCommand(LateLine l, string? outerRequestType)
    {
        var w = l.W;
        var a = w.HoldDoc(l.HoldA);
        Assert.Equal("Voided", a["Status"].AsString);
        Assert.Equal((l.Reversal, l.Decision), (a["Voided"]["ReversalTransactionId"].AsString, a["Voided"]["DecisionId"].AsString));
        Assert.DoesNotContain(w.EventKinds(l.Decision), k => k is "Applied" or "ApplyFailed");
        var mark = a["SourceReversalConflict"];
        Assert.Equal("LinesPosted", mark["Cause"].AsString);
        Assert.Equal((l.Reversal, l.ReversalNumber, w.S.Ledger.User.ToString("D")),
            (mark["ReversalTransactionId"].AsString, mark["ReversalTransactionNumber"].AsString, mark["ReversedByUserId"].AsString));
        var history = w.History(l.HoldA).Where(h => h["Action"].AsString.StartsWith("Source", StringComparison.Ordinal)).OrderBy(h => h["AtUtc"].ToUniversalTime()).ToList();
        Assert.Equal(["SourceReversed", "SourceReversalConflict"], history.Select(h => h["Action"].AsString));
        Assert.All(history, h => Assert.Equal((w.S.Ledger.User.ToString("D"), l.ReversalCorrelation.ToString("D")), (h["ByUserId"].AsString, h["CorrelationId"].AsString)));
        var rows = w.ReversalAudit(l.HoldA);
        Assert.Equal(2, rows.Count); // the close + the mark
        Assert.All(rows, r => SourceReversalCloseTests.AssertSystemRecord(r, "ApplySourceReversalToHoldCommand", "QuarantineHold", l.ReversalCorrelation));
        // the approval's own records (create / submit of the draft were the decider's, before the race)
        var outer = w.Audit(Guid.Parse(l.Decision)).Where(r => r.RequestType is not ("CreateDecisionCommand" or "SubmitDecisionCommand")).ToList();
        if (outerRequestType is null)
        {
            Assert.Empty(outer);
        }
        else
        {
            var row = Assert.Single(outer);
            Assert.Equal((outerRequestType, CentralAuditActorTypes.TenantUser, (Guid?)w.S.Approver), (row.RequestType, row.ActorType, row.ActorId));
        }
    }

    /// <summary>AC-39 (a1)–(a3) + AC-20 (d): refresh, the completion consumer, the read-time reconciliation — the mark is the command's, System, once.</summary>
    [Theory]
    [InlineData("refresh")]
    [InlineData("consumer")]
    [InlineData("read")]
    public async Task AC39a_a_line_posted_after_the_close_is_marked_by_the_command_on_every_entry(string entry)
    {
        var l = await LateLineAsync();
        var bVersion = l.W.HoldDoc(l.HoldB)["Version"].AsInt32;
        var pause = BeforeLine(l.Decision);
        int sentBefore, sentAfter;
        try
        {
            Task run = entry switch
            {
                "refresh" => Task.Run(() => l.W.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{l.Decision}/refresh", new { }, 200, token: l.W.S.ApproverToken)),
                "consumer" => Task.Run(() => _factory.Services.GetRequiredService<QuarantineWorkflowOutcomeConsumer>()
                    .ConsumeAsync(Completed(l.W.S.Tenant, l.Decision, l.Instance, l.W.S.Approver))),
                _ => Task.Run(() => l.W.S.GetHoldAsync(l.HoldA))
            };
            await pause.Reached;
            Assert.Equal(1, (await l.W.SweepAsync()).Voided); // R5: nothing posted yet
            sentBefore = _factory.Dispatches.Sent.Count(s => s.EndsWith(l.Reversal, StringComparison.Ordinal));
            pause.Release();
            await run;
            sentAfter = _factory.Dispatches.Sent.Count(s => s.EndsWith(l.Reversal, StringComparison.Ordinal));
        }
        finally
        {
            Hooks.Clear();
            pause.Release();
        }

        Assert.Single(l.W.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString == $"quarantine:{l.Decision}:1"); // the window's known result
        Assert.Equal(1, sentAfter - sentBefore);
        AssertMarkedByTheCommand(l, entry == "refresh" ? "RefreshDecisionCommand" : null);
        Assert.Equal(bVersion, l.W.HoldDoc(l.HoldB)["Version"].AsInt32);
    }

    /// <summary>
    /// AC-39 (a4) W6: the close read the decision before line 1 (nothing posted → R5) and commits after line 2 was refused — the engine's
    /// failure recorder sees Voided, writes nothing, and the command marks the posted line.
    /// </summary>
    [Fact]
    public async Task AC39a4_W6_a_refused_line_after_a_posted_one_is_marked_by_the_command()
    {
        var l = await LateLineAsync(twoLines: true);
        var closeRead = new Pause();
        var refused = new Pause();
        var engine = BeforeLine(l.Decision);
        Hooks.After = async c =>
        {
            // the close: after it read the decision's line keys (the second key is its last read) — nothing posted then
            if (Is<IInventoryLedgerRepository>(c, nameof(IInventoryLedgerRepository.FindByIdempotencyKeyAsync)) && engine.Fired
                && c.Arg<string>(0) == $"quarantine:{l.Decision}:2" && c.Result is null && closeRead.TryFire())
                await closeRead.HoldAsync();
            if (Is<IInventoryLedgerRepository>(c, nameof(IInventoryLedgerRepository.PostAsync))
                && c.Arg<LedgerPosting>(0).Transaction.IdempotencyKey == $"quarantine:{l.Decision}:2" && refused.TryFire())
                await refused.HoldAsync();
        };
        try
        {
            var refreshing = Task.Run(() => l.W.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{l.Decision}/refresh", new { }, 200, token: l.W.S.ApproverToken));
            await engine.Reached;                    // before line 1
            // the close as the command alone (not a sweep round: its void feed would mark the line itself and hide W6's dispatch)
            var closing = Task.Run(() => l.W.SendHoldAsync(l.Reversal));
            await closeRead.Reached;                 // the close saw no posted line
            engine.Release();                        // line 1 posts, line 2 is refused (the bucket is empty)
            await refused.Reached;
            closeRead.Release();                     // the close commits R5
            Assert.Equal(("R5", SourceReversalVerdict.Voided), ((await closing).Data!.Rule, (await closing).Data!.Outcome));
            refused.Release();                       // W6: Voided → nothing written; the command marks R0a
            await refreshing;
        }
        finally
        {
            Hooks.Clear();
            engine.Release();
            closeRead.Release();
            refused.Release();
        }

        Assert.Single(l.W.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString == $"quarantine:{l.Decision}:1");
        AssertMarkedByTheCommand(l, "RefreshDecisionCommand");
    }

    /// <summary>AC-39 (b) + (d): the internal dispatch fails (its enlist) → Voided, unmarked, logged; the void feed marks it; a repeat changes nothing.</summary>
    [Fact]
    public async Task AC39bd_a_failed_dispatch_is_logged_and_the_void_feed_marks_once()
    {
        var l = await LateLineAsync();
        var pause = BeforeLine(l.Decision);
        try
        {
            var refreshing = Task.Run(() => l.W.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{l.Decision}/refresh", new { }, 200, token: l.W.S.ApproverToken));
            await pause.Reached;
            await l.W.SweepAsync();
            Hooks.Before = c => c.Service == typeof(ICentralAuditOutboxStore) && c.Method == nameof(ICentralAuditOutboxStore.InsertInTransactionAsync)
                                && c.Arg<CentralAuditOutboxRecord>(1).TenantId == l.W.S.Tenant
                                && c.Arg<CentralAuditOutboxRecord>(1).RequestType == "ApplySourceReversalToHoldCommand"
                ? throw new InvalidOperationException("staged enlist failure of the internal dispatch")
                : Task.CompletedTask;
            pause.Release();
            Assert.Equal("Voided", Id(await refreshing, "state")); // the entry's answer is unchanged
        }
        finally
        {
            Hooks.Clear();
            pause.Release();
        }

        var a = l.W.HoldDoc(l.HoldA);
        Assert.Equal(("Voided", true), (a["Status"].AsString, a.GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull));
        Assert.Contains(l.W.Logs("quarantine.source_reversal.mark_dispatch_failed"), x => x.Contains(a["HoldNumber"].AsString) && x.Contains(l.Decision));

        var round = await l.W.SweepAsync(); // the void feed
        Assert.Equal(1, round.Conflicts);
        AssertMarkedByTheCommand(l, "RefreshDecisionCommand");
        Assert.Equal(0, (await l.W.SweepAsync()).Changes);
        Assert.Equal(SourceReversalNoChange.AlreadyRecorded, (await l.W.SendHoldAsync(l.Reversal)).Data!.NoChange);
        AssertMarkedByTheCommand(l, "RefreshDecisionCommand");
    }

    /// <summary>AC-39 (c): the void feed reads a lineless voided record and writes nothing; a record voided longer ago than the watch is not read (accepted limit).</summary>
    [Fact]
    public async Task AC39c_the_void_feed_writes_nothing_without_a_posted_line_and_stops_after_the_watch()
    {
        var l = await LateLineAsync();
        // a lineless voided record (AC-3 (a) shape): another hold of the tenant, voided while PendingApproval
        var source = await l.W.ReceiveAsync("3", await l.W.S.ReleasedLotAsync(), "QUARANTINE", location: l.W.S.Ledger.LocationB);
        var lineless = l.W.HoldOfSource(source.Tx)["_id"].AsString;
        await SubmittedAsync(l.W, lineless, QuarantineScenario.Line(1, "Release", 3m));
        await l.W.ReverseAsync(source);

        // A's line posted after the close, with the internal dispatch failing (as in (b)) — then A's void is moved past the watch
        var pause = BeforeLine(l.Decision);
        try
        {
            var refreshing = Task.Run(() => l.W.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{l.Decision}/refresh", new { }, 200, token: l.W.S.ApproverToken));
            await pause.Reached;
            await l.W.SweepAsync(); // voids A (R5) and the lineless hold (R4)
            Hooks.Before = c => c.Service == typeof(ICentralAuditOutboxStore) && c.Method == nameof(ICentralAuditOutboxStore.InsertInTransactionAsync)
                                && c.Arg<CentralAuditOutboxRecord>(1).TenantId == l.W.S.Tenant
                                && c.Arg<CentralAuditOutboxRecord>(1).RequestType == "ApplySourceReversalToHoldCommand"
                ? throw new InvalidOperationException("staged enlist failure")
                : Task.CompletedTask;
            pause.Release();
            await refreshing;
        }
        finally
        {
            Hooks.Clear();
            pause.Release();
        }

        var watch = new global::Diten.InventoryService.Persistence.Quarantine.QuarantineSweeperOptions().SourceReversalVoidedWatchHours;
        _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", l.HoldA),
            new BsonDocument("$set", new BsonDocument("Voided.AtUtc", DateTime.UtcNow.AddHours(-(watch + 1)))));
        var linelessBefore = l.W.HoldDoc(lineless).ToJson();

        var round = await l.W.SweepAsync();

        Assert.Equal(0, round.Changes);
        Assert.Equal(linelessBefore, l.W.HoldDoc(lineless).ToJson());
        Assert.True(l.W.HoldDoc(l.HoldA).GetValue("SourceReversalConflict", BsonNull.Value).IsBsonNull); // not read: the accepted limit
    }
}
