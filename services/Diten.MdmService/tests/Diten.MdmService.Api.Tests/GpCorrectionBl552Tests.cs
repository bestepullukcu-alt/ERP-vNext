using System.Reflection;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
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
/// WP-MDM-GP-WF-ANSWERS-01 (BL-552), flow 1 — the Global Product CORRECTION on this host's throwaway mongod (BL-527),
/// with the accepted GSKU pattern: (A) the product's lock is given back only when the engine is known to hold nothing
/// for the request; (B) no state stays stuck for ever, unseen, without a named way out. The engine is the host's fake,
/// answering only what Platform answers (FakePlatformCodeGuardTests reads this file too); every state is produced
/// through the processor, never written into the database directly.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const string GpCorrectionObject = "GlobalProductCorrection";
    private readonly Guid _gpTemplate = Guid.NewGuid();
    private readonly Guid _gpCandidate = Guid.NewGuid();
    private readonly Guid _gpIdentityTemplate = Guid.NewGuid();

    private static GlobalProductCorrectionExecutionConfiguration GpTwoAttempts() =>
        new(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1), 2, TimeSpan.FromMilliseconds(2), TimeSpan.FromSeconds(2));

    private GlobalProductCorrectionWorkflowProcessor GpCorrection(IGlobalProductRepository? store = null) =>
        new(new GlobalProductCorrectionOperationRepository(host.Database, Tenant()),
            store ?? new GlobalProductRepository(host.Database, Tenant()), host.Engine,
            new GlobalProductCorrectionWorkflowStartRequestFactory(new(_gpTemplate, null, [_gpCandidate], "CORRECTION",
                false, false, null, _gpIdentityTemplate, null), host.Clock),
            host.Clock);

    // The maker's own call: the host's ordinary retry delay, so a request that met a fault answers instead of going
    // round again at once (the worker's attempts use GpTwoAttempts).
    private Task<GlobalProductCorrectionProcessingResult> GpCorrectAsync(Guid product, Guid key,
        IGlobalProductRepository? store = null) =>
        GpCorrection(store).StartInteractiveAsync(_tenant, product, 0, key, _maker, $"Corrected {key:N}"[..20],
            MakerToken(), GpTwoAttempts() with { RetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(10) }, default);

    private async Task<GlobalProductCorrectionOperation> GpCorrectionOf(Guid key) =>
        (await new GlobalProductCorrectionOperationRepository(host.Database, Tenant()).GetByOperationIdAsync(key))!;

    private async Task<GlobalProduct> GpOf(Guid product) =>
        (await new GlobalProductRepository(host.Database, Tenant()).GetByIdAsync(product))!;

    // The worker: what discovery hands it, as often as asked (a stuck one is not discovered and stays as it is).
    private async Task<GlobalProductCorrectionOperation> GpWorkerRunsAsync(Guid key, int runs,
        IGlobalProductRepository? store = null, GlobalProductCorrectionExecutionConfiguration? execution = null)
    {
        var worker = GpCorrection(store);
        for (var run = 0; run < runs; run++)
        {
            await Task.Delay(5);
            var current = await GpCorrectionOf(key);
            if (current.Checkpoint is GlobalProductCorrectionCheckpoint.ManualReconciliationRequired
                or GlobalProductCorrectionCheckpoint.Completed) break;
            await worker.RecoverAsync(current, $"gp-worker-{run}", execution ?? GpTwoAttempts(), default);
        }
        return await GpCorrectionOf(key);
    }

    private async Task<(Guid Product, Guid Key)> GpAwaitingDecisionAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        var started = await GpCorrectAsync(product, key);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, started.Operation!.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        return (product, key);
    }

    private async Task<(long Manual, long Person)> GpCorrectionGaugesAsync()
    {
        var repository = new GlobalProductCorrectionOperationRepository(host.Database, Tenant());
        return (await repository.CountManualReconciliationAsync(), await repository.CountNeedingAPersonAsync());
    }

    // ── Item 1: the engine's three answers ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC_An_approval_closed_without_a_decision_ends_the_correction_and_frees_the_product()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(host.Engine.Open(GpCorrectionObject, key.ToString("D")));

        var ended = await GpWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition); // ended, not stuck
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.ClosedWithoutDecision, ended.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.StartsWith("Product ", gp.GlobalProductName); // not applied
        Assert.Single(gp.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductCorrectionEnded);
        Assert.Equal((0L, 0L), await GpCorrectionGaugesAsync());
    }

    [Fact]
    public async Task GPC_An_escalated_approval_still_open_waits_with_the_lock_held()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.Escalate(host.Engine.Open(GpCorrectionObject, key.ToString("D")));

        var waiting = await GpWorkerRunsAsync(key, 3);

        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, waiting.RecoveryDisposition);
        Assert.Equal(0, waiting.FailedAttemptCount);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC_Inconsistent_evidence_is_stuck_with_the_lock_and_the_requesters_repeat_reads_the_engine_again()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var instance = host.Engine.Open(GpCorrectionObject, key.ToString("D"));
        instance.EvidenceInconsistent = true;

        var stuck = await GpWorkerRunsAsync(key, 2);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.EvidenceConflict, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 0L), await GpCorrectionGaugesAsync()); // stuck, but the requester's repeat is its way out

        // Platform settles it and the approver decides: the requester's same command reads the engine again.
        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, Guid.NewGuid());
        var repeat = await GpCorrectAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, repeat.Operation!.Checkpoint);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal($"Corrected {key:N}"[..20], gp.GlobalProductName);
    }

    // ── Item 2: the start LOOKUP's answer is read apart from the start's ───────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task GPC_A_refused_lookup_keeps_the_lock(ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpCorrectAsync(product, key)).Succeeded);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            using var crossed1 = host.Clock.Shift(TimeSpan.FromSeconds(3)); // past the settle window: only the refusal can decide now
            var after = await GpWorkerRunsAsync(key, 3);

            Assert.Equal(GlobalProductCorrectionCheckpoint.StartOutcomeUnknown, after.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task GPC_The_starts_own_403_frees_the_product()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN", false);

        var refused = await GpCorrectAsync(product, key);

        Assert.False(refused.Succeeded);
        var ended = await GpCorrectionOf(key);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC_Nothing_arrived_inside_the_settle_window_keeps_the_lock_and_after_it_frees_the_product()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpCorrectAsync(product, key)).Succeeded);

        // One worker attempt per run (a realistic delay): the settle wait is what is observed, not a loop of them.
        var oneAttempt = GpTwoAttempts() with { RetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(10) };
        var settling = await GpWorkerRunsAsync(key, 1, execution: oneAttempt);
        Assert.Equal(GlobalProductCorrectionCheckpoint.StartOutcomeUnknown, settling.Checkpoint);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartSettling, settling.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);

        using var crossed2 = host.Clock.Shift(TimeSpan.FromSeconds(3));
        var ended = await GpWorkerRunsAsync(key, 2, execution: oneAttempt);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── Items 3 and 5: refused writes, the limit's cause, the gauges ──────────────────────────────────────────────

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_EXCEEDED")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE")]
    public async Task GPC_A_write_refusal_that_passes_by_itself_is_never_stuck(string code)
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyCorrectionDecisionAsync), code);

        var waiting = await GpWorkerRunsAsync(key, 5, refusing);

        Assert.Equal(GlobalProductCorrectionCheckpoint.DecisionObserved, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.RetryLimitReached, waiting.LastFailureCode);
        Assert.Equal((1L, 0L), await GpCorrectionGaugesAsync()); // seen, not a person's

        var done = await GpWorkerRunsAsync(key, 3); // the store back
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, done.Checkpoint);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC_A_concurrent_write_is_retried_counted_and_at_the_limit_needs_a_person_with_its_cause()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyCorrectionDecisionAsync),
            "PRODUCT_IDENTITY_CONCURRENCY_CONFLICT");

        var stuck = await GpWorkerRunsAsync(key, 5, refusing);

        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GpCorrectionGaugesAsync());
    }

    [Fact]
    public async Task GPC_Any_other_write_refusal_is_stuck_at_once_for_a_person()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyCorrectionDecisionAsync),
            "PRODUCT_IDENTITY_STATE_CONFLICT");

        var stuck = await GpWorkerRunsAsync(key, 2, refusing);

        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal("PRODUCT_IDENTITY_STATE_CONFLICT", stuck.LastFailureCode);
        Assert.Equal(0, stuck.FailedAttemptCount);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GpCorrectionGaugesAsync());
    }

    // ── Item 4: the requester's repeat, a release that has to wait, the store invariant ────────────────────────────

    private async Task<(Guid Product, Guid Key)> GpStuckOnARecordAnswerAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpCorrectAsync(product, key)).Succeeded);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            var stuck = await GpWorkerRunsAsync(key, 4);
            Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
            Assert.Equal((1L, 1L), await GpCorrectionGaugesAsync());
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        return (product, key);
    }

    [Fact]
    public async Task GPC_The_repeat_says_503_when_the_engine_cannot_be_asked_and_409_when_a_person_has_to()
    {
        var (_, key) = await GpStuckOnARecordAnswerAsync();
        var product = (await GpCorrectionOf(key)).GlobalProductId;

        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Retryable;
        host.Engine.LookupFailureCode = "PRODUCT_WORKFLOW_UNAVAILABLE";
        try
        {
            var silent = await GpCorrectAsync(product, key);
            Assert.Equal(503, silent.StatusCode);
            Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, silent.ErrorCode);

            host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
            host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
            var person = await GpCorrectAsync(product, key);
            Assert.Equal(409, person.StatusCode);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.ManualReconciliationNeeded, person.ErrorCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, (await GpCorrectionOf(key)).Checkpoint);
    }

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_EXCEEDED")]
    [InlineData("GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT")]
    public async Task GPC_A_stuck_correction_whose_release_has_to_wait_goes_back_to_the_worker_and_is_released(string code)
    {
        var (product, key) = await GpStuckOnARecordAnswerAsync();
        using var crossed3 = host.Clock.Shift(TimeSpan.FromSeconds(3)); // past the settle window: "nothing under this key" is trusted

        var startsBefore = host.Engine.StartCalls;
        var repeat = await GpCorrectAsync(product, key,
            RefusingProducts(nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), code));

        // The repeat that left the request waiting stops and says so — it does not go on in this request (where
        // "nothing arrived" would read as a start never sent and send it again).
        Assert.Equal(503, repeat.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, repeat.ErrorCode);
        Assert.Equal(startsBefore, host.Engine.StartCalls);
        var waiting = await GpCorrectionOf(key);
        Assert.Equal(GlobalProductCorrectionCheckpoint.StartOutcomeUnknown, waiting.Checkpoint); // where the worker looks
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Contains(key, (await new GlobalProductCorrectionOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks, 100)).Operations.Select(x => x.OperationId));

        var ended = await GpWorkerRunsAsync(key, 3); // the store answers again
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC_The_store_refuses_manual_reconciliation_marked_retryable()
    {
        var (_, key) = await GpAwaitingDecisionAsync();
        var repository = new GlobalProductCorrectionOperationRepository(host.Database, Tenant());
        var operation = await GpCorrectionOf(key);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "probe",
            now, now + TimeSpan.FromMinutes(1).Ticks);
        Assert.NotNull(claim);

        Assert.False(await repository.AdvanceAsync(claim!, new(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, LastFailureCode: "X")));
        Assert.True(await repository.AdvanceAsync(claim!, new(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now + 1, LastFailureCode: "X",
            ReleaseLease: true)));
    }

    // ── CT item 3: the new lock release write, on the real store ──────────────────────────────────────────────────

    [Fact]
    public async Task GPC_The_release_write_is_conditional_tenant_bound_and_audited()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var gp = await GpOf(product);
        var binding = gp.ActiveLifecycleOperation!;
        LocalAuditIntent Audit(GlobalProduct p, int version) =>
            GlobalProductCorrectionAuditIntentFactory.Create(p, version, key,
                _maker, ProductAuditOperation.GlobalProductCorrectionEnded, "x",
                DateTimeOffset.UtcNow);
        var store = new GlobalProductRepository(host.Database, Tenant());

        // Another tenant cannot see it.
        var stranger = await new GlobalProductRepository(host.Database, Tenant(Guid.NewGuid()))
            .ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version));
        Assert.False(stranger.Succeeded);
        // Another operation's lock is not this one to give back.
        var other = await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding with { OperationId = Guid.NewGuid() },
            Audit(gp, gp.Version));
        Assert.False(other.Succeeded);
        Assert.Equal(ProductIdentityWriteFaults.GlobalProductReleaseConflict, other.ErrorCode);
        // A version that moved is refused before anything is written.
        var drift = await store.ReleaseLifecycleOperationAsync(product, gp.Version + 1, binding with
            { BaseProductVersion = binding.BaseProductVersion + 1 }, Audit(gp, gp.Version + 1));
        Assert.False(drift.Succeeded);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);

        var released = await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version));
        Assert.True(released.Succeeded, released.ErrorCode);
        var after = await GpOf(product);
        Assert.Null(after.ActiveLifecycleOperation);
        Assert.Equal(gp.Version + 1, after.Version);
        Assert.Single(after.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductCorrectionEnded);
        // An exact repeat answers as the first did.
        Assert.True((await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version))).IsReplay);
    }

    // ── CT item 4: the streak / settle fields are optional — a document written before them reads with defaults ────

    [Fact]
    public async Task GPC_A_correction_stored_before_the_new_fields_reads_with_defaults_and_goes_on()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        await host.Database.GetCollection<BsonDocument>(GlobalProductCorrectionOperationRepository.CollectionName)
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Unset("FailedAttemptCount").Unset("LastStartSentAtUtcTicksV1"));

        var old = await GpCorrectionOf(key);
        Assert.Equal(0, old.FailedAttemptCount);
        Assert.Null(old.LastStartSentAtUtcTicksV1);

        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, (await GpWorkerRunsAsync(key, 4)).Checkpoint);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    private IGlobalProductRepository RefusingProducts(string method, string code)
    {
        var proxy = DispatchProxy.Create<IGlobalProductRepository, RefuseProductWrite>();
        var refuse = (RefuseProductWrite)(object)proxy;
        refuse.Inner = new GlobalProductRepository(host.Database, Tenant());
        refuse.Method = method;
        refuse.Code = code;
        return proxy;
    }

    // Refuses one Global Product write with one code, as the store would; everything else is the real store.
    public class RefuseProductWrite : DispatchProxy
    {
        public IGlobalProductRepository Inner { get; set; } = null!;
        public string Method { get; set; } = "";
        public string Code { get; set; } = "";

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != Method) return method.Invoke(Inner, args);
            return Task.FromResult(new GlobalProductLifecycleWriteResult(false, null, Code));
        }
    }
}
