using System.Net;
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
/// WP-MDM-GSKU-ACCEPT-01 FIX7 on this host's throwaway mongod (BL-527): no state stays stuck unseen with no named way
/// out. Stuck states are produced through the worker; the requester's repeat runs the processor the application runs,
/// built from the host's own configured factory, with a store that refuses the lock release.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private GskuCorrectionWorkflowProcessor CorrectionWorkerFromHost(IGskuRepository? store = null)
    {
        GskuCorrectionWorkflowStartRequestFactory? factory = null;
        host.RunAsync<GskuCorrectionWorkflowStartRequestFactory>(f => { factory = f; return Task.CompletedTask; })
            .GetAwaiter().GetResult();
        var tenant = Tenant();
        return new GskuCorrectionWorkflowProcessor(
            new GskuCorrectionWorkflowOperationRepository(host.Database, tenant), store ?? new GskuRepository(host.Database, tenant),
            new ProductDefinitionRevisionRepository(host.Database, tenant), host.Engine, new VersionedReferences(),
            factory!, TimeProvider.System);
    }

    // A request stuck on the engine's answer about the record, with nothing on record at the engine (the lookup
    // refused until the limit). Then Platform is fixed: the lookup finds nothing — the start never arrived.
    private async Task StuckOnARecordAnswerAsync(Guid gsku, int version, Guid key, bool correction)
    {
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((correction ? await CorrectAsync(gsku, version, 4m, key) : await RetireAsync(gsku, version, key))
            .IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            if (correction)
            {
                var stuck = await WorkerRunsAsync(gsku, WorkerWith(new VersionedReferences()), 4);
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
                Assert.Equal(GskuCorrectionWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            }
            else
            {
                var stuck = await RetirementWorkerRunsAsync(gsku, RetirementWorkerWith(), 4);
                Assert.Equal(GskuRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
                Assert.Equal(GskuRetirementRequestWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            }
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        await Task.Delay(2100); // past the settle window: "nothing under this key" is trusted now
    }

    // ── Item 1: a stuck request whose lock release has to WAIT is never left stuck unseen ─────────────────────────────

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_REACHED")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE")]
    [InlineData("GSKU_LIFECYCLE_RELEASE_CONFLICT")]
    public async Task F7_A_stuck_correction_whose_release_has_to_wait_goes_back_to_the_worker_and_is_released(string code)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        await StuckOnARecordAnswerAsync(approved.Id, approved.Version, key, correction: true);

        // The requester's repeat: nothing arrived — the request would end, but the release is refused.
        await CorrectionWorkerFromHost(Refusing(nameof(IGskuRepository.ReleaseLifecycleOperationAsync), code))
            .StartInteractiveAsync(_tenant, approved.Id, approved.Version, key, _maker, 4m, "C62", MakerToken(),
                TwoAttempts(), default);

        var waiting = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, waiting.Checkpoint); // where the worker looks
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Contains(waiting.OperationId, (await new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks, 100)).Operations.Select(x => x.OperationId));

        // The worker, the store back: the lock is released.
        var ended = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 3);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal("GSKU_CORRECTION_START_NOT_RECEIVED", ended.LastFailureCode);
        Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
    }

    [Theory]
    [InlineData("AUDIT_INTENT_CAPACITY_REACHED")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE")]
    [InlineData("GSKU_LIFECYCLE_RELEASE_CONFLICT")]
    public async Task F7_A_stuck_retirement_request_whose_release_has_to_wait_goes_back_to_the_worker_and_is_released(string code)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        await StuckOnARecordAnswerAsync(approved.Id, approved.Version, key, correction: false);

        await RetirementWorkerWith(Refusing(nameof(IGskuRepository.ReleaseLifecycleOperationAsync), code))
            .StartInteractiveAsync(_tenant, approved.Id, approved.Version, key, _maker, "PRODUCT_DISCONTINUED", MakerToken(),
                TwoRetirementAttempts(), default);

        var waiting = await RetirementOf(approved.Id);
        Assert.Equal(GskuRetirementRequestCheckpoint.StartOutcomeUnknown, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Contains(waiting.OperationId, (await new GskuRetirementRequestOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks, 100)).Operations.Select(x => x.OperationId));

        var ended = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(), 3);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal("GSKU_RETIREMENT_REQUEST_START_NOT_RECEIVED", ended.LastFailureCode);
        Assert.Contains("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
    }

    // The store invariant: manual reconciliation + retryable can be written by no path.
    [Fact]
    public async Task F7_The_store_refuses_manual_reconciliation_marked_retryable_for_both_flows()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        var corrections = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        var correction = await CorrectionOf(approved.Id);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await corrections.TryClaimAsync(correction.OperationId, correction.OperationFingerprint,
            [correction.Checkpoint], "invariant", now, now + TimeSpan.TicksPerMinute);
        Assert.NotNull(claim);

        Assert.False(await corrections.AdvanceAsync(claim!, new(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, LastFailureCode: "ANY")));
        Assert.Equal(correction.Checkpoint, (await CorrectionOf(approved.Id)).Checkpoint);
        // Stuck (held for a person) and ended remain writable.
        Assert.True(await corrections.AdvanceAsync(claim!, new(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now + 2, LastFailureCode: "ANY")));

        var other = await ApprovedGskuAsync();
        Assert.True((await RetireAsync(other.Id, other.Version, Guid.NewGuid())).IsSuccessStatusCode);
        var requests = new GskuRetirementRequestOperationRepository(host.Database, Tenant());
        var request = await RetirementOf(other.Id);
        var retirementClaim = await requests.TryClaimAsync(request.OperationId, request.OperationFingerprint,
            [request.Checkpoint], "invariant", now, now + TimeSpan.TicksPerMinute);
        Assert.NotNull(retirementClaim);

        Assert.False(await requests.AdvanceAsync(retirementClaim!, new(GskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, LastFailureCode: "ANY")));
        Assert.Equal(request.Checkpoint, (await RetirementOf(other.Id)).Checkpoint);
    }

    // ── Item 2: the limit reached on an answer about the record keeps that cause and needs a person; on a service fault
    // it stays RETRY_LIMIT_REACHED, uncounted as a person's, and moves on by itself when the service is back ─────────

    [Fact]
    public async Task F7_A_limit_reached_on_a_record_answer_needs_a_person_and_the_repeat_says_so()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4);
            var gauge = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());

            Assert.Equal(GskuCorrectionWorkflowProcessor.StuckOnRecordAnswer, stuck.LastFailureCode);
            Assert.Equal(1, await gauge.CountNeedingAPersonAsync());
            var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal(GskuCorrectionWorkflowProcessor.ManualReconciliationNeeded, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task F7_A_limit_reached_on_a_service_fault_needs_no_person_and_moves_on_by_itself()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        host.Engine.LookupFailureCode = "PRODUCT_WORKFLOW_TIMEOUT";
        var gauge = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        try
        {
            var waiting = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4);

            Assert.Equal(GskuCorrectionWorkflowProcessor.RetryLimitReached, waiting.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
            Assert.Equal(0, await gauge.CountNeedingAPersonAsync());
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

        var moved = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 2);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.AwaitingDecision, moved.Checkpoint);
    }

    // ── Item 3: a lock release that KEEPS conflicting reaches the limit visibly — a person's (correction and retirement)

    [Fact]
    public async Task F7_A_correction_release_that_keeps_conflicting_is_stuck_for_a_person()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Latest("GskuCorrection"));
        var refusing = Refusing(nameof(IGskuRepository.ReleaseLifecycleOperationAsync), "GSKU_LIFECYCLE_RELEASE_CONFLICT");

        var stuck = await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences(), refusing), 5);

        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal(GskuCorrectionWorkflowProcessor.StuckOnReleaseConflict, stuck.LastFailureCode);
        Assert.Equal(1, await new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant()).CountNeedingAPersonAsync());
    }

    [Fact]
    public async Task F7_A_retirement_request_release_that_keeps_conflicting_is_stuck_for_a_person()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType));
        var refusing = Refusing(nameof(IGskuRepository.ReleaseLifecycleOperationAsync), "GSKU_LIFECYCLE_RELEASE_CONFLICT");

        var stuck = await RetirementWorkerRunsAsync(approved.Id, RetirementWorkerWith(refusing), 5);

        Assert.Equal(GskuRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.Equal(GskuRetirementRequestWorkflowProcessor.StuckOnReleaseConflict, stuck.LastFailureCode);
        Assert.Equal(1, await new GskuRetirementRequestOperationRepository(host.Database, Tenant()).CountNeedingAPersonAsync());
    }

    // Item 3: the requester's repeat inside the settle window does not take "nothing arrived" for an answer (503).
    [Fact]
    public async Task F7_The_repeat_inside_the_settle_window_keeps_the_lock_and_asks_again()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
                (await WorkerRunsAsync(approved.Id, WorkerWith(new VersionedReferences()), 4)).Checkpoint);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

        var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key); // within 2 s of the start

        Assert.Equal(HttpStatusCode.ServiceUnavailable, repeat.StatusCode);
        Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
    }
}
