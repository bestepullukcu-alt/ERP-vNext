using Diten.MdmService.Api.Services.ProductItemSkuMaster;
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
/// WP-MDM-GP-WF-ANSWERS-01 flow 2 with the rules of the flow 1 review (P-2026-10-06-GP-FLOW1-FIX1) — the Global
/// Product RETIREMENT REQUEST: a request cut off after taking the lock, its intent compacted, goes on; its own lock is
/// never an end; an applied decision whose record was lost completes; the worker's scope rule; a settle window no retry
/// refreshes; rollback safety; status codes; a lock already given back; a parked row's way back.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private async Task<(Guid Product, Guid Key)> GprInterruptedAfterTheLockAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartThrows = true;
        await Assert.ThrowsAnyAsync<IOException>(() => GpRetireAsync(product, key));
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Prepared, (await GpRetirementOf(key)).Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        return (product, key);
    }

    [Fact]
    public async Task GPR1_The_makers_repeat_of_a_request_cut_off_after_the_lock_goes_on_after_its_intent_was_compacted()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        await CompactAsync(product, ProductAuditOperation.GlobalProductRetirementRequested);

        var gp = await GpOf(product);
        var storeRepeat = await new GlobalProductRepository(host.Database, Tenant()).AcquireLifecycleOperationAsync(product, 0,
            new(GlobalProductLifecycleOperationKind.Retirement, key, 0),
            GlobalProductRetirementRequestAuditIntentFactory.Create(gp, 0, key, _maker,
                ProductAuditOperation.GlobalProductRetirementRequested, GpRetirementReason, DateTimeOffset.UtcNow));
        Assert.True(storeRepeat.IsReplay, storeRepeat.ErrorCode);
        var repeat = await GpRetireAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, repeat.Operation!.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPR1_A_product_held_by_this_very_request_is_its_own_lock_not_an_end()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();

        var repeat = await GpRetireAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_OPERATION_ACTIVE"));

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, repeat.Operation!.Checkpoint);
    }

    [Fact]
    public async Task GPR1_An_applied_retirement_is_recognised_by_the_processor_before_any_repeat_of_its_write()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        var approver = Guid.NewGuid();
        var repository = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        var operation = await GpRetirementOf(key);
        var now = DateTimeOffset.UtcNow;
        var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "probe",
            now.UtcTicks, now.AddMinutes(1).UtcTicks);
        Assert.True(await repository.AdvanceAsync(claim!, new(GlobalProductRetirementRequestCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, DecisionKind: ProductIdentityDecisionKind.Approved,
            DecisionActorSubjectId: approver, DecisionReasonCode: "APPROVED", DecisionAtUtcTicksV1: now.UtcTicks,
            DecisionTransitionSequence: 2, DecisionTaskStatus: "Approved", DecisionInstanceStatus: "Completed",
            ReleaseLease: true)));
        var gp = await GpOf(product);
        Assert.True((await new GlobalProductRepository(host.Database, Tenant()).ApplyRetirementDecisionAsync(product, 1,
            new(GlobalProductLifecycleOperationKind.Retirement, key, 0), true,
            GlobalProductRetirementRequestAuditIntentFactory.Create(gp, 1, key, approver,
                ProductAuditOperation.GlobalProductIdentityRetired, GpRetirementReason, now))).Succeeded);
        await CompactAsync(product, ProductAuditOperation.GlobalProductIdentityRetired);

        var done = await GprWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync), "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT"));

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, done.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPR1_In_a_suspended_tenant_the_worker_does_not_apply_a_decision()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), host.Approver);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.FailClosedSuspended;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);

        await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, (await GpRetirementOf(key)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GpOf(product)).LifecycleStatus);

        // The same runner applies it once the tenant is no longer suspended (it ran, it was the scope that held it).
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .DeleteOneAsync(x => x.Id == rollout.Id);
        await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, (await GpRetirementOf(key)).Checkpoint);
    }

    [Fact]
    public async Task GPR1_The_settle_window_closes_even_when_retries_are_faster_than_the_window()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        var fast = GprTwoAttempts() with { RetryDelay = TimeSpan.FromMilliseconds(50),
            MaxRetryDelay = TimeSpan.FromMilliseconds(50), QuarantineAfterFailedAttempts = 0 };
        var worker = GpRetirement();
        var until = DateTimeOffset.UtcNow.AddSeconds(4);
        while (DateTimeOffset.UtcNow < until)
        {
            var current = await GpRetirementOf(key);
            if (current.Checkpoint == GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired) break;
            await worker.RecoverAsync(current, "gpr-fast-worker", fast, default);
            await Task.Delay(60);
        }

        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived, (await GpRetirementOf(key)).LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPR1_A_parked_requests_repeat_whose_lookup_cannot_answer_waits_where_the_worker_looks()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        await host.Database.GetCollection<GlobalProductRetirementRequestOperation>(
                GlobalProductRetirementRequestOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == key, Builders<GlobalProductRetirementRequestOperation>.Update
                .Set(x => x.Checkpoint, GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                .Set(x => x.NextAttemptAtUtcTicksV1, null));
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        host.Engine.LookupFailureCode = "PRODUCT_WORKFLOW_TIMEOUT";
        try
        {
            var repeat = await GpRetireAsync(product, key);

            Assert.Equal(503, repeat.StatusCode);
            var waiting = await GpRetirementOf(key);
            Assert.Equal(GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, waiting.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    [Fact]
    public async Task GPR1_A_retirement_document_with_a_field_this_binary_does_not_know_still_reads()
    {
        var (_, key) = await GprAwaitingDecisionAsync();
        await host.Database.GetCollection<BsonDocument>(GlobalProductRetirementRequestOperationRepository.CollectionName)
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Set("FieldOfALaterVersion", "x"));

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, (await GpRetirementOf(key)).Checkpoint);
    }

    [Fact]
    public async Task GPR1_A_request_refused_at_the_audit_cap_answers_503()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);

        var refused = await GpRetireAsync(product, Guid.NewGuid(), RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"));

        Assert.Equal(503, refused.StatusCode);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", refused.ErrorCode);
    }

    [Fact]
    public async Task GPR1_A_lock_already_given_back_is_an_end_not_a_refusal()
    {
        var (_, key) = await GprAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GprInstance(key));

        var ended = await GprWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_NOT_HELD"));

        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.ClosedWithoutDecision, ended.LastFailureCode);
    }
}
