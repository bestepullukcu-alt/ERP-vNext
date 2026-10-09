using System.Net;
using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GP-WF-ANSWERS-01 (BL-552), flow 3 — the Global Product IDENTITY approval, over HTTP on this host's throwaway
/// mongod (BL-527), with CT's decision S2: a submit stuck in manual reconciliation holds the draft (as GSKU's does).
/// The submit and the draft edit go through the real endpoints, the worker through its real runner; the processor is
/// driven directly only where a store or a client has to misbehave.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const string GpiObject = ProductIdentityWorkflowStartRequestFactory.GlobalProductObjectType;
    private static readonly string[] GpiPermissions =
        ["mdm.global-products.read", "mdm.global-products.update", "mdm.global-products.submit"];

    private string GpiToken() => host.Token(_tenant, _maker, GpiPermissions);

    private Task<HttpResponseMessage> GpiSubmitAsync(Guid product, int version, Guid key) =>
        SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/submit", new { expectedVersion = version }, GpiToken(), key);

    private Task<HttpResponseMessage> GpiEditAsync(Guid product, int version) =>
        SendAsync(HttpMethod.Put, $"/api/global-products/{product:D}",
            new { expectedVersion = version, globalProductName = $"Edited {Guid.NewGuid():N}"[..20] }, GpiToken());

    private async Task<GlobalProductIdentityWorkflowOperation> GpiOpOf(Guid key) =>
        (await new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant()).GetByOperationIdAsync(key))!;

    private async Task<(long Manual, long Person)> GpiGaugesAsync()
    {
        var repository = new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant());
        return (await repository.CountManualReconciliationAsync(), await repository.CountNeedingAPersonAsync());
    }

    // The worker: the real runner, as often as asked, each time once the submit's next attempt is due.
    private async Task GpiRunWorkerAsync(Guid key, int cycles)
    {
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var current = await GpiOpOf(key);
            if (current.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired
                or GlobalProductIdentityWorkflowCheckpoint.Completed) return;
            var due = (current.NextAttemptAtUtcTicksV1 ?? 0) - host.Clock.GetUtcNow().UtcTicks;
            await Task.Delay(TimeSpan.FromTicks(Math.Max(0, due)) + TimeSpan.FromMilliseconds(10));
            await host.RunAsync<ProductIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }
    }

    private async Task<(Guid Product, Guid Key)> GpiAwaitingDecisionAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var submitted = await GpiSubmitAsync(product, 0, key);
        Assert.True(submitted.StatusCode == HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
        return (product, key);
    }

    private FakeWorkflowEngine.Instance GpiInstance(Guid product) => host.Engine.Open(GpiObject, product.ToString("D"));

    // ── 1: the engine's three answers ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPI_An_approval_closed_without_a_decision_returns_the_product_to_draft()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GpiInstance(product));

        await GpiRunWorkerAsync(key, 2);

        var op = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, op.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.ClosedWithoutDecision, op.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, gp.LifecycleStatus);
        Assert.Single(gp.AuditIntents, x => x.Operation == ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable);
        // The maker's repeat is told why; the draft is the maker's again (edit, then a new submit).
        var repeat = await GpiSubmitAsync(product, 0, key);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.ClosedWithoutDecision, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.True((await GpiEditAsync(product, gp.Version)).IsSuccessStatusCode);
        Assert.Equal((0L, 0L), await GpiGaugesAsync());
    }

    [Fact]
    public async Task GPI_An_escalated_approval_still_open_waits()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        host.Engine.Escalate(GpiInstance(product));

        await GpiRunWorkerAsync(key, 2);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPI_Inconsistent_evidence_is_stuck_and_the_submitters_repeat_reads_the_engine_again()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        var instance = GpiInstance(product);
        instance.EvidenceInconsistent = true;

        await GpiRunWorkerAsync(key, 2);
        var stuck = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal((1L, 0L), await GpiGaugesAsync()); // stuck, and the submitter's repeat is its way out

        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, host.Approver);
        var repeat = await GpiSubmitAsync(product, 0, key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPI_A_submit_its_own_maker_decided_returns_the_product_to_draft()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        host.Engine.Approve(GpiInstance(product), _maker);

        await GpiRunWorkerAsync(key, 2);

        Assert.Equal(GlobalProductIdentityWorkflowProcessor.SelfApprovalIgnored, (await GpiOpOf(key)).LastFailureCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GpOf(product)).LifecycleStatus);
    }

    // ── 2: the start lookup's answer is read apart from the start's; the draft is held while the engine may hold ──

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task GPI_A_refused_lookup_keeps_the_draft_held(ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpiSubmitAsync(product, 0, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            using var crossed11 = host.Clock.Shift(TimeSpan.FromSeconds(3));
            await GpiRunWorkerAsync(key, 2);

            Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, (await GpiOpOf(key)).Checkpoint);
            Assert.Equal(HttpStatusCode.Conflict, (await GpiEditAsync(product, 0)).StatusCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task GPI_Nothing_arrived_inside_the_window_holds_the_draft_and_after_it_the_draft_is_the_makers()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await GpiSubmitAsync(product, 0, key)).StatusCode);

        // Inside the window (a 60 s window for this worker, so the machine's load cannot move it past): the worker
        // that hears "nothing under this key" keeps the submit in flight and the draft held.
        var unknown = await GpiOpOf(key);
        using (host.Clock.Shift(TimeSpan.FromSeconds(5)))
            await GpiProcessor(host.Engine, settleWindow: TimeSpan.FromSeconds(60)).RecoverAsync(unknown, "gpi-window",
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        var settling = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, settling.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StartSettlingCode, settling.LastFailureCode);
        Assert.Equal(HttpStatusCode.Conflict, (await GpiEditAsync(product, 0)).StatusCode);

        using var crossed12 = host.Clock.Shift(TimeSpan.FromSeconds(3));
        await GpiRunWorkerAsync(key, 2);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, (await GpiOpOf(key)).Checkpoint);
        Assert.True((await GpiEditAsync(product, 0)).IsSuccessStatusCode);
    }

    // ── S2: a submit stuck for a person holds the draft; the submitter's same submit is its way out ──────────────

    private async Task<(Guid Product, Guid Key)> GpiStuckOnARecordAnswerAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await GpiSubmitAsync(product, 0, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            await GpiRunWorkerAsync(key, 8);
            var stuck = await GpiOpOf(key);
            Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
            Assert.Equal(GlobalProductIdentityWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            Assert.Equal((1L, 1L), await GpiGaugesAsync());
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        return (product, key);
    }

    [Fact]
    public async Task GPI_A_stuck_submit_holds_the_draft_against_an_edit_and_a_new_submit()
    {
        var (product, _) = await GpiStuckOnARecordAnswerAsync();

        var edit = await GpiEditAsync(product, 0);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(GlobalProductRepository.IdentityReconciliationRequired, Envelope.FirstError(await Envelope.ReadAsync(edit)));
        var fresh = await GpiSubmitAsync(product, 0, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, fresh.StatusCode);
        Assert.Equal(GlobalProductRepository.IdentityReconciliationRequired, Envelope.FirstError(await Envelope.ReadAsync(fresh)));
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPI_The_submitters_repeat_settles_a_stuck_submit_nothing_arrived_for_and_it_goes_on()
    {
        var (product, key) = await GpiStuckOnARecordAnswerAsync();
        using var crossed13 = host.Clock.Shift(TimeSpan.FromSeconds(3)); // past the window: "nothing under this key" is trusted

        // The engine still cannot say: a person's (409) — the draft stays held.
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            var person = await GpiSubmitAsync(product, 0, key);
            Assert.Equal(HttpStatusCode.Conflict, person.StatusCode);
            Assert.Equal(GlobalProductIdentityWorkflowProcessor.ManualReconciliationNeeded,
                Envelope.FirstError(await Envelope.ReadAsync(person)));
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

        // The engine answers "nothing": the same submit IS the resubmit — it starts, and waits for its decision.
        var settled = await GpiSubmitAsync(product, 0, key);
        Assert.Equal(HttpStatusCode.Accepted, settled.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    // The headline case of S2: the engine answered "started" and its answer could not be used — the engine certainly
    // holds the approval. The draft is held; the submitter's same submit finds the start and the submit goes on.
    public class UnusableStartProof : DispatchProxy
    {
        public IProductIdentityWorkflowClient Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = method!.Invoke(Inner, args);
            if (method.Name != nameof(IProductIdentityWorkflowClient.StartAsync)) return result;
            var started = ((Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>>)result!).GetAwaiter().GetResult();
            return Task.FromResult(started.Outcome == ProductIdentityWorkflowTransportOutcome.Success
                ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                    started.Value! with { ObjectRef = "another-object" })
                : started);
        }
    }

    private GlobalProductIdentityWorkflowProcessor GpiProcessor(IProductIdentityWorkflowClient client,
        IGlobalProductRepository? store = null, TimeSpan? settleWindow = null)
    {
        using var scope = host.Factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ProductIdentityWorkflowServiceFaultSettings>();
        if (settleWindow is { } window) settings = settings with { StartSettleWindow = window };
        return new GlobalProductIdentityWorkflowProcessor(
            new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant()),
            store ?? new GlobalProductRepository(host.Database, Tenant()), client,
            scope.ServiceProvider.GetRequiredService<ProductIdentityWorkflowStartRequestFactory>(), host.Clock,
            new ReadyTransport(), settings,
            NullLogger<GlobalProductIdentityWorkflowProcessor>.Instance);
    }

    [Fact]
    public async Task GPI_A_submit_whose_start_proof_was_unusable_holds_the_draft_and_its_repeat_goes_on()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var proxy = DispatchProxy.Create<IProductIdentityWorkflowClient, UnusableStartProof>();
        ((UnusableStartProof)(object)proxy).Inner = host.Engine;

        var first = await GpiProcessor(proxy).StartInteractiveAsync(_tenant, product, 0, key, _maker, GpiToken(),
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        Assert.False(first.Succeeded);
        var stuck = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StartProofInvalid, stuck.LastFailureCode);
        // Its submitter's same submit settles it: counted as stuck, not as a person's.
        Assert.Equal((1L, 0L), await GpiGaugesAsync());

        Assert.Equal(HttpStatusCode.Conflict, (await GpiEditAsync(product, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await GpiSubmitAsync(product, 0, Guid.NewGuid())).StatusCode);

        var repeat = await GpiSubmitAsync(product, 0, key);
        Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await GpOf(product)).LifecycleStatus);
    }

    // A submit stuck at an EARLIER version (a draft edited under the previous rule) does not hold today's draft.
    [Fact]
    public async Task GPI_A_submit_stuck_at_an_earlier_version_does_not_hold_the_draft()
    {
        var (product, key) = await GpiStuckOnARecordAnswerAsync();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Inc(x => x.Version, 1));

        Assert.True((await GpiEditAsync(product, 1)).IsSuccessStatusCode);
    }

    // ── 3: refused writes are classified; 4: the store invariant; 5: the gauges ────────────────────────────────

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_EXCEEDED")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE")]
    public async Task GPI_A_write_refusal_that_passes_by_itself_is_never_stuck(string code)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        await GpiProcessor(host.Engine, RefusingProducts(nameof(IGlobalProductRepository.SubmitIdentityAsync), code))
            .StartInteractiveAsync(_tenant, product, 0, key, _maker, GpiToken(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        var waiting = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal(code, waiting.LastFailureCode);
    }

    [Fact]
    public async Task GPI_A_concurrent_write_is_counted_and_at_the_limit_needs_a_person()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var refusing = RefusingProducts(nameof(IGlobalProductRepository.SubmitIdentityAsync), "PRODUCT_IDENTITY_CONCURRENCY_CONFLICT");
        var worker = GpiProcessor(host.Engine, refusing);
        await worker.StartInteractiveAsync(_tenant, product, 0, key, _maker, GpiToken(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1));
        for (var run = 0; run < 8; run++)
        {
            var current = await GpiOpOf(key);
            if (current.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) break;
            await Task.Delay(TimeSpan.FromTicks(Math.Max(0, (current.NextAttemptAtUtcTicksV1 ?? 0) - DateTimeOffset.UtcNow.UtcTicks)) + TimeSpan.FromMilliseconds(5));
            await worker.RecoverAsync(current, "gpi-worker", TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1));
        }

        var stuck = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
        Assert.Equal((1L, 1L), await GpiGaugesAsync());
    }

    [Fact]
    public async Task GPI_Any_other_write_refusal_is_stuck_at_once_for_a_person()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        await GpiProcessor(host.Engine, RefusingProducts(nameof(IGlobalProductRepository.SubmitIdentityAsync), "PRODUCT_IDENTITY_STATE_CONFLICT"))
            .StartInteractiveAsync(_tenant, product, 0, key, _maker, GpiToken(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        var stuck = await GpiOpOf(key);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal("PRODUCT_IDENTITY_STATE_CONFLICT", stuck.LastFailureCode);
        Assert.Equal((1L, 1L), await GpiGaugesAsync());
    }

    [Fact]
    public async Task GPI_The_store_refuses_manual_reconciliation_marked_retryable()
    {
        var (_, key) = await GpiAwaitingDecisionAsync();
        var repository = new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant());
        var op = await GpiOpOf(key);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(new(key, op.OperationFingerprint, [op.Checkpoint], "probe", now,
            now + TimeSpan.FromMinutes(1).Ticks, IgnoreRetrySchedule: true));
        Assert.NotNull(claim);

        Assert.False(await repository.AdvanceAsync(claim!, new(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, LastFailureCode: "X")));
        Assert.True(await repository.AdvanceAsync(claim!, new(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now + 1, LastFailureCode: "X", ReleaseLease: true)));
    }

    [Fact]
    public async Task GPI_The_runner_counts_a_stuck_submit_on_both_gauges()
    {
        await GpiStuckOnARecordAnswerAsync();

        await host.RunAsync<ProductIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        Assert.True(metrics.Current(ProductIdentityWorkflowRecoveryRunner.Flow) >= 1);
        Assert.True(metrics.NeedingAPerson(ProductIdentityWorkflowRecoveryRunner.Flow) >= 1);
    }

    // ── flow 1 FIX1: a compacted intent is still this write ──────────────────────────────────────────────────

    [Fact]
    public async Task GPI_A_pending_write_whose_record_was_lost_goes_on_after_its_intent_was_compacted()
    {
        var (product, key) = await GpiAwaitingDecisionAsync();
        // The local "pending" write committed, its own record was lost (the operation still at WorkflowStarted), and
        // the audit delivery compacted the intent.
        await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == key, Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted));
        await CompactAsync(product, ProductAuditOperation.GlobalProductIdentitySubmitted);

        await host.RunAsync<ProductIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
    }

    // ── 12: documents written before the fields existed; rollback safety ─────────────────────────────────────

    [Fact]
    public async Task GPI_A_submit_on_a_product_and_request_written_before_the_new_fields_goes_through_end_to_end()
    {
        var product = await SeedOldProductAsync();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft));
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await GpiSubmitAsync(product, 0, key)).StatusCode);
        await UnsetRequestFieldsAsync(GlobalProductIdentityWorkflowOperationRepository.CollectionName, key);

        host.Engine.Approve(GpiInstance(product), host.Approver);
        await GpiRunWorkerAsync(key, 3);

        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPI_An_identity_submit_document_with_a_field_this_binary_does_not_know_still_reads()
    {
        var (_, key) = await GpiAwaitingDecisionAsync();
        await host.Database.GetCollection<BsonDocument>(GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Set("FieldOfALaterVersion", "x"));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, (await GpiOpOf(key)).Checkpoint);
    }
}
