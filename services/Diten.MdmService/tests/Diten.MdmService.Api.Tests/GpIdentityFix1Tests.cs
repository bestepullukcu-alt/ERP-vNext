using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GP-WF-ANSWERS-01 · P-2026-10-07-GP-FLOW3-FIX1 — the review of flow 3 (the Global Product IDENTITY approval)
/// on this host's throwaway mongod (BL-527). Each test pins one item: the withdrawal write and its receipt, the settings,
/// the reconciling branches, the store's manual rule, the unsettled repeat, the earlier-version row, the abandoned
/// request (flows 1–2) and the startup check.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private static readonly TimeSpan GpiLease = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GpiRetry = TimeSpan.FromSeconds(1);

    private Task<GlobalProductIdentityWorkflowProcessingResult> GpiWithdrawAsync(Guid product, Guid command,
        IGlobalProductRepository? store = null) =>
        GpiProcessor(host.Engine, store).WithdrawInteractiveAsync(_tenant, product, 1, command, _maker,
            "REQUESTER_WITHDRAWAL", null, GpiToken(), GpiLease, GpiRetry);

    private Task<GlobalProductIdentityWorkflowProcessingResult> GpiRepeatAsync(Guid product, Guid key,
        IProductIdentityWorkflowClient? client = null, IGlobalProductRepository? store = null, TimeSpan? window = null) =>
        GpiProcessor(client ?? host.Engine, store, window).StartInteractiveAsync(_tenant, product, 0, key, _maker,
            GpiToken(), GpiLease, GpiRetry);

    // ── 1 (a): the withdrawal's local write goes through the shared refusal rules ───────────────────────────────

    [Fact]
    public async Task GPIF_A_withdrawal_whose_local_write_meets_the_audit_cap_waits_and_is_not_stuck()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        var command = Guid.NewGuid();

        var refused = await GpiWithdrawAsync(product, command, RefusingProducts(
            nameof(IGlobalProductRepository.WithdrawIdentityApprovalAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"));

        Assert.False(refused.Succeeded);
        var waiting = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal((0L, 0L), await GpiGaugesAsync());

        // The cap passes; the requester's same withdrawal finishes it.
        using var due = host.Clock.Shift(TimeSpan.FromSeconds(30));
        var done = await GpiWithdrawAsync(product, command);
        Assert.True(done.Succeeded, done.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GpOf(product)).LifecycleStatus);
    }

    // ── FIX2 item 1: the same at the REAL store — a product at the audit cap is not a binding conflict ───────────────

    // Fillers up to the cap, beside what the product already holds (what an audit delivery behind leaves).
    private async Task GpFillToTheAuditCapAsync(Guid product)
    {
        var gp = await GpOf(product);
        var template = gp.AuditIntents.First();
        var fillers = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate - gp.AuditIntents.Count).Select(i =>
        {
            var copy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LocalAuditIntent>(template.ToBsonDocument());
            copy.IntentId = Guid.NewGuid();
            copy.IdempotencyKey = $"filler-{i}-{Guid.NewGuid():N}";
            return copy;
        }).ToList();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.PushEach(x => x.AuditIntents, fillers));
        Assert.Equal(AuditIntentLimits.MaxPerAggregate, (await GpOf(product)).AuditIntents.Count);
    }

    private async Task GpClearTheFillersAsync(Guid product) =>
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.PullFilter(x => x.AuditIntents, x => x.IdempotencyKey.StartsWith("filler-")));

    [Fact]
    public async Task GPIF2_A_withdrawal_at_the_real_stores_audit_cap_waits_and_is_not_stuck()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        await GpFillToTheAuditCapAsync(product);
        var command = Guid.NewGuid();

        var refused = await GpiWithdrawAsync(product, command);

        Assert.False(refused.Succeeded);
        var waiting = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", waiting.LastFailureCode);
        Assert.Equal((0L, 0L), await GpiGaugesAsync());

        await GpClearTheFillersAsync(product);
        using var due = host.Clock.Shift(TimeSpan.FromSeconds(30));
        var done = await GpiWithdrawAsync(product, command);
        Assert.True(done.Succeeded, done.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GpOf(product)).LifecycleStatus);
    }

    [Theory]
    [InlineData(true)]   // the approval decided: the decision's write
    [InlineData(false)]  // closed without a decision: back to draft
    public async Task GPIF2_A_decision_at_the_real_stores_audit_cap_waits_and_is_not_stuck(bool decided)
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        if (decided) host.Engine.Approve(GpiInstance(product), host.Approver);
        else host.Engine.CloseWithoutDecision(GpiInstance(product));
        await GpFillToTheAuditCapAsync(product);

        await GpiProcessor(host.Engine).RecoverAsync(await GpiOpOf(key), "gpi-worker", GpiLease, GpiRetry);

        var waiting = await GpiOpOf(key);
        Assert.NotEqual(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", waiting.LastFailureCode);
        Assert.Equal((0L, 0L), await GpiGaugesAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    // ── 1 (b): a withdrawal intent compacted between the apply and its check is not an inconsistency ──────────────

    [Fact]
    public async Task GPIF_A_withdrawal_whose_intent_was_compacted_before_its_check_completes_and_its_repeat_is_done()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        var command = Guid.NewGuid();
        var proxy = DispatchProxy.Create<IGlobalProductRepository, CompactAfterWithdrawal>();
        var compacting = (CompactAfterWithdrawal)(object)proxy;
        compacting.Inner = new GlobalProductRepository(host.Database, Tenant());
        compacting.Compact = () => CompactAsync(product, ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn);

        var first = await GpiWithdrawAsync(product, command, proxy);
        var repeat = await GpiWithdrawAsync(product, command);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, (await GpiOpOf(key)).Checkpoint);
        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.True(repeat.IsReplay);
        Assert.Empty((await GpOf(product)).AuditIntents
            .Where(x => x.Operation == ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn));
    }

    // The withdrawal write, then the audit delivery's compaction of its intent before anything else reads the product.
    public class CompactAfterWithdrawal : DispatchProxy
    {
        public IGlobalProductRepository Inner { get; set; } = null!;
        public Func<Task> Compact { get; set; } = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = method!.Invoke(Inner, args);
            return method.Name == nameof(IGlobalProductRepository.WithdrawIdentityApprovalAsync)
                ? ThenCompactAsync((Task<GlobalProductLifecycleWriteResult>)result!)
                : result;
        }

        private async Task<GlobalProductLifecycleWriteResult> ThenCompactAsync(Task<GlobalProductLifecycleWriteResult> write)
        {
            var written = await write;
            if (written.Succeeded) await Compact();
            return written;
        }
    }

    // ── 3: the settle window and the record-answer limit are checked, never clamped ──────────────────────────────

    [Fact]
    public void GPIF_The_identity_worker_refuses_a_settle_window_or_a_limit_out_of_bounds()
    {
        static ProductIdentityWorkflowWorkerOptions Options(int settle = 120, int quarantine = 30) => new()
        {
            Enabled = true, LeaseOwner = "worker", StartSettleSeconds = settle, QuarantineAfterFailedAttempts = quarantine
        };
        Options().EnsureValidWhenEnabled();
        Options(settle: 1).EnsureValidWhenEnabled();
        Options(settle: 3600).EnsureValidWhenEnabled();
        Options(quarantine: 0).EnsureValidWhenEnabled();
        Options(quarantine: 1000).EnsureValidWhenEnabled();
        foreach (var bad in new[] { Options(settle: 0), Options(settle: -1), Options(settle: 3601),
                     Options(quarantine: -5), Options(quarantine: 1001) })
            Assert.Throws<InvalidOperationException>(bad.EnsureValidWhenEnabled);
        Assert.False(ProductIdentityWorkflowWorkerOptions.IsRecordAnswerWindowValid(0, 30));
        Assert.False(ProductIdentityWorkflowWorkerOptions.IsRecordAnswerWindowValid(120, -5));
        Assert.True(ProductIdentityWorkflowWorkerOptions.IsRecordAnswerWindowValid(120, 30));
    }

    // ── 4 (a): the reconciling repeat keeps the draft held inside the settle window ───────────────────────────────

    [Fact]
    public async Task GPIF_The_reconciling_repeat_inside_the_settle_window_says_503_and_keeps_the_draft_held()
    {
        var (product, key) = await GpiStuckOnARecordAnswerAsync();

        var inside = await GpiRepeatAsync(product, key, window: TimeSpan.FromSeconds(60));

        Assert.Equal(503, inside.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, inside.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(HttpStatusCode.Conflict, (await GpiEditAsync(product, 0)).StatusCode);
    }

    // ── 4 (b): the reconciling repeat after the engine's approval is on record, branch by branch ─────────────────

    // Stuck right after the start: the pending write met a state the store will not take (not a wait) — a person's.
    private async Task<(Guid Product, Guid Key)> GpiStuckAfterTheStartAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var stuck = await GpiRepeatAsync(product, key, store: RefusingProducts(
            nameof(IGlobalProductRepository.SubmitIdentityAsync), "PRODUCT_IDENTITY_STATE_CONFLICT"));
        Assert.False(stuck.Succeeded);
        var operation = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.NotNull(operation.WorkflowInstanceId);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GpOf(product)).LifecycleStatus);
        return (product, key);
    }

    [Fact]
    public async Task GPIF_A_submit_stuck_after_its_start_with_the_product_still_a_draft_is_started_again_by_the_repeat()
    {
        var (product, key) = await GpiStuckAfterTheStartAsync();

        var repeat = await GpiRepeatAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPIF_A_submit_stuck_with_its_approval_still_open_is_put_back_to_wait_by_the_repeat()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        var instance = GpiInstance(product);
        instance.EvidenceInconsistent = true;
        await GpiRunWorkerAsync(key, 2);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await GpiOpOf(key)).Checkpoint);
        instance.EvidenceInconsistent = false; // the engine's facts agree again; nobody has decided yet

        var repeat = await GpiRepeatAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.False(repeat.IsReplay && repeat.Operation!.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPIF_A_submit_stuck_with_its_decision_on_record_has_it_applied_by_the_repeat()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        host.Engine.Approve(GpiInstance(product), host.Approver);
        await GpiProcessor(host.Engine, RefusingProducts(nameof(IGlobalProductRepository.ReconcileIdentityDecisionAsync),
                "PRODUCT_IDENTITY_STATE_CONFLICT"))
            .RecoverAsync(await GpiOpOf(key), "gpi-worker", GpiLease, GpiRetry);
        var stuck = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.NotNull(stuck.DecisionKind);

        var repeat = await GpiRepeatAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GpOf(product)).LifecycleStatus);
    }

    // The decision on record is applied even when the engine can only say "incomplete" on the repeat — the recorded
    // decision is the submit's, not the engine's answer of the moment.
    [Fact]
    public async Task GPIF_A_submit_stuck_with_its_decision_on_record_has_it_applied_even_when_the_engine_answers_incomplete()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        host.Engine.Approve(GpiInstance(product), host.Approver);
        await GpiProcessor(host.Engine, RefusingProducts(nameof(IGlobalProductRepository.ReconcileIdentityDecisionAsync),
                "PRODUCT_IDENTITY_STATE_CONFLICT"))
            .RecoverAsync(await GpiOpOf(key), "gpi-worker", GpiLease, GpiRetry);
        Assert.NotNull((await GpiOpOf(key)).DecisionKind);
        var proxy = DispatchProxy.Create<IProductIdentityWorkflowClient, TerminalEvidenceIncomplete>();
        ((TerminalEvidenceIncomplete)(object)proxy).Inner = host.Engine;

        var repeat = await GpiRepeatAsync(product, key, client: proxy);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GpOf(product)).LifecycleStatus);
    }

    public class TerminalEvidenceIncomplete : DispatchProxy
    {
        public IProductIdentityWorkflowClient Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IProductIdentityWorkflowClient.GetTerminalEvidenceAsync)
                ? Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                    ProductIdentityWorkflowTransportOutcome.Incomplete, "WORKFLOW_START_NOT_COMPLETED"))
                : method.Invoke(Inner, args);
    }

    [Fact]
    public async Task GPIF_A_submit_stuck_whose_approval_the_engine_does_not_know_stays_a_persons()
    {
        var (product, key) = await GpiStuckAfterTheStartAsync();
        var proxy = DispatchProxy.Create<IProductIdentityWorkflowClient, TerminalEvidenceNotFound>();
        ((TerminalEvidenceNotFound)(object)proxy).Inner = host.Engine;

        var repeat = await GpiRepeatAsync(product, key, client: proxy);

        Assert.Equal(409, repeat.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.ManualReconciliationNeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal((1L, 1L), await GpiGaugesAsync());
    }

    // The engine answers "no such approval" for the decision evidence; everything else is the host's engine.
    public class TerminalEvidenceNotFound : DispatchProxy
    {
        public IProductIdentityWorkflowClient Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IProductIdentityWorkflowClient.GetTerminalEvidenceAsync)
                ? Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                    ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"))
                : method.Invoke(Inner, args);
    }

    // ── 4 (c): the product writes of the decision path go through the shared refusal rules ────────────────────────

    [Theory]
    [InlineData(nameof(IGlobalProductRepository.ReturnIdentityToDraftAsync))]
    [InlineData(nameof(IGlobalProductRepository.ReconcileIdentityDecisionAsync))]
    public async Task GPIF_A_decision_write_that_meets_the_audit_cap_waits_and_is_not_stuck(string write)
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        if (write == nameof(IGlobalProductRepository.ReturnIdentityToDraftAsync))
            host.Engine.CloseWithoutDecision(GpiInstance(product));
        else
            host.Engine.Approve(GpiInstance(product), host.Approver);

        await GpiProcessor(host.Engine, RefusingProducts(write, "AUDIT_INTENT_CAPACITY_EXCEEDED"))
            .RecoverAsync(await GpiOpOf(key), "gpi-worker", GpiLease, GpiRetry);

        var waiting = await GpiOpOf(key);
        Assert.NotEqual(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", waiting.LastFailureCode);
        Assert.Equal((0L, 0L), await GpiGaugesAsync());
    }

    // ── 4 (d) and (e): an INCOMPLETE answer to the start lookup waits; on a repeat it is the engine's silence ───────

    [Fact]
    public async Task GPIF_An_incomplete_start_lookup_waits_with_the_draft_held()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpiSubmitAsync(product, 0, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Incomplete;
        host.Engine.LookupFailureCode = "WORKFLOW_START_NOT_COMPLETED";
        try
        {
            using var crossed = host.Clock.Shift(TimeSpan.FromSeconds(3));
            await GpiRunWorkerAsync(key, 2);

            var waiting = await GpiOpOf(key);
            Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, waiting.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
            // A service's silence, not an answer about the record: not counted towards a person.
            Assert.Equal(0, waiting.FailedAttemptCount);
            Assert.Equal(HttpStatusCode.Conflict, (await GpiEditAsync(product, 0)).StatusCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task GPIF_A_reconciling_repeat_answered_incomplete_says_503()
    {
        var (product, key) = await GpiStuckOnARecordAnswerAsync();
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Incomplete;
        host.Engine.LookupFailureCode = "WORKFLOW_START_NOT_COMPLETED";
        try
        {
            var repeat = await GpiRepeatAsync(product, key);

            Assert.Equal(503, repeat.StatusCode);
            Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, repeat.ErrorCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── 5: manual reconciliation only as manual ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ProductIdentityWorkflowRecoveryDisposition.None, false)]
    [InlineData(ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, false)]
    [InlineData(ProductIdentityWorkflowRecoveryDisposition.Retryable, false)]
    [InlineData(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, true)]
    public async Task GPIF_The_store_writes_manual_reconciliation_only_as_manual(
        ProductIdentityWorkflowRecoveryDisposition disposition, bool accepted)
    {
        var (_, key) = await GpiAwaitingDecisionAsync();
        var repository = new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant());
        var operation = await GpiOpOf(key);
        var later = host.Clock.GetUtcNow().AddDays(1);
        var claim = await repository.TryClaimAsync(new(key, operation.OperationFingerprint, [operation.Checkpoint],
            "probe", later.UtcTicks, later.AddMinutes(1).UtcTicks, IgnoreRetrySchedule: true));
        Assert.NotNull(claim);

        var advanced = await repository.AdvanceAsync(claim!, new(
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, disposition, later.UtcTicks,
            LastFailureCode: "PRODUCT_IDENTITY_STATE_CONFLICT", ReleaseLease: true));

        Assert.Equal(accepted, advanced);
    }

    // ── 9: a submit awaiting its decision is completed from there only by ending ─────────────────────────────────

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowProcessor.ClosedWithoutDecision, true)]
    [InlineData(GlobalProductIdentityWorkflowProcessor.SelfApprovalIgnored, true)]
    [InlineData(null, false)]
    [InlineData("PRODUCT_IDENTITY_STATE_CONFLICT", false)]
    public async Task GPIF_A_submit_awaiting_its_decision_is_completed_from_there_only_by_ending(string? code, bool accepted)
    {
        var (_, key) = await GpiAwaitingDecisionAsync();
        var repository = new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant());
        var operation = await GpiOpOf(key);
        var later = host.Clock.GetUtcNow().AddDays(1);
        var claim = await repository.TryClaimAsync(new(key, operation.OperationFingerprint, [operation.Checkpoint],
            "probe", later.UtcTicks, later.AddMinutes(1).UtcTicks, IgnoreRetrySchedule: true));

        var advanced = await repository.AdvanceAsync(claim!, new(GlobalProductIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, later.UtcTicks, LastFailureCode: code, ReleaseLease: true));

        Assert.Equal(accepted, advanced);
    }

    // ── 6: a repeat that cannot settle a submit hands it to a person ─────────────────────────────────────────────

    [Fact]
    public async Task GPIF_A_start_proof_that_stays_unusable_is_handed_to_a_person_by_the_repeat()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var proxy = DispatchProxy.Create<IProductIdentityWorkflowClient, UnusableStartProof>();
        ((UnusableStartProof)(object)proxy).Inner = host.Engine;
        Assert.False((await GpiRepeatAsync(product, key, client: proxy)).Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StartProofInvalid, (await GpiOpOf(key)).LastFailureCode);
        Assert.Equal((1L, 0L), await GpiGaugesAsync()); // its submitter's repeat is meant to settle it

        // The proof is still unusable on the repeat: nothing the repeat can settle — a person's now, and counted.
        var lookupProxy = DispatchProxy.Create<IProductIdentityWorkflowClient, UnusableStartLookup>();
        ((UnusableStartLookup)(object)lookupProxy).Inner = host.Engine;
        var repeat = await GpiRepeatAsync(product, key, client: lookupProxy);

        Assert.Equal(409, repeat.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.ManualReconciliationNeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StuckOnRecordAnswer, (await GpiOpOf(key)).LastFailureCode);
        Assert.Equal((1L, 1L), await GpiGaugesAsync());
    }

    // The start lookup's answer cannot be used either (another object), as the start's was.
    public class UnusableStartLookup : DispatchProxy
    {
        public IProductIdentityWorkflowClient Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = method!.Invoke(Inner, args);
            if (method.Name != nameof(IProductIdentityWorkflowClient.GetStartResultAsync)) return result;
            var found = ((Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>>)result!).GetAwaiter().GetResult();
            return Task.FromResult(found.Outcome == ProductIdentityWorkflowTransportOutcome.Success
                ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                    found.Value! with { ObjectRef = "another-object" })
                : found);
        }
    }

    // ── 14: the gauges of a submit stuck at an earlier version ───────────────────────────────────────────────────

    [Fact]
    public async Task GPIF_A_submit_stuck_at_an_earlier_version_still_counts_on_both_gauges()
    {
        var (product, _) = await GpiStuckOnARecordAnswerAsync();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Inc(x => x.Version, 1));

        Assert.True((await GpiEditAsync(product, 1)).IsSuccessStatusCode);
        Assert.Equal((1L, 1L), await GpiGaugesAsync());
    }

    // ── 15 (flows 1–2): a request that never took the product ends after the limit ──────────────────────────────

    [Fact]
    public async Task GPIF_A_correction_that_never_took_the_product_waits_and_after_the_limit_ends()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        Assert.Equal(503, (await GpCorrectAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"))).StatusCode);

        using (host.Clock.Shift(TimeSpan.FromDays(6)))
        {
            var waiting = await GpWorkerRunsAsync(key, 1);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.AwaitingMaker, waiting.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        }
        using (host.Clock.Shift(TimeSpan.FromDays(8)))
        {
            var ended = await GpWorkerRunsAsync(key, 1);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.MakerNeverReturned, ended.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
            Assert.Equal((0L, 0L), await GpCorrectionGaugesAsync());
            // Flow 3 FIX2 K3: the maker who comes back is told why it ended.
            var late = await GpCorrectAsync(product, key);
            Assert.Equal(409, late.StatusCode);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.MakerNeverReturned, late.ErrorCode);
        }
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPIF_A_retirement_that_never_took_the_product_waits_and_after_the_limit_ends()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        Assert.Equal(503, (await GpRetireAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"))).StatusCode);

        // Flow 3 FIX2 K3: before the limit it waits for its maker.
        using (host.Clock.Shift(TimeSpan.FromDays(6)))
        {
            var waiting = await GprWorkerRunsAsync(key, 1);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.AwaitingMaker, waiting.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        }
        using (host.Clock.Shift(TimeSpan.FromDays(8)))
        {
            var ended = await GprWorkerRunsAsync(key, 2);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.MakerNeverReturned, ended.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
            var late = await GpRetireAsync(product, key);
            Assert.Equal(409, late.StatusCode);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.MakerNeverReturned, late.ErrorCode);
        }
    }
}

/// <summary>
/// Flow 3 FIX1 item 11: the startup check proven on PRODUCTION's Register(), with a broken map registered first — in
/// an isolated load context, so this process's own class maps (registered by every other test) are not touched.
/// </summary>
public sealed class GpLifecycleClassMapRegisterIsolationTests
{
    [Fact]
    public void GPIF_Register_refuses_to_start_when_a_lifecycle_map_was_registered_without_ignoring_extra_elements()
    {
        var context = new IsolatedMdm(Path.GetDirectoryName(typeof(GpLifecycleClassMapRegisterIsolationTests).Assembly.Location)!);
        try
        {
            var bson = context.LoadFromAssemblyName(new AssemblyName("MongoDB.Bson"));
            var domain = context.LoadFromAssemblyName(new AssemblyName("Diten.MdmService.Domain"));
            var persistence = context.LoadFromAssemblyName(new AssemblyName("Diten.MdmService.Persistence"));
            var operation = domain.GetType("Diten.MdmService.Domain.Entities.GlobalProductCorrectionOperation", true)!;
            var classMap = bson.GetType("MongoDB.Bson.Serialization.BsonClassMap", true)!;
            var map = Activator.CreateInstance(classMap, operation)!;
            classMap.GetMethod("AutoMap", Type.EmptyTypes)!.Invoke(map, null);
            classMap.GetMethod("SetIgnoreExtraElements")!.Invoke(map, [false]);
            classMap.GetMethod("RegisterClassMap", [classMap])!.Invoke(null, [map]);
            var register = persistence.GetType("Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps", true)!
                .GetMethod("Register")!;

            var refused = Assert.Throws<TargetInvocationException>(() => register.Invoke(null, null));

            Assert.IsType<InvalidOperationException>(refused.InnerException);
            Assert.Contains("GlobalProductCorrectionOperation", refused.InnerException!.Message);
        }
        finally
        {
            context.Unload();
        }
    }

    // Flow 3 FIX2: a store built outside the host's container (a tool, a test) registers the guarded map before it is
    // first used — before, its first read auto-mapped the type unguarded and every later host start refused it.
    [Theory]
    [InlineData("GlobalProductIdentityWorkflowOperationRepository", "GlobalProductIdentityWorkflowOperation")]
    [InlineData("GlobalProductCorrectionOperationRepository", "GlobalProductCorrectionOperation")]
    [InlineData("GlobalProductRetirementRequestOperationRepository", "GlobalProductRetirementRequestOperation")]
    [InlineData("GlobalProductRepository", "GlobalProductIdentityWorkflowOperation")]
    [InlineData("GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository", "GlobalProductIdentityWorkflowOperation")]
    public void GPIF2_A_lifecycle_store_registers_the_guarded_map_before_its_first_use(string store, string document)
    {
        var context = new IsolatedMdm(Path.GetDirectoryName(typeof(GpLifecycleClassMapRegisterIsolationTests).Assembly.Location)!);
        try
        {
            var bson = context.LoadFromAssemblyName(new AssemblyName("MongoDB.Bson"));
            var domain = context.LoadFromAssemblyName(new AssemblyName("Diten.MdmService.Domain"));
            var persistence = context.LoadFromAssemblyName(new AssemblyName("Diten.MdmService.Persistence"));
            var storeType = persistence.GetType($"Diten.MdmService.Persistence.Repositories.{store}", true)!;
            var documentType = domain.GetType($"Diten.MdmService.Domain.Entities.{document}", true)!;

            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(storeType.TypeHandle);

            var classMap = bson.GetType("MongoDB.Bson.Serialization.BsonClassMap", true)!;
            Assert.True((bool)classMap.GetMethod("IsClassMapRegistered")!.Invoke(null, [documentType])!);
            var map = classMap.GetMethod("LookupClassMap")!.Invoke(null, [documentType])!;
            Assert.True((bool)classMap.GetProperty("IgnoreExtraElements")!.GetValue(map)!);
        }
        finally
        {
            context.Unload();
        }
    }

    // MDM's own assemblies and the Mongo driver, loaded afresh (their statics apart from this process's); the rest shared.
    private sealed class IsolatedMdm(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) =>
            name.Name is { } simple && (simple.StartsWith("Diten.MdmService.", StringComparison.Ordinal)
                                        || simple.StartsWith("MongoDB.", StringComparison.Ordinal))
            && File.Exists(Path.Combine(directory, simple + ".dll"))
                ? LoadFromAssemblyPath(Path.Combine(directory, simple + ".dll"))
                : null;
    }
}
