using System.Net;
using System.Reflection;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX6 on this host's throwaway mongod (BL-527). Every engine answer below is one the real client
/// can produce (Platform's codes, or the client's own); stuck states are produced through the worker.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private static GskuRetirementRequestExecutionConfiguration TwoRetirementAttempts() =>
        new(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1), 2, TimeSpan.FromMilliseconds(2), TimeSpan.FromSeconds(2));

    private GskuRetirementRequestWorkflowProcessor RetirementWorkerWith(IGskuRepository? store = null)
    {
        GskuRetirementRequestWorkflowStartRequestFactory? factory = null;
        host.RunAsync<GskuRetirementRequestWorkflowStartRequestFactory>(f => { factory = f; return Task.CompletedTask; })
            .GetAwaiter().GetResult();
        var tenant = Tenant();
        var gskus = store ?? new GskuRepository(host.Database, tenant);
        var revisions = new ProductDefinitionRevisionRepository(host.Database, tenant);
        return new GskuRetirementRequestWorkflowProcessor(
            new GskuRetirementRequestOperationRepository(host.Database, tenant), gskus, revisions, host.Engine,
            new FirstGskuIdentityRetirementProcessor(new FirstGskuIdentityRetirementOperationRepository(host.Database, tenant),
                gskus, revisions, TimeProvider.System),
            factory!, TimeProvider.System);
    }

    private async Task<GskuRetirementRequestOperation> RetirementWorkerRunsAsync(Guid gsku,
        GskuRetirementRequestWorkflowProcessor worker, int runs)
    {
        for (var run = 0; run < runs; run++)
        {
            var current = await RetirementOf(gsku);
            if (current.Checkpoint == GskuRetirementRequestCheckpoint.ManualReconciliationRequired) break;
            await worker.RecoverAsync(current, $"fix6-worker-{run}", TwoRetirementAttempts(), default);
        }
        return await RetirementOf(gsku);
    }

    // ── Item 1 (blocker): a refused LOOKUP keeps the lock; the same refusal of the START itself gives it back ─────────

    public static TheoryData<ProductIdentityWorkflowTransportOutcome, string> RefusedServiceIdentity => new()
    {
        { ProductIdentityWorkflowTransportOutcome.AuthenticationRejected, ProductIdentityWorkflowFaultPolicy.IdentityNotGranted },
        { ProductIdentityWorkflowTransportOutcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED" },
        { ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN" },
        { ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN" }
    };

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task F6_A_correction_whose_lookup_is_refused_keeps_the_gsku_locked(
        ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            var after = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 3);

            Assert.Equal(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, after.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
            Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions); // still held
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task F6_A_correction_whose_start_is_refused_gives_the_gsku_back(
        ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (outcome, code, false);

        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);

        var ended = await CorrectionOf(approved.Id);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
    }

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task F6_A_retirement_request_whose_lookup_is_refused_keeps_the_gsku_locked(
        ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        Assert.False((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            var after = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(), 3);

            Assert.Equal(GskuRetirementRequestCheckpoint.StartOutcomeUnknown, after.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
            Assert.DoesNotContain("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Theory]
    [MemberData(nameof(RefusedServiceIdentity))]
    public async Task F6_A_retirement_request_whose_start_is_refused_gives_the_gsku_back(
        ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (outcome, code, false);

        Assert.False((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);

        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, (await RetirementOf(approved.Id)).RecoveryDisposition);
        Assert.Contains("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
    }

    // ── Item 2: the requester's repeat — a service fault is 503 (ask again), an answer about the record is 409 (a person)

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT", 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH", 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Invalid, ProductIdentityWorkflowFaultPolicy.ResponseInvalid, 503)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", 503)]
    public async Task F6_The_correction_repeat_tells_a_service_fault_from_a_record_a_person_must_reconcile(
        ProductIdentityWorkflowTransportOutcome outcome, string code, int status)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        await StuckCorrectionWithoutAnInstanceAsync(approved, key);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

            Assert.Equal((HttpStatusCode)status, repeat.StatusCode);
            Assert.Equal(status == 503 ? ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit
                    : GskuCorrectionWorkflowProcessor.ManualReconciliationNeeded,
                Envelope.FirstError(await Envelope.ReadAsync(repeat)));
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, (await CorrectionOf(approved.Id)).Checkpoint);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT", 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE", 503)]
    public async Task F6_The_retirement_request_repeat_tells_a_service_fault_from_a_record_a_person_must_reconcile(
        ProductIdentityWorkflowTransportOutcome outcome, string code, int status)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            var stuck = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(), 4);
            Assert.Equal(GskuRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
            host.Engine.LookupFailure = outcome;
            host.Engine.LookupFailureCode = code;

            var repeat = await RetireAsync(approved.Id, approved.Version, key);

            Assert.Equal((HttpStatusCode)status, repeat.StatusCode);
            Assert.Equal(status == 503 ? ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit
                    : GskuRetirementRequestWorkflowProcessor.ManualReconciliationNeeded,
                Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── Item 2 (FIX5) on the Prepared path: a start cut off before it was sent; the worker's lookup meets a service fault
    // (it waits) or an answer about the record (counted, a person at the limit) — correction and retirement request.

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Invalid, ProductIdentityWorkflowFaultPolicy.ResponseUnreadable, false)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT", true)]
    public async Task F6_A_prepared_correction_without_its_maker_waits_on_a_service_fault(
        ProductIdentityWorkflowTransportOutcome outcome, string code, bool stuck)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartThrows = true;
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.Prepared, (await CorrectionOf(approved.Id)).Checkpoint);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            var after = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4);

            Assert.Equal(stuck, after.Checkpoint == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired);
            if (!stuck) Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Invalid, ProductIdentityWorkflowFaultPolicy.ResponseUnreadable, false)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT", true)]
    public async Task F6_A_prepared_retirement_request_without_its_maker_waits_on_a_service_fault(
        ProductIdentityWorkflowTransportOutcome outcome, string code, bool stuck)
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartThrows = true;
        Assert.False((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        host.Engine.LookupFailure = outcome;
        host.Engine.LookupFailureCode = code;
        try
        {
            var after = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(), 4);

            Assert.Equal(stuck, after.Checkpoint == GskuRetirementRequestCheckpoint.ManualReconciliationRequired);
            if (!stuck) Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── Item 3: refused writes of the RETIREMENT REQUEST (the reject branch) and the correction's lock release ────────

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_REACHED", "waits")]
    [InlineData("GSKU_RETIREMENT_REQUEST_BUSY", "waits")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE", "waits")]
    [InlineData("GSKU_RETIREMENT_REQUEST_CONTRACT_INVALID", "stuck")]
    public async Task F6_A_refused_retirement_rejection_is_read_by_what_can_change_it(string code, string expected)
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        host.Engine.Reject(host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType), host.Approver);
        var refusing = Refusing(nameof(IGskuRepository.RejectRetirementRequestAsync), code);

        var after = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(refusing), 5);

        if (expected == "waits")
        {
            Assert.Equal(GskuRetirementRequestCheckpoint.DecisionObserved, after.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, after.RecoveryDisposition);
        }
        else
        {
            Assert.Equal(GskuRetirementRequestCheckpoint.ManualReconciliationRequired, after.Checkpoint);
            Assert.Equal(code, after.LastFailureCode);
            // A refusal the repeat cannot settle: counted on its own (item 4 (b)).
            Assert.Equal(1, await new GskuRetirementRequestOperationRepository(host.Database, Tenant()).CountNeedingAPersonAsync());
        }
    }

    [Theory]
    [InlineData("GSKU_LIFECYCLE_RELEASE_CONTRACT_INVALID", "stuck")]
    [InlineData("GSKU_LIFECYCLE_RELEASE_IDEMPOTENCY_CONFLICT", "stuck")]
    [InlineData("AUDIT_INTENT_CAPACITY_REACHED", "waits")]
    [InlineData("GSKU_LIFECYCLE_RELEASE_CONFLICT", "released")]
    public async Task F6_A_refused_lock_release_of_a_closed_correction_is_read_by_what_can_change_it(string code, string expected)
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Latest("GskuCorrection"));
        // The release is refused ONCE — the cause gone on the next attempt (item 4 (c): only a counted retry finds out).
        var refusing = Refusing(nameof(IGskuRepository.ReleaseLifecycleOperationAsync), code, once: true);

        var after = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences(), refusing), 4);

        switch (expected)
        {
            case "stuck":
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, after.RecoveryDisposition);
                Assert.Equal(code, after.LastFailureCode);
                Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
                break;
            default:
                // waits, then the store takes it: ended, the GSKU free.
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, after.RecoveryDisposition);
                Assert.Equal("GSKU_CORRECTION_CLOSED_WITHOUT_DECISION", after.LastFailureCode);
                Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
                break;
        }
    }

    // A stuck request their repeat CAN settle (evidence that contradicted itself) is not counted as needing a person.
    [Fact]
    public async Task F6_A_request_the_repeat_can_settle_is_not_counted_as_needing_a_person()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.Latest("GskuCorrection").EvidenceInconsistent = true;
        var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 3);
        var gauge = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());

        Assert.Equal(GskuCorrectionWorkflowProcessor.EvidenceConflict, stuck.LastFailureCode);
        Assert.Equal(1, await gauge.CountManualReconciliationAsync());
        Assert.Equal(0, await gauge.CountNeedingAPersonAsync());
    }

    // Item 3: another user's repeat of a RETIREMENT REQUEST — refused (409) with the "changed elsewhere" code, untouched.
    [Fact]
    public async Task F6_Another_users_repeat_of_a_retirement_request_is_refused_and_changes_nothing()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
        var before = await RetirementOf(approved.Id);

        var other = await SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/retirement-requests",
            new { expectedGskuVersion = approved.Version, requestReason = "PRODUCT_DISCONTINUED" },
            host.Token(_tenant, Guid.NewGuid(), MakerPermissions), key);

        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal("GSKU_RETIREMENT_REQUEST_OPERATION_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(other)));
        Assert.Equal(before.Version, (await RetirementOf(approved.Id)).Version);
    }

    private async Task StuckCorrectionWithoutAnInstanceAsync((Guid Id, int Version) approved, Guid key)
    {
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Null(stuck.WorkflowInstanceId);
        host.Engine.LookupFailure = null;
    }

    private IGskuRepository Refusing(string method, string code, bool once = false)
    {
        var proxy = DispatchProxy.Create<IGskuRepository, RefuseWrite>();
        var refuse = (RefuseWrite)(object)proxy;
        refuse.Inner = new GskuRepository(host.Database, Tenant());
        refuse.Method = method;
        refuse.Code = code;
        refuse.Once = once;
        return proxy;
    }

    // Refuses one GSKU write with one code (once, or every time), as the store would; everything else is the real store.
    public class RefuseWrite : DispatchProxy
    {
        public IGskuRepository Inner { get; set; } = null!;
        public string Method { get; set; } = "";
        public string Code { get; set; } = "";
        public bool Once { get; set; }
        private int _refused;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != Method || Once && Interlocked.Exchange(ref _refused, 1) == 1)
                return method.Invoke(Inner, args);
            var resultType = method.ReturnType.GetGenericArguments()[0];
            var result = Activator.CreateInstance(resultType, false, false, null, Code)!;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result]);
        }
    }
}
