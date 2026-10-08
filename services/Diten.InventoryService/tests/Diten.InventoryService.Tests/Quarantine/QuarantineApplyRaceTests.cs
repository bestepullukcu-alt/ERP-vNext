using System.Reflection;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// CT v1.1 (WP-0175-Q2): DETERMINISTIC proof of the two concurrency guards of <c>417dc7b24</c> — no real concurrency, no waiting.
/// The real host, real ledger and real 0175 records; only the race window is staged:
/// <list type="bullet">
/// <item>S1 — two appliers of one decision. The first applies normally (Applied, one movement). The second is the loser of the
/// runtime-probe race: it read the decision BEFORE the winner's Applied (a stale 0175 view: Applied hidden) and its ledger
/// lookups missed the key the winner was committing (a stale ledger view: <c>FindByIdempotencyKeyAsync</c> → null), so its
/// posting reaches the real ledger and is refused on stock. Expected: Applied stays, NO ApplyFailed, one movement.</item>
/// <item>S2 — the in-flight grace, with an injected clock: a FRESH Approved is left to the approving call (state Approved,
/// nothing posted); an Approved older than <see cref="QuarantineOutcomeApplier.InFlightGrace"/> is finished (Applied, posted).</item>
/// </list>
/// K3: S1 guard removed → S1 red; <c>InFlightGrace = Zero</c> → the fresh case red; <c>InFlightGrace = MaxValue</c> → the stale case red.
/// </summary>
public sealed class QuarantineApplyRaceTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineApplyRaceTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    /// <summary>A DI scope with the scenario's tenant, company and decider as the caller (what the scope middleware sets).</summary>
    private IServiceScope ScopeOf(QuarantineScenario s)
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.Ledger.LegalEntity, s.Ledger.User);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        return scope;
    }

    private List<string> KindsOf(QuarantineScenario s, string decisionId)
        => s.Events().Where(e => e["DecisionId"].AsString == decisionId).OrderBy(e => e["Sequence"].ToInt64()).Select(e => e["Kind"].AsString).ToList();

    [Fact]
    public async Task S1_a_loser_whose_same_key_posting_is_refused_after_the_winner_applied_records_no_ApplyFailed()
    {
        var s = new QuarantineScenario(_factory);
        var (_, hold) = await s.HoldAsync("700", await s.ReleasedLotAsync());
        var winner = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 700m)]));
        Assert.Equal("Applied", Id(winner, "state"));
        var decisionId = Guid.Parse(Id(winner, "decisionId"));
        Assert.Single(await s.LedgerMovementsAsync(Id(winner, "decisionNumber")));

        using var scope = ScopeOf(s);
        var sp = scope.ServiceProvider;
        var staleDecisions = Stale<IQuarantineDecisionRepository>.Wrap(sp.GetRequiredService<IQuarantineDecisionRepository>(), (m, args, target) =>
            m.Name == nameof(IQuarantineDecisionRepository.EventsAsync)
                ? ((Task<IReadOnlyList<QuarantineDecisionEvent>>)m.Invoke(target, args)!).ContinueWith(
                    t => (IReadOnlyList<QuarantineDecisionEvent>)t.Result.Where(e => e.Kind != DecisionEventKind.Applied).ToList(), TaskScheduler.Default)
                : Stale.Pass);
        var staleLedger = Stale<IInventoryLedgerRepository>.Wrap(sp.GetRequiredService<IInventoryLedgerRepository>(), (m, _, _) =>
            m.Name == nameof(IInventoryLedgerRepository.FindByIdempotencyKeyAsync) ? Task.FromResult<InventoryTransaction?>(null) : Stale.Pass);
        var posting = new InternalMovementPostingService(staleLedger, sp.GetRequiredService<ILotMasterClient>(), sp.GetRequiredService<MovementMasterDataValidator>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ICorrelationContext>(), sp.GetServices<IPostedMovementObserver>(),
            sp.GetRequiredService<ILogger<InternalMovementPostingService>>());
        var support = sp.GetRequiredService<QuarantineSupport>();
        var loser = new QuarantineApplyEngine(sp.GetRequiredService<IQuarantineRepository>(), staleDecisions,
            new QuarantineMovementPoster(posting, staleLedger, support), support);

        var decision = (await sp.GetRequiredService<IQuarantineDecisionRepository>().FindAsync(decisionId))!;
        var result = await loser.ApplyAsync(decision, s.Approver, CancellationToken.None);

        // the staging really reached the race: the loser's line was refused by the ledger (not answered as a replay)
        Assert.Equal(DecisionState.Applied, result.State);
        Assert.DoesNotContain("ApplyFailed", KindsOf(s, decisionId.ToString("D")));
        Assert.Equal(["Submitted", "WorkflowStarted", "Approved", "Applied"], KindsOf(s, decisionId.ToString("D")));
        Assert.Single(await s.LedgerMovementsAsync(Id(winner, "decisionNumber")));
        Assert.Equal("Applied", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200), "state"));
    }

    /// <summary>The control for S1: the same staging WITHOUT the winner's Applied record really refuses → ApplyFailed.</summary>
    [Fact]
    public async Task S1_control_the_staged_loser_posting_is_really_refused_by_the_ledger()
    {
        var s = new QuarantineScenario(_factory);
        var (_, hold) = await s.HoldAsync("300", await s.ReleasedLotAsync());
        var winner = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 300m)]));
        using var scope = ScopeOf(s);
        var sp = scope.ServiceProvider;
        var staleLedger = Stale<IInventoryLedgerRepository>.Wrap(sp.GetRequiredService<IInventoryLedgerRepository>(), (m, _, _) =>
            m.Name == nameof(IInventoryLedgerRepository.FindByIdempotencyKeyAsync) ? Task.FromResult<InventoryTransaction?>(null) : Stale.Pass);
        var posting = new InternalMovementPostingService(staleLedger, sp.GetRequiredService<ILotMasterClient>(), sp.GetRequiredService<MovementMasterDataValidator>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ICorrelationContext>(), sp.GetServices<IPostedMovementObserver>(),
            sp.GetRequiredService<ILogger<InternalMovementPostingService>>());
        var poster = new QuarantineMovementPoster(posting, staleLedger, sp.GetRequiredService<QuarantineSupport>());
        var decision = (await sp.GetRequiredService<IQuarantineDecisionRepository>().FindAsync(Guid.Parse(Id(winner, "decisionId"))))!;
        var movement = QuarantineStockEffects.Plan(decision).Single();
        var envelope = new MovementEnvelope(decision.ItemId, decision.SkuId, decision.SkuLevel, decision.LotId, decision.BaseUomId,
            "QUARANTINE_DECISION", decision.DecisionNumber, decision.Id.ToString("D"), null);

        var refused = await poster.PostAsync(movement, envelope, QuarantineMovementPoster.DecisionKey(decision.Id, 1), s.Approver, CancellationToken.None);
        Assert.False(refused.Posted);
        Assert.Equal("INSUFFICIENT_STOCK", refused.ErrorCode); // the probe's refusal, reproduced without concurrency
        Assert.Single(await s.LedgerMovementsAsync(Id(winner, "decisionNumber")));
    }

    // ── S2: in-flight grace (injected clock) ──────────────────────────────────────────────────────────────

    private async Task<(QuarantineScenario S, Guid DecisionId, string Number, DateTimeOffset ApprovedAt)> ApprovedNotAppliedAsync()
    {
        var s = new QuarantineScenario(_factory);
        var (_, hold) = await s.HoldAsync("120", await s.ReleasedLotAsync());
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 120m)]);
        var decisionId = Guid.Parse(Id(draft, "decisionId"));
        await s.SubmitAsync(decisionId.ToString("D"));

        // the approving call recorded Approved and is (still) posting: the event exists, Applied does not
        using var scope = ScopeOf(s);
        var sp = scope.ServiceProvider;
        var decision = (await sp.GetRequiredService<IQuarantineDecisionRepository>().FindAsync(decisionId))!;
        var at = QuarantineSupport.Now();
        var approved = QuarantineSupport.Event(decisionId, DecisionEventKind.Approved, at, s.Approver);
        approved.ApproverUserId = s.Approver;
        approved.WorkflowOutcome = "approved";
        approved.TenantId = s.Tenant;
        approved.LegalEntityId = decision.LegalEntityId;
        await sp.GetRequiredService<IQuarantineRepository>().InTransactionAsync(async (session, c) =>
        {
            await session.InsertDecisionEventAsync(approved, c);
            return true;
        });
        return (s, decisionId, Id(draft, "decisionNumber"), at);
    }

    private async Task<DecisionState> OutcomeAtAsync(QuarantineScenario s, Guid decisionId, DateTimeOffset now)
    {
        using var scope = ScopeOf(s);
        var sp = scope.ServiceProvider;
        var applier = new QuarantineOutcomeApplier(sp.GetRequiredService<IQuarantineRepository>(), sp.GetRequiredService<IQuarantineDecisionRepository>(),
            sp.GetRequiredService<QuarantineApplyEngine>(), sp.GetRequiredService<QuarantineFacts>(), sp.GetRequiredService<QuarantineSupport>(), new FixedClock(now));
        var result = await applier.ApplyOutcomeAsync(decisionId, ApprovalOutcome.Approved, s.Approver, null, CancellationToken.None);
        Assert.True(result.IsSuccessful, result.ErrorCode);
        return result.Data;
    }

    [Fact]
    public async Task S2_a_fresh_Approved_is_left_to_the_approving_call_nothing_is_posted()
    {
        var (s, decisionId, number, approvedAt) = await ApprovedNotAppliedAsync();
        Assert.Equal(DecisionState.Approved, await OutcomeAtAsync(s, decisionId, approvedAt + TimeSpan.FromSeconds(30)));
        Assert.Empty(await s.LedgerMovementsAsync(number));
        Assert.DoesNotContain("Applied", KindsOf(s, decisionId.ToString("D")));
    }

    [Fact]
    public async Task S2_an_Approved_older_than_the_grace_is_finished_and_posted()
    {
        var (s, decisionId, number, approvedAt) = await ApprovedNotAppliedAsync();
        Assert.Equal(DecisionState.Applied, await OutcomeAtAsync(s, decisionId, approvedAt + QuarantineOutcomeApplier.InFlightGrace + TimeSpan.FromSeconds(1)));
        Assert.Single(await s.LedgerMovementsAsync(number));
        Assert.Equal("Applied", KindsOf(s, decisionId.ToString("D")).Last());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

/// <summary>Marker of <see cref="Stale{T}"/>: "not staged — call the real implementation".</summary>
public static class Stale
{
    public static readonly object Pass = new();
}

/// <summary>A pass-through proxy over a real repository that stages a stale view for chosen methods (the race window).</summary>
public class Stale<T> : DispatchProxy where T : class
{
    private T _target = null!;
    private Func<MethodInfo, object?[]?, T, object?> _stage = null!;

    public static T Wrap(T target, Func<MethodInfo, object?[]?, T, object?> stage)
    {
        var proxy = Create<T, Stale<T>>();
        var self = (Stale<T>)(object)proxy;
        self._target = target;
        self._stage = stage;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var staged = _stage(method!, args, _target);
        return ReferenceEquals(staged, Stale.Pass) ? method!.Invoke(_target, args) : staged;
    }
}
