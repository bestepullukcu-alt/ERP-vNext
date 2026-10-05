using System.Net;
using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX5 on this host's throwaway mongod (BL-527). Stuck states are produced through the WORKER
/// (the correction's processor with a retry limit of two, or the retirement request's runner with this host's limit of
/// four) — never written into the database directly.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private static GskuCorrectionExecutionConfiguration TwoAttempts() =>
        new(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1), 2, TimeSpan.FromMilliseconds(2), TimeSpan.FromSeconds(2));

    private GskuCorrectionWorkflowProcessor WorkerWith(IWorkflowVerifiedGskuReferenceResolver references,
        IGskuRepository? store = null)
    {
        var tenant = Tenant();
        return new GskuCorrectionWorkflowProcessor(
            new GskuCorrectionWorkflowOperationRepository(host.Database, tenant), store ?? new GskuRepository(host.Database, tenant),
            new ProductDefinitionRevisionRepository(host.Database, tenant), host.Engine, references,
            new GskuCorrectionWorkflowStartRequestFactory(new(null, "GSKU-CORRECTION", [host.Approver], "GSKU_CORRECTION",
                false, false, null, null, "GSKU-IDENTITY"), TimeProvider.System),
            TimeProvider.System);
    }

    // The worker's attempts on the correction until it stops changing (or the attempts run out).
    private async Task<GskuCorrectionWorkflowOperation> WorkerRunsAsync(Guid gsku, GskuCorrectionWorkflowProcessor worker, int runs)
    {
        for (var run = 0; run < runs; run++)
        {
            var current = await CorrectionOf(gsku);
            if (current.Checkpoint == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired) break;
            await worker.RecoverAsync(current, $"fix5-worker-{run}", TwoAttempts(), default);
        }
        return await CorrectionOf(gsku);
    }

    // ── Item 2: on the start / lookup paths a service fault waits — an unreadable 502 / 503 included ─────────────────

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Invalid, ProductIdentityWorkflowFaultPolicy.ResponseUnreadable, false)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE", false)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT", true)]
    public async Task F5_A_service_fault_on_the_start_lookup_waits_and_an_unusable_answer_is_stuck(
        ProductIdentityWorkflowTransportOutcome outcome, string code, bool stuck)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);

            var after = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4);

            if (stuck)
            {
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, after.Checkpoint);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, after.RecoveryDisposition);
            }
            else
            {
                // Past the limit, at the ceiling, said — but never quarantined: it heals when the service is back.
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, after.Checkpoint);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
                Assert.Equal(GskuCorrectionWorkflowProcessor.RetryLimitReached, after.LastFailureCode);
            }
        }
        finally
        {
            host.Engine.LookupFailure = null;
            host.Engine.LookupFailureCode = null;
        }
    }

    // ── Item 3: a refused write that waiting will not change is stuck at once; the capacity waits for ever ─────────

    [Theory]
    [InlineData("GSKU_CORRECTION_CONTRACT_INVALID", "stuck")]
    [InlineData("GSKU_CORRECTION_IDEMPOTENCY_CONFLICT", "stuck")]
    [InlineData("GSKU_NOT_FOUND", "stuck")]
    [InlineData("AUDIT_INTENT_CAPACITY_REACHED", "waits")]
    // FIX6 item 3 (CT sabotage W1 / W2 stayed green): the other two members of "waits" are measured too.
    [InlineData("GSKU_CORRECTION_BUSY", "waits")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE", "waits")]
    [InlineData("GSKU_CORRECTION_CONCURRENCY_CONFLICT", "counted")]
    public async Task F5_A_refused_apply_is_read_by_what_can_change_it(string code, string expected)
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        var refusing = DispatchProxy.Create<IGskuRepository, RefuseApply>();
        ((RefuseApply)(object)refusing).Inner = new GskuRepository(host.Database, Tenant());
        ((RefuseApply)(object)refusing).Code = code;

        var after = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences(), refusing), 5);

        switch (expected)
        {
            case "stuck":
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, after.Checkpoint);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, after.RecoveryDisposition);
                Assert.Equal(code, after.LastFailureCode);
                Assert.Equal(0, after.FailedAttemptCount); // at once, not after a streak of retries
                break;
            case "waits":
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.DecisionObserved, after.Checkpoint);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
                break;
            default:
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, after.Checkpoint);
                Assert.Equal(GskuCorrectionWorkflowProcessor.RetryLimitReached, after.LastFailureCode);
                break;
        }
    }

    // ── Item 8 (+ 5): a stuck CORRECTION, its requester's same command in three branches ─────────────────────────

    // Decision on record: the apply met a contract mismatch until the worker's limit; the repeat applies it.
    [Fact]
    public async Task F5_A_stuck_correction_with_its_decision_on_record_is_applied_by_the_repeat()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new MismatchedReferences()), 5);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.NotNull(stuck.DecisionKind);

        var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(4m, (await DetailAsync(approved.Id)).Quantity);
    }

    // No approval on record, but the engine has the start: the engine silent first (503, nothing changes), then back
    // (the start is recorded and the request waits for its decision, 202).
    [Fact]
    public async Task F5_A_stuck_correction_whose_start_the_engine_has_waits_and_a_silent_engine_is_a_503()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict; // Platform refuses the lookup (409)
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
            var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 5);
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
            Assert.Null(stuck.WorkflowInstanceId);

            host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
            var silent = await CorrectAsync(approved.Id, approved.Version, 4m, key);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, silent.StatusCode);
            Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, Envelope.FirstError(await Envelope.ReadAsync(silent)));
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, (await CorrectionOf(approved.Id)).Checkpoint);

            host.Engine.LookupFailure = null;
            var back = await CorrectAsync(approved.Id, approved.Version, 4m, key);
            Assert.Equal(HttpStatusCode.Accepted, back.StatusCode);
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.AwaitingDecision, (await CorrectionOf(approved.Id)).Checkpoint);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // G2: the requester's repeat with a STALE GSKU version is another request — refused (409), the stuck one untouched.
    // Another user of the same tenant with the same key: refused (409, a sentence on the screen), untouched.
    [Fact]
    public async Task F5_A_repeat_with_a_stale_version_or_by_another_user_is_refused_and_changes_nothing()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        host.Engine.Latest("GskuCorrection").EvidenceInconsistent = true;
        var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 3);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        host.Engine.Latest("GskuCorrection").EvidenceInconsistent = false;
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);

        var stale = await CorrectAsync(approved.Id, approved.Version - 1, 4m, key);
        var other = await SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 4m, packUomCode = "C62" },
            host.Token(_tenant, Guid.NewGuid(), MakerPermissions), key);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("GSKU_CORRECTION_OPERATION_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal("GSKU_CORRECTION_OPERATION_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(other)));
        var after = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, after.Checkpoint);
        Assert.Equal(stuck.Version, after.Version);
        Assert.Equal(1m, (await DetailAsync(approved.Id)).Quantity);
    }

    // ── Item 8: a stuck RETIREMENT REQUEST, the requester's repeat — open, closed, the start never arrived — and its
    // gauge (stuck counted, ended not) ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("open")]
    [InlineData("closed")]
    public async Task F5_A_stuck_retirement_request_with_its_approval_open_or_closed_is_settled_by_the_repeat(string engine)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
        var instance = host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType);
        instance.EvidenceInconsistent = true;
        await RetirementCyclesUntilAsync(approved.Id, detail => detail.LifecycleRequestState == "STUCK");
        var gauge = new GskuRetirementRequestOperationRepository(host.Database, Tenant());
        Assert.Equal(1, await gauge.CountManualReconciliationAsync());
        instance.EvidenceInconsistent = false;
        if (engine == "closed") host.Engine.CloseWithoutDecision(instance);

        var repeat = await RetireAsync(approved.Id, approved.Version, key);

        if (engine == "open")
        {
            Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
            Assert.Equal(GskuRetirementRequestCheckpoint.AwaitingDecision, (await RetirementOf(approved.Id)).Checkpoint);
            Assert.Equal(0, await gauge.CountManualReconciliationAsync());
            Assert.DoesNotContain("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions); // still held
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal("GSKU_RETIREMENT_REQUEST_CLOSED_WITHOUT_DECISION", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
            Assert.Contains("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
            Assert.Equal(0, await gauge.CountManualReconciliationAsync()); // ended: not counted
        }
    }

    [Fact]
    public async Task F5_A_stuck_retirement_request_whose_start_never_arrived_is_ended_by_the_repeat()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict; // Platform refuses the lookup (409)
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            Assert.False((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
            await RetirementCyclesUntilAsync(approved.Id, detail => detail.LifecycleRequestState == "STUCK");
            Assert.Null((await RetirementOf(approved.Id)).WorkflowInstanceId);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

        var repeat = await RetireAsync(approved.Id, approved.Version, key);

        Assert.Equal("GSKU_RETIREMENT_REQUEST_START_NOT_RECEIVED", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.Contains("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
    }

    [Fact]
    public async Task F5_A_retirement_request_repeat_with_a_stale_version_is_refused_and_changes_nothing()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
        host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType).EvidenceInconsistent = true;
        await RetirementCyclesUntilAsync(approved.Id, detail => detail.LifecycleRequestState == "STUCK");
        var before = await RetirementOf(approved.Id);

        var stale = await RetireAsync(approved.Id, approved.Version - 1, key);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("GSKU_RETIREMENT_REQUEST_OPERATION_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        Assert.Equal(before.Version, (await RetirementOf(approved.Id)).Version);
    }

    // ── Item 7: a product written before the reservation count existed (no field at all) is edited with the single
    // write — on a standalone mongod, where a transaction is not available, it would otherwise be a 503 ──────────────
    [Fact]
    public async Task F5_A_product_without_the_reservation_field_is_edited_without_a_transaction()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var standalone = host.StandaloneDatabase;
        await standalone.GetCollection<GlobalProduct>(GlobalProducts).InsertOneAsync(await ProductAsync(product));
        await standalone.GetCollection<MongoDB.Bson.BsonDocument>(GlobalProducts).UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(product, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument(nameof(GlobalProduct.IdentitySubmitReservations), "")));
        Assert.False((await standalone.GetCollection<MongoDB.Bson.BsonDocument>(GlobalProducts)
            .Find(new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(product, MongoDB.Bson.GuidRepresentation.Standard)))
            .SingleAsync()).Contains(nameof(GlobalProduct.IdentitySubmitReservations)));

        var edit = await EditProductAsync(new GlobalProductRepository(standalone, Tenant()), product, 0, "Old document edited");

        Assert.True(edit.Succeeded, edit.ErrorCode);
    }

    // A reference service whose contract this service does not meet (a version mismatch between the two): never an
    // outage, never "not selectable" — it says nothing about the approved values.
    private sealed class MismatchedReferences : IWorkflowVerifiedGskuReferenceResolver
    {
        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(Guid tenantId, string pack, string uom,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(VerifiedGskuReferenceResolveResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH"));
    }

    // Refuses every decision write with one code, as the GSKU store would; everything else reaches the real store.
    public class RefuseApply : DispatchProxy
    {
        public IGskuRepository Inner { get; set; } = null!;
        public string Code { get; set; } = "";

        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IGskuRepository.ApplyCorrectionDecisionAsync)
                ? Task.FromResult(new GskuCorrectionWriteResult(false, false, null, Code))
                : method.Invoke(Inner, args);
    }
}
