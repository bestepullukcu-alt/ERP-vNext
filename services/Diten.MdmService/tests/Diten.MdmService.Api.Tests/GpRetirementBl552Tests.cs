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
/// WP-MDM-GP-WF-ANSWERS-01 (BL-552), flow 2 — the Global Product RETIREMENT REQUEST on this host's throwaway mongod
/// (BL-527), with the accepted GSKU pattern: (A) the product's lock is given back only when the engine is known to hold
/// nothing for the request; (B) no state stays stuck for ever, unseen, without a named way out. The engine is the
/// host's fake, answering only what Platform answers (FakePlatformCodeGuardTests reads this file too); every state is
/// produced through the processor, never written into the database directly.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const string GpRetirementObject = GlobalProductRetirementRequestWorkflowStartRequestFactory.ObjectType;
    private const string GpRetirementReason = "The product is discontinued.";
    private readonly Guid _gprTemplate = Guid.NewGuid();

    private static GlobalProductRetirementRequestExecutionConfiguration GprTwoAttempts() =>
        new(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1), 2, TimeSpan.FromMilliseconds(2), TimeSpan.FromSeconds(2));

    // One worker attempt per run (a realistic delay).
    private static GlobalProductRetirementRequestExecutionConfiguration GprOneAttempt() =>
        GprTwoAttempts() with { RetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(10) };

    private GlobalProductRetirementRequestWorkflowProcessor GpRetirement(IGlobalProductRepository? store = null) =>
        new(new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant()),
            store ?? new GlobalProductRepository(host.Database, Tenant()), host.Engine,
            new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(_gprTemplate, null, [_gpCandidate],
                "RETIREMENT", false, false, null, _gpIdentityTemplate, null, _gpTemplate, null), host.Clock),
            host.Clock);

    // The maker's own call: the host's ordinary retry delay, so a request that met a fault answers instead of going
    // round again at once (the worker's attempts use GprTwoAttempts).
    private Task<GlobalProductRetirementRequestProcessingResult> GpRetireAsync(Guid product, Guid key,
        IGlobalProductRepository? store = null) =>
        GpRetirement(store).StartInteractiveAsync(_tenant, product, 0, key, _maker, GpRetirementReason,
            MakerToken(), GprTwoAttempts() with { RetryDelay = TimeSpan.FromMilliseconds(500),
                MaxRetryDelay = TimeSpan.FromMilliseconds(500) },
            default);

    private async Task<GlobalProductRetirementRequestOperation> GpRetirementOf(Guid key) =>
        (await new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant()).GetByOperationIdAsync(key))!;

    private async Task<GlobalProductRetirementRequestOperation> GprWorkerRunsAsync(Guid key, int runs,
        IGlobalProductRepository? store = null, GlobalProductRetirementRequestExecutionConfiguration? execution = null)
    {
        var worker = GpRetirement(store);
        for (var run = 0; run < runs; run++)
        {
            var current = await GpRetirementOf(key);
            if (current.Checkpoint is GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired
                or GlobalProductRetirementRequestCheckpoint.Completed) break;
            // The retirement store hands the worker an operation only once its next attempt is due (its claim and its
            // discovery both say so): the worker comes back then.
            var due = (current.NextAttemptAtUtcTicksV1 ?? 0) - host.Clock.GetUtcNow().UtcTicks;
            await Task.Delay(TimeSpan.FromTicks(Math.Max(0, due)) + TimeSpan.FromMilliseconds(5));
            await worker.RecoverAsync(current, $"gpr-worker-{run}", execution ?? GprTwoAttempts(), default);
        }
        return await GpRetirementOf(key);
    }

    private async Task<(Guid Product, Guid Key)> GprAwaitingDecisionAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        var started = await GpRetireAsync(product, key);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, started.Operation!.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        return (product, key);
    }

    private async Task<(long Manual, long Person)> GprGaugesAsync()
    {
        var repository = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        return (await repository.CountManualReconciliationAsync(), await repository.CountNeedingAPersonAsync());
    }

    private FakeWorkflowEngine.Instance GprInstance(Guid key) => host.Engine.Open(GpRetirementObject, key.ToString("D"));

    // ── Item 1: the engine's three answers ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPR_An_approval_closed_without_a_decision_ends_the_request_and_frees_the_product()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GprInstance(key));

        var ended = await GprWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition); // ended, not stuck
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.ClosedWithoutDecision, ended.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gp.LifecycleStatus); // not retired
        Assert.Single(gp.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductRetirementEnded);
        Assert.Equal((0L, 0L), await GprGaugesAsync());
    }

    [Fact]
    public async Task GPR_An_escalated_approval_still_open_waits_with_the_lock_held()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Escalate(GprInstance(key));

        var waiting = await GprWorkerRunsAsync(key, 3);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, waiting.RecoveryDisposition);
        Assert.Equal(0, waiting.FailedAttemptCount);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPR_Inconsistent_evidence_is_stuck_with_the_lock_and_the_requesters_repeat_reads_the_engine_again()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        var instance = GprInstance(key);
        instance.EvidenceInconsistent = true;

        var stuck = await GprWorkerRunsAsync(key, 2);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.EvidenceConflict, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 0L), await GprGaugesAsync()); // stuck, but the requester's repeat is its way out

        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, Guid.NewGuid());
        var repeat = await GpRetireAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, repeat.Operation!.Checkpoint);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, gp.LifecycleStatus);
    }

    [Fact]
    public async Task GPR_A_request_its_own_maker_decided_ends_unapplied_and_frees_the_product()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), _maker);

        var ended = await GprWorkerRunsAsync(key, 2);

        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.SelfApprovalIgnored, ended.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gp.LifecycleStatus);
    }

    // ── Item 2: the start LOOKUP's answer is read apart from the start's ───────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task GPR_A_refused_lookup_keeps_the_lock(ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpRetireAsync(product, key)).Succeeded);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            using var crossed8 = host.Clock.Shift(TimeSpan.FromSeconds(3)); // past the settle window: only the refusal can decide now
            var after = await GprWorkerRunsAsync(key, 3);

            Assert.Equal(GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, after.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task GPR_The_starts_own_403_frees_the_product()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN", false);

        Assert.False((await GpRetireAsync(product, key)).Succeeded);

        var ended = await GpRetirementOf(key);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPR_Nothing_arrived_inside_the_settle_window_keeps_the_lock_and_after_it_frees_the_product()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpRetireAsync(product, key)).Succeeded);

        var settling = await GprWorkerRunsAsync(key, 1, execution: GprOneAttempt());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, settling.Checkpoint);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartSettling, settling.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);

        using var crossed9 = host.Clock.Shift(TimeSpan.FromSeconds(3));
        var ended = await GprWorkerRunsAsync(key, 2, execution: GprOneAttempt());
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── Items 3 and 5: refused writes, the limit's cause, the gauges ──────────────────────────────────────────────

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_EXCEEDED")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE")]
    public async Task GPR_A_write_refusal_that_passes_by_itself_is_never_stuck(string code)
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync), code);

        var waiting = await GprWorkerRunsAsync(key, 5, refusing);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.DecisionObserved, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.RetryLimitReached, waiting.LastFailureCode);
        Assert.Equal((1L, 0L), await GprGaugesAsync()); // seen, not a person's

        var done = await GprWorkerRunsAsync(key, 3); // the store back
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, done.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPR_A_concurrent_write_is_retried_counted_and_at_the_limit_needs_a_person_with_its_cause()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync),
            "PRODUCT_IDENTITY_CONCURRENCY_CONFLICT");

        var stuck = await GprWorkerRunsAsync(key, 5, refusing);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GprGaugesAsync());
    }

    [Fact]
    public async Task GPR_Any_other_write_refusal_is_stuck_at_once_for_a_person()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync),
            "PRODUCT_IDENTITY_STATE_CONFLICT");

        var stuck = await GprWorkerRunsAsync(key, 2, refusing);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal("PRODUCT_IDENTITY_STATE_CONFLICT", stuck.LastFailureCode);
        Assert.Equal(0, stuck.FailedAttemptCount);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GprGaugesAsync());
    }

    // An approved retirement that meets a dependant (found after the request) cannot be applied: the request ends "not
    // applicable" and the product is given back — the GSKU retirement request's rule. Before: stuck, lock held for ever.
    [Theory]
    [InlineData("DEPENDENT_IDENTITIES_EXIST")]
    [InlineData("DRAFT_CHILD_CANCELLATION_REQUIRED")]
    public async Task GPR_An_approved_retirement_that_meets_a_dependant_ends_and_frees_the_product(string blocker)
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), Guid.NewGuid());

        var ended = await GprWorkerRunsAsync(key, 3,
            RefusingProducts(nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync), blocker));

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(blocker, ended.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gp.LifecycleStatus);
        Assert.Equal((0L, 0L), await GprGaugesAsync());
    }

    // ── Item 4: the requester's repeat, a release that has to wait, the store invariant ────────────────────────────

    private async Task<(Guid Product, Guid Key)> GprStuckOnARecordAnswerAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpRetireAsync(product, key)).Succeeded);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            var stuck = await GprWorkerRunsAsync(key, 4);
            Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
            Assert.Equal((1L, 1L), await GprGaugesAsync());
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        return (product, key);
    }

    [Fact]
    public async Task GPR_The_repeat_says_503_when_the_engine_cannot_be_asked_and_409_when_a_person_has_to()
    {
        var (product, key) = await GprStuckOnARecordAnswerAsync();

        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Retryable;
        host.Engine.LookupFailureCode = "PRODUCT_WORKFLOW_UNAVAILABLE";
        try
        {
            var silent = await GpRetireAsync(product, key);
            Assert.Equal(503, silent.StatusCode);
            Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, silent.ErrorCode);

            host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
            host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
            var person = await GpRetireAsync(product, key);
            Assert.Equal(409, person.StatusCode);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.ManualReconciliationNeeded, person.ErrorCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, (await GpRetirementOf(key)).Checkpoint);
    }

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_EXCEEDED")]
    [InlineData("GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT")]
    public async Task GPR_A_stuck_request_whose_release_has_to_wait_goes_back_to_the_worker_and_is_released(string code)
    {
        var (product, key) = await GprStuckOnARecordAnswerAsync();
        using var crossed10 = host.Clock.Shift(TimeSpan.FromSeconds(3)); // past the settle window: "nothing under this key" is trusted

        var startsBefore = host.Engine.StartCalls;
        var repeat = await GpRetireAsync(product, key,
            RefusingProducts(nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), code));

        // The repeat that left the request waiting stops and says so — it does not go on in this request.
        Assert.Equal(503, repeat.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, repeat.ErrorCode);
        Assert.Equal(startsBefore, host.Engine.StartCalls);
        var waiting = await GpRetirementOf(key);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Contains(key, (await new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks, 100)).Operations.Select(x => x.OperationId));

        var ended = await GprWorkerRunsAsync(key, 3); // the store answers again
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPR_The_store_refuses_manual_reconciliation_marked_retryable()
    {
        var (_, key) = await GprAwaitingDecisionAsync();
        var repository = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        var operation = await GpRetirementOf(key);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "probe",
            now, now + TimeSpan.FromMinutes(1).Ticks);
        Assert.NotNull(claim);

        Assert.False(await repository.AdvanceAsync(claim!, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, LastFailureCode: "X")));
        Assert.True(await repository.AdvanceAsync(claim!, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now + 1, LastFailureCode: "X",
            ReleaseLease: true)));
    }

    // ── CT item 3: the lock release write for a RETIREMENT binding, on the real store ─────────────────────────────

    [Fact]
    public async Task GPR_The_release_write_is_conditional_tenant_bound_and_audited_for_a_retirement()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        var gp = await GpOf(product);
        var binding = gp.ActiveLifecycleOperation!;
        Assert.Equal(GlobalProductLifecycleOperationKind.Retirement, binding.Kind);
        LocalAuditIntent Audit(GlobalProduct p, int version) => GlobalProductRetirementRequestAuditIntentFactory.Create(p,
            version, key, _maker, ProductAuditOperation.GlobalProductRetirementEnded,
            GpRetirementReason, DateTimeOffset.UtcNow);
        var store = new GlobalProductRepository(host.Database, Tenant());

        Assert.False((await new GlobalProductRepository(host.Database, Tenant(Guid.NewGuid()))
            .ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version))).Succeeded);
        var other = await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding with { OperationId = Guid.NewGuid() },
            Audit(gp, gp.Version));
        Assert.Equal(ProductIdentityWriteFaults.GlobalProductReleaseConflict, other.ErrorCode);
        // A correction's binding is not a retirement's to give back.
        Assert.False((await store.ReleaseLifecycleOperationAsync(product, gp.Version,
            binding with { Kind = GlobalProductLifecycleOperationKind.Correction }, Audit(gp, gp.Version))).Succeeded);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);

        var released = await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version));
        Assert.True(released.Succeeded, released.ErrorCode);
        var after = await GpOf(product);
        Assert.Null(after.ActiveLifecycleOperation);
        Assert.Single(after.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductRetirementEnded);
        Assert.True((await store.ReleaseLifecycleOperationAsync(product, gp.Version, binding, Audit(gp, gp.Version))).IsReplay);
    }

    // ── CT item 4: the streak / settle fields are optional — a document written before them reads with defaults ────

    [Fact]
    public async Task GPR_A_request_stored_before_the_new_fields_reads_with_defaults_and_goes_on()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        await host.Database.GetCollection<BsonDocument>(GlobalProductRetirementRequestOperationRepository.CollectionName)
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Unset("FailedAttemptCount").Unset("LastStartSentAtUtcTicksV1"));

        var old = await GpRetirementOf(key);
        Assert.Equal(0, old.FailedAttemptCount);
        Assert.Null(old.LastStartSentAtUtcTicksV1);

        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, (await GprWorkerRunsAsync(key, 4)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }

    // BL-552 item 12: a product and a request written before the new fields existed go through end to end.
    [Fact]
    public async Task GPR_A_retirement_on_a_product_and_request_written_before_the_new_fields_goes_through_end_to_end()
    {
        var product = await SeedOldProductAsync();
        var key = Guid.NewGuid();

        var started = await GpRetireAsync(product, key);
        Assert.True(started.Succeeded, started.ErrorCode);
        await UnsetRequestFieldsAsync(GlobalProductRetirementRequestOperationRepository.CollectionName, key);

        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, (await GprWorkerRunsAsync(key, 3)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }
}
