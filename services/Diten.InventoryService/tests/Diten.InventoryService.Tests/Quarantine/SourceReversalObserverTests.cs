using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Tests.Architecture;
using Diten.InventoryService.Tests.TestInfrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-1 on the PRODUCTION host (observer registered): AC-3 (a voided decision absorbs every outcome, V8), AC-11 (a) (exactly
/// once, both orders), AC-19 for AC-3, AC-20 (c) (the system mode is the dispatch's only, in the same DI scope).
/// </summary>
public sealed class SourceReversalObserverTests : IClassFixture<HookedQuarantineApiFactory>
{
    private readonly HookedQuarantineApiFactory _factory;

    public SourceReversalObserverTests(HookedQuarantineApiFactory factory) => _factory = factory;

    private static async Task<(ReversalWorld.Posted Source, string HoldId, string Decision)> PendingAsync(ReversalWorld w, string? location = null)
    {
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE", location: location);
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var draft = await w.S.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 4m)]);
        await w.S.SubmitAsync(Id(draft, "decisionId"));
        return (source, holdId, Id(draft, "decisionId"));
    }

    /// <summary>AC-3 (a) + AC-19: a PendingApproval hold is voided with its decision; the approval that comes later is 200 Voided, writes nothing, posts nothing.</summary>
    [Fact]
    public async Task AC03a_a_later_approval_of_a_voided_decision_changes_nothing()
    {
        var w = new ReversalWorld(_factory);
        var (source, holdId, decision) = await PendingAsync(w);
        var before = w.LedgerCount();

        await w.ReverseAsync(source);

        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", decision), (hold["Status"].AsString, hold["Voided"]["DecisionId"].AsString));
        Assert.Equal(["Submitted", "WorkflowStarted", "Voided"], w.EventKinds(decision));
        Assert.Equal("Voided", Id(await w.S.OutcomeAsync(decision), "state"));
        Assert.Equal(["Submitted", "WorkflowStarted", "Voided"], w.EventKinds(decision));
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["IdempotencyKey"].AsString.StartsWith($"quarantine:{decision}", StringComparison.Ordinal));
        Assert.Equal(before + 1, w.LedgerCount()); // AC-19: the REVERSAL only
        using var scope = w.Scope();
        Assert.Equal(QuarantineWorkflowReconciler.Result.Nothing,
            await scope.ServiceProvider.GetRequiredService<QuarantineWorkflowReconciler>().ReconcileAsync([Guid.Parse(decision)], force: true, CancellationToken.None));
    }

    /// <summary>AC-3 (b): a rejection / cancellation / time-out after the close is 200 Voided; no such event or history row; an outcome naming another instance too (V8).</summary>
    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    [InlineData("TimedOut")]
    public async Task AC03b_any_later_outcome_of_a_voided_decision_is_Voided(string outcome)
    {
        var w = new ReversalWorld(_factory);
        var (source, holdId, decision) = await PendingAsync(w);
        await w.ReverseAsync(source);

        Assert.Equal("Voided", Id(await w.S.OutcomeAsync(decision, outcome), "state"));
        Assert.DoesNotContain(outcome, w.EventKinds(decision));
        Assert.DoesNotContain(outcome, w.Actions(holdId));
        var hold = w.HoldDoc(holdId);
        Assert.Equal(("Voided", true), (hold["Status"].AsString, hold["ActiveDecisionId"].IsBsonNull));
        using var scope = w.Scope(w.S.Approver);
        var named = await scope.ServiceProvider.GetRequiredService<QuarantineOutcomeApplier>()
            .ApplyOutcomeAsync(Guid.Parse(decision), ApprovalOutcome.Approved, w.S.Approver, "another-instance", CancellationToken.None);
        Assert.Equal((true, DecisionState.Voided), (named.IsSuccessful, named.Data));
    }

    /// <summary>AC-11 (a): the observer closes; two sweeps and the observer again change nothing — one history row, one record, one version step.</summary>
    [Fact]
    public async Task AC11a_the_observer_close_happens_exactly_once()
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var version = w.HoldDoc(holdId)["Version"].AsInt32;
        var reversal = await w.ReverseAsync(source);
        Assert.Equal("Voided", w.HoldDoc(holdId)["Status"].AsString);

        Assert.Equal(0, (await w.SweepAsync()).Changes);
        Assert.Equal(0, (await w.SweepAsync()).Changes);
        var again = (await w.ObserveAsync(reversal))[0].Data!;
        Assert.Equal((SourceReversalVerdict.NoChange, SourceReversalNoChange.AlreadyRecorded), (again.Outcome, again.NoChange));
        Assert.Single(w.History(holdId), h => h["Action"].AsString == "SourceReversed");
        Assert.Single(w.ReversalAudit(holdId));
        Assert.Equal(version + 1, w.HoldDoc(holdId)["Version"].AsInt32);
    }

    /// <summary>AC-11 (a) reverse order: the observer fails (staged), the posting is still 201, the sweep closes, the observer again → nothing.</summary>
    [Fact]
    public async Task AC11a_a_failed_observer_is_covered_by_the_sweep_once()
    {
        var w = new ReversalWorld(_factory);
        var source = await w.ReceiveAsync("4", await w.S.ReleasedLotAsync(), "QUARANTINE");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var failOnce = 0;
        _factory.Hooks.Before = c => c.Service == typeof(IQuarantineRepository) && c.Method == nameof(IQuarantineRepository.FindHoldBySourceMovementAsync)
                                     && c.Arg<string>(0) == source.Tx && Interlocked.Exchange(ref failOnce, 1) == 0
            ? throw new InvalidOperationException("staged observer failure")
            : Task.CompletedTask;
        string reversal;
        try
        {
            reversal = await w.ReverseAsync(source); // 201
        }
        finally
        {
            _factory.Hooks.Clear();
        }

        Assert.Equal("Open", w.HoldDoc(holdId)["Status"].AsString);
        Assert.Equal(1, (await w.SweepAsync()).Voided);
        Assert.Equal(SourceReversalNoChange.AlreadyRecorded, (await w.ObserveAsync(reversal))[0].Data!.NoChange);
        Assert.Single(w.History(holdId), h => h["Action"].AsString == "SourceReversed");
        Assert.Single(w.ReversalAudit(holdId));
    }

    /// <summary>
    /// AC-18 retest queue (predicate measured on a stored state: a Retest hold is MOD-0175's own and a reversal never voids it, R1) — a Voided
    /// retest hold no longer makes its lot "Quarantined".
    /// </summary>
    [Fact]
    public async Task AC18_the_retest_queue_does_not_count_a_voided_hold()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(400)));
        await w.S.Lots.ReleaseAsync(lot);
        await w.S.Lots.PostOkAsync(w.S.Lots.Receipt("6", lot));
        await w.S.Lots.SetRetestDateAsync(lot, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2));
        var key = QuarantineScenario.Key("rt");
        var hold = (await w.S.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key))
            .EnumerateArray().Single();
        bool Quarantined(System.Text.Json.JsonElement list) => list.EnumerateArray().Any(i => Id(i, "lotId") == lot && Id(i, "state") == "Quarantined");
        Assert.True(Quarantined(await w.S.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest", null, 200)));

        _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", Id(hold, "holdId")), new BsonDocument("$set", new BsonDocument("Status", "Voided")));

        Assert.False(Quarantined(await w.S.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest", null, 200)));
    }

    /// <summary>AC-20 (c): in ONE DI scope, the source-reversal command then another marked command — the second record keeps the caller.</summary>
    [Fact]
    public async Task AC20c_the_system_mode_ends_with_the_dispatch_in_the_same_scope()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var other = await w.ReceiveAsync("2", lot, "QUARANTINE", location: w.S.Ledger.LocationB);
        var otherHold = w.HoldOfSource(other.Tx);
        var reversal = await w.ReverseAsync(source); // the observer closed it already: here the command answers AlreadyRecorded
        using var scope = w.Scope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.True((await sender.Send(new ApplySourceReversalToHoldCommand(reversal))).IsSuccessful);
        var update = await sender.Send(new UpdateHoldCommand(Guid.Parse(otherHold["_id"].AsString), true, "QA notu - ayni kapsam", null, "upd-same-scope-01", otherHold["Version"].AsInt32));
        Assert.True(update.IsSuccessful, update.ErrorCode);
        var row = Assert.Single(w.Audit(Guid.Parse(otherHold["_id"].AsString), "UpdateHoldCommand"));
        Assert.Equal((CentralAuditActorTypes.TenantUser, (Guid?)w.S.Ledger.User), (row.ActorType, row.ActorId));
    }
}

/// <summary>AC-14 (§8.4, Q-09 = A): neither observer — the intake sweep finds a movement already reversed and opens no record.</summary>
public sealed class SourceReversalLateIntakeTests : IClassFixture<NoObserverQuarantineApiFactory>
{
    private readonly NoObserverQuarantineApiFactory _factory;

    public SourceReversalLateIntakeTests(NoObserverQuarantineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC14_a_late_intake_of_a_reversed_movement_opens_no_record()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var reversed = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var kept = await w.ReceiveAsync("2", lot, "QUARANTINE", "CUSTOMER_RETURN", w.S.Ledger.LocationB);
        await w.ReverseAsync(reversed);
        Assert.Empty(w.S.Holds());

        Assert.Equal(1, await _factory.SweepAsync(w.S.Tenant));

        Assert.DoesNotContain(w.S.Holds(), h => h["SourceMovementId"].AsString == reversed.Tx);
        Assert.Single(w.S.Holds(), h => h["SourceMovementId"].AsString == kept.Tx); // the control: a live movement is still taken in
        Assert.Contains(w.Logs("quarantine.intake.source_already_reversed"), l => l.Contains(reversed.Tx, StringComparison.Ordinal));
    }
}

/// <summary>
/// AC-18 source scan: in MOD-0175's own sources no open / closed decision is written as a <c>HoldStatus.Decided</c> comparison
/// outside <c>QuarantineRecordStates</c> (it would count a Voided hold as open). The rule table / feed name Decided as a STATE, not as
/// "the only closed state", and the apply engine assigns it — neither is a comparison.
/// </summary>
public sealed class SourceReversalOpenPredicateScanTests
{
    private static readonly Regex DecidedComparison = new(
        @"[!=]=\s*HoldStatus\.Decided|HoldStatus\.Decided\s*[!=]=|\bNe\(\s*\w+\s*=>\s*\w+\.Status\s*,\s*HoldStatus\.Decided\s*\)|nameof\(HoldStatus\.Decided\)", RegexOptions.Compiled);

    [Fact]
    public void AC18_no_Decided_comparison_outside_the_predicate()
    {
        var sources = SourceScan.Production().Where(f => f.RelativePath.Contains("/Quarantine/", StringComparison.Ordinal)).ToList();
        Assert.True(sources.Count >= 40, $"only {sources.Count} quarantine sources — the scan would be vacuous");
        Assert.Contains(sources, f => f.RelativePath.EndsWith("Rules/QuarantineRecordStates.cs", StringComparison.Ordinal));
        var offenders = sources.Where(f => !f.RelativePath.EndsWith("Rules/QuarantineRecordStates.cs", StringComparison.Ordinal) && DecidedComparison.IsMatch(f.Body))
            .Select(f => f.RelativePath).ToList();
        Assert.True(offenders.Count == 0, "HoldStatus.Decided comparison outside QuarantineRecordStates:\n" + string.Join("\n", offenders));
    }
}
