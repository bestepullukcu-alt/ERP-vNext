using System.Net;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GP-WF-ANSWERS-01 · P-2026-10-06-GP-FLOW2-FIX1 — the review of flow 2 (the Global Product RETIREMENT REQUEST)
/// and what flow 1 left, on this host's throwaway mongod (BL-527). Each test pins one item of the review: the runner and
/// its gauges, the endpoint over HTTP, the worker's own lock, a parked request, an old product, the start's status
/// codes, a replay after the lock was given back, a refusal that passes by itself, the previous version's conflict row,
/// the ended audit, the store's table, a compacted write's replay, the worker options and CT's Q1 / Q6.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private GlobalProductRetirementRequestWorkflowStartRequestFactory GprFactory() =>
        new(new(_gprTemplate, null, [_gpCandidate], "RETIREMENT", false, false, null, _gpIdentityTemplate, null,
            _gpTemplate, null), host.Clock);

    // ── 1: the retirement runner, its cycle and both gauges; an ended request is not counted ────────────────────

    [Fact]
    public async Task GPF1_The_retirement_runner_counts_a_stuck_request_on_both_gauges_and_not_an_ended_one()
    {
        var (_, stuckKey) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(stuckKey), Guid.NewGuid());
        var stuck = await GprWorkerRunsAsync(stuckKey, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ApplyRetirementDecisionAsync), "PRODUCT_IDENTITY_STATE_CONFLICT"));
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        var (_, endedKey) = await GprAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GprInstance(endedKey));
        var ended = await GprWorkerRunsAsync(endedKey, 2);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);

        GlobalProductRetirementRequestRecoveryRunResult? result = null;
        await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(async runner => result = await runner.RunCycleAsync());

        Assert.NotNull(result!.Cycle);
        Assert.True(result.Cycle!.InManualReconciliation >= 1);
        Assert.True(result.Cycle.NeedingAPerson >= 1);
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        Assert.Equal(result.Cycle.InManualReconciliation, metrics.Current(GlobalProductRetirementRequestRecoveryRunner.Flow));
        Assert.Equal(result.Cycle.NeedingAPerson, metrics.NeedingAPerson(GlobalProductRetirementRequestRecoveryRunner.Flow));
        // This tenant: the stuck one on both gauges, the ended one on neither.
        Assert.Equal((1L, 1L), await GprGaugesAsync());
    }

    // ── 2: the retirement request's endpoint, over HTTP ─────────────────────────────────────────────────────────

    private static readonly string[] GprPermissions =
        ["mdm.global-products.read", "mdm.global-products.request-retirement"];

    private Task<HttpResponseMessage> GprHttpAsync(Guid product, Guid key) =>
        SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/retirement-requests",
            new { expectedVersion = 0, reason = GpRetirementReason },
            host.Token(_tenant, _maker, GprPermissions), key);

    [Fact]
    public async Task GPF1_Over_http_a_retirement_request_is_accepted_and_holds_the_product()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();

        var accepted = await GprHttpAsync(product, key);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, (await GpRetirementOf(key)).Checkpoint);
        Assert.Equal(new GlobalProductActiveLifecycleOperationBinding(GlobalProductLifecycleOperationKind.Retirement, key, 0),
            (await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPF1_Over_http_a_closed_retirement_approval_ends_the_request_and_the_repeat_says_why()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await GprHttpAsync(product, key)).StatusCode);

        host.Engine.CloseWithoutDecision(GprInstance(key));
        await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());

        var repeat = await GprHttpAsync(product, key);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.ClosedWithoutDecision, await ErrorCodeAsync(repeat));
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPF1_Over_http_inconsistent_retirement_evidence_is_settled_by_the_requesters_repeat()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await GprHttpAsync(product, key)).StatusCode);
        var instance = GprInstance(key);
        instance.EvidenceInconsistent = true;
        await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, (await GpRetirementOf(key)).Checkpoint);

        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, host.Approver);
        var repeat = await GprHttpAsync(product, key);

        Assert.True(repeat.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }

    [Fact]
    public async Task GPF1_Over_http_a_retirement_stuck_on_a_record_answer_is_a_persons()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await GprHttpAsync(product, key)).StatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            for (var cycle = 0; cycle < 10 && (await GpRetirementOf(key)).Checkpoint
                     != GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired; cycle++)
            {
                // The retirement store hands the runner a request only once it is due.
                await Task.Delay(TimeSpan.FromTicks(Math.Max(0, ((await GpRetirementOf(key)).NextAttemptAtUtcTicksV1 ?? 0)
                    - host.Clock.GetUtcNow().UtcTicks)) + TimeSpan.FromMilliseconds(10));
                await host.RunAsync<GlobalProductRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
            }
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StuckOnRecordAnswer, (await GpRetirementOf(key)).LastFailureCode);

            var repeat = await GprHttpAsync(product, key);
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.ManualReconciliationNeeded, await ErrorCodeAsync(repeat));
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // The product's audit list filled to its cap (what the audit delivery has not yet carried away).
    private async Task FillTheAuditCapAsync(Guid product)
    {
        var gp = await GpOf(product);
        var template = GlobalProductRetirementRequestAuditIntentFactory.Create(gp, gp.Version, Guid.NewGuid(), _maker,
            ProductAuditOperation.GlobalProductRetirementRequested, GpRetirementReason, DateTimeOffset.UtcNow);
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Set(x => x.AuditIntents,
                Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(i =>
                {
                    var copy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LocalAuditIntent>(template.ToBsonDocument());
                    copy.IntentId = Guid.NewGuid();
                    copy.IdempotencyKey = $"filler-{i}";
                    return copy;
                }).ToList()));
    }

    // Items 2 and 8 over HTTP: the audit cap answers 503 and ends nothing — the same key goes through once it passed.
    [Fact]
    public async Task GPF1_Over_http_a_retirement_at_the_audit_cap_answers_503_and_its_same_key_goes_through_later()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        await FillTheAuditCapAsync(product);

        var refused = await GprHttpAsync(product, key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", await ErrorCodeAsync(refused));
        var waiting = await GpRetirementOf(key);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Prepared, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);

        // The audit delivery carries the list away; the maker's same key, once its retry is due, goes through.
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Set(x => x.AuditIntents, new List<LocalAuditIntent>()));
        using var due = host.Clock.Shift(TimeSpan.FromSeconds(30));
        var accepted = await GprHttpAsync(product, key);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, (await GpRetirementOf(key)).Checkpoint);
    }

    // ── 3: on the WORKER's path, the product held by this very request is its own lock ───────────────────────────

    [Fact]
    public async Task GPF1_The_correction_worker_takes_the_product_held_by_its_own_request_as_its_own_lock()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, (await host.Engine.StartAsync(_tenant,
            GpCorrectionFactory().Rehydrate(await GpCorrectionOf(key)), MakerToken())).Outcome);

        var settled = await GpWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_OPERATION_ACTIVE"));

        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, settled.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPF1_The_retirement_worker_takes_the_product_held_by_its_own_request_as_its_own_lock()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, (await host.Engine.StartAsync(_tenant,
            GprFactory().Rehydrate(await GpRetirementOf(key)), MakerToken())).Outcome);

        var settled = await GprWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_OPERATION_ACTIVE"));

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, settled.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPF1_The_retirement_worker_settles_a_request_cut_off_after_the_lock_whose_start_arrived_after_its_intent_was_compacted()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        await CompactAsync(product, ProductAuditOperation.GlobalProductRetirementRequested);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, (await host.Engine.StartAsync(_tenant,
            GprFactory().Rehydrate(await GpRetirementOf(key)), MakerToken())).Outcome);

        var settled = await GprWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, settled.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, settled.RecoveryDisposition);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── 4: a parked retirement request (AwaitingMakerReplay, the lock held) is discovered and ended ──────────────

    [Fact]
    public async Task GPF1_A_parked_retirement_request_holding_the_lock_is_discovered_and_ended_after_the_window()
    {
        var (product, key) = await GprInterruptedAfterTheLockAsync();
        await host.Database.GetCollection<GlobalProductRetirementRequestOperation>(GlobalProductRetirementRequestOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == key, Builders<GlobalProductRetirementRequestOperation>.Update
                .Set(x => x.Checkpoint, GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                .Set(x => x.LastFailureCode, "GLOBAL_PRODUCT_RETIREMENT_MAKER_REPLAY_REQUIRED"));
        using var crossed = host.Clock.Shift(TimeSpan.FromSeconds(3));

        var page = await new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(host.Clock.GetUtcNow().UtcTicks, 100);
        Assert.Contains(key, page.Operations.Select(x => x.OperationId));
        await GpRetirement().RecoverAsync(page.Operations.Single(x => x.OperationId == key), "gpr-worker", GprTwoAttempts(), default);

        var ended = await GpRetirementOf(key);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── 5: a product written before the lifecycle fields is retired directly ────────────────────────────────────

    [Fact]
    public async Task GPF1_A_product_written_before_the_lifecycle_fields_is_retired_directly()
    {
        var product = await SeedOldProductAsync();
        var gp = await GpOf(product);

        var retired = await new GlobalProductRepository(host.Database, Tenant()).RetireIdentityAsync(product, 0,
            ProductIdentityLifecycleAuditIntentFactory.CreateRetire(gp, 0, Guid.NewGuid(), _maker, "DISCONTINUED", null,
                DateTimeOffset.UtcNow));

        Assert.True(retired.Succeeded, retired.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await GpOf(product)).LifecycleStatus);
    }

    // ── 6: the start's refusal — the maker's own (401 / 403) keep their code, the service's is "resubmit" (503) ────

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN",
        ProductIdentityWorkflowMakerCodes.StartPermissionRequired, 403)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN",
        ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503)]
    public async Task GPF1_A_refused_correction_start_answers_the_makers_or_the_services_status(
        ProductIdentityWorkflowTransportOutcome outcome, string engineCode, string code, int status)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        host.Engine.NextStartFailure = (outcome, engineCode, false);

        var refused = await GpCorrectAsync(product, Guid.NewGuid());

        Assert.Equal(status, refused.StatusCode);
        Assert.Equal(code, refused.ErrorCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN",
        ProductIdentityWorkflowMakerCodes.StartPermissionRequired, 403)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN",
        ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503)]
    public async Task GPF1_A_refused_retirement_start_answers_the_makers_or_the_services_status(
        ProductIdentityWorkflowTransportOutcome outcome, string engineCode, string code, int status)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        host.Engine.NextStartFailure = (outcome, engineCode, false);

        var refused = await GpRetireAsync(product, Guid.NewGuid());

        Assert.Equal(status, refused.StatusCode);
        Assert.Equal(code, refused.ErrorCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── 7: a repeated acquire after the lock was given back is not "done" ───────────────────────────────────────

    [Fact]
    public async Task GPF1_A_repeated_acquire_after_the_lock_was_given_back_is_not_a_replay()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        var gp = await GpOf(product);
        var store = new GlobalProductRepository(host.Database, Tenant());
        Assert.True((await store.ReleaseLifecycleOperationAsync(product, gp.Version, gp.ActiveLifecycleOperation!,
            GlobalProductCorrectionAuditIntentFactory.Create(gp, gp.Version, key, _maker,
                ProductAuditOperation.GlobalProductCorrectionEnded, GpProposedName(key), DateTimeOffset.UtcNow,
                "RELEASED_ELSEWHERE"))).Succeeded);
        Assert.Equal(GlobalProductCorrectionCheckpoint.Prepared, (await GpCorrectionOf(key)).Checkpoint);

        var fresh = await GpOf(product);
        var repeat = await store.AcquireLifecycleOperationAsync(product, 0,
            new(GlobalProductLifecycleOperationKind.Correction, key, 0),
            GlobalProductCorrectionAuditIntentFactory.Create(fresh, 0, key, _maker,
                ProductAuditOperation.GlobalProductCorrectionRequested, GpProposedName(key), DateTimeOffset.UtcNow));

        Assert.False(repeat.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", repeat.ErrorCode);
        // And the maker's repeat does not go on as if it held the product.
        var makers = await GpCorrectAsync(product, key);
        Assert.False(makers.Succeeded);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── 8: a refusal of the lock that passes by itself ends nothing ─────────────────────────────────────────────

    [Fact]
    public async Task GPF1_A_correction_refused_at_the_audit_cap_waits_and_its_same_key_goes_through_later()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();

        var refused = await GpCorrectAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"));
        Assert.Equal(503, refused.StatusCode);
        var waiting = await GpCorrectionOf(key);
        Assert.Equal(GlobalProductCorrectionCheckpoint.Prepared, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);

        using var due = host.Clock.Shift(TimeSpan.FromSeconds(30));
        var accepted = await GpCorrectAsync(product, key);

        Assert.True(accepted.Succeeded, accepted.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, accepted.Operation!.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPF1_A_retirement_refused_at_the_audit_cap_waits_and_its_same_key_goes_through_later()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();

        var refused = await GpRetireAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"));
        Assert.Equal(503, refused.StatusCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, (await GpRetirementOf(key)).RecoveryDisposition);

        using var due = host.Clock.Shift(TimeSpan.FromSeconds(30));
        var accepted = await GpRetireAsync(product, key);

        Assert.True(accepted.Succeeded, accepted.ErrorCode);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.AwaitingDecision, accepted.Operation!.Checkpoint);
    }

    // ── 9: the previous version's conflict row (124e30acc and before) is ended by the requester's repeat ─────────

    // What that version wrote: the request stuck (Manual + Manual) on its conflict, and the product moved one version
    // on with its "manual reconciliation required" intent, the lock still this request's.
    private async Task GpLegacyConflictRowAsync(Guid product, Guid key, Func<Task<bool>> stick,
        Func<GlobalProduct, LocalAuditIntent> conflictIntent)
    {
        Assert.True(await stick());
        var gp = await GpOf(product);
        Assert.Equal(1, gp.Version);
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Inc(x => x.Version, 1).Push(x => x.AuditIntents, conflictIntent(gp)));
    }

    [Fact]
    public async Task GPF1_A_name_conflict_row_of_the_previous_version_is_ended_by_the_requesters_repeat()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var approver = Guid.NewGuid();
        var repository = new GlobalProductCorrectionOperationRepository(host.Database, Tenant());
        var now = host.Clock.GetUtcNow();
        await GpLegacyConflictRowAsync(product, key, async () =>
        {
            var operation = await GpCorrectionOf(key);
            // Claimed "later" than any poll the request has scheduled, so the claim is the store's to give.
            var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "legacy",
                now.AddDays(1).UtcTicks, now.AddDays(1).AddMinutes(1).UtcTicks);
            return await repository.AdvanceAsync(claim!, new(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
                ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.AddDays(1).UtcTicks,
                LastFailureCode: GlobalProductCorrectionWorkflowProcessor.NameConflict,
                DecisionKind: ProductIdentityDecisionKind.Approved, DecisionActorSubjectId: approver,
                DecisionReasonCode: "APPROVED", DecisionAtUtcTicksV1: now.UtcTicks, DecisionTransitionSequence: 2,
                DecisionTaskStatus: "Approved", DecisionInstanceStatus: "Completed", ReleaseLease: true));
        }, gp => GlobalProductCorrectionAuditIntentFactory.Create(gp, 1, key, approver,
            ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired, GpProposedName(key), now));
        Assert.Equal((1L, 1L), await GpCorrectionGaugesAsync());

        var repeat = await GpCorrectAsync(product, key);

        Assert.Equal(409, repeat.StatusCode);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.NameConflict, repeat.ErrorCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, (await GpCorrectionOf(key)).RecoveryDisposition);
        var gpAfter = await GpOf(product);
        Assert.Null(gpAfter.ActiveLifecycleOperation);
        Assert.Equal(3, gpAfter.Version);
        Assert.StartsWith("Product ", gpAfter.GlobalProductName);
        Assert.Single(gpAfter.AuditIntents, x => x.Operation == ProductAuditOperation.GlobalProductCorrectionEnded);
        Assert.Equal((0L, 0L), await GpCorrectionGaugesAsync());
    }

    [Fact]
    public async Task GPF1_A_dependant_conflict_row_of_the_previous_version_is_ended_by_the_requesters_repeat()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        var approver = Guid.NewGuid();
        var repository = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        var now = host.Clock.GetUtcNow();
        await GpLegacyConflictRowAsync(product, key, async () =>
        {
            var operation = await GpRetirementOf(key);
            var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "legacy",
                now.AddDays(1).UtcTicks, now.AddDays(1).AddMinutes(1).UtcTicks);
            return await repository.AdvanceAsync(claim!, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
                ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.AddDays(1).UtcTicks,
                LastFailureCode: "DEPENDENT_IDENTITIES_EXIST",
                DecisionKind: ProductIdentityDecisionKind.Approved, DecisionActorSubjectId: approver,
                DecisionReasonCode: "APPROVED", DecisionAtUtcTicksV1: now.UtcTicks, DecisionTransitionSequence: 2,
                DecisionTaskStatus: "Approved", DecisionInstanceStatus: "Completed", ReleaseLease: true));
        }, gp => GlobalProductRetirementRequestAuditIntentFactory.Create(gp, 1, key, approver,
            ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired, GpRetirementReason, now));
        Assert.Equal((1L, 1L), await GprGaugesAsync());

        var repeat = await GpRetireAsync(product, key);

        Assert.Equal(409, repeat.StatusCode);
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", repeat.ErrorCode);
        var gpAfter = await GpOf(product);
        Assert.Null(gpAfter.ActiveLifecycleOperation);
        Assert.Equal(3, gpAfter.Version);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gpAfter.LifecycleStatus);
        Assert.Single(gpAfter.AuditIntents, x => x.Operation == ProductAuditOperation.GlobalProductRetirementEnded);
        Assert.Equal((0L, 0L), await GprGaugesAsync());
    }

    // ── 12: an ENDED request is audited as ended, with its reason; a STUCK one is not ────────────────────────────

    [Fact]
    public async Task GPF1_An_ended_request_writes_its_own_ended_audit_with_its_reason_and_a_stuck_one_none()
    {
        var (endedProduct, endedKey) = await GprAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GprInstance(endedKey));
        await GprWorkerRunsAsync(endedKey, 2);
        var ended = await GpOf(endedProduct);
        Assert.Single(ended.AuditIntents, x => x.Operation == ProductAuditOperation.GlobalProductRetirementEnded);
        Assert.DoesNotContain(ended.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired);

        var (stuckProduct, stuckKey) = await GprAwaitingDecisionAsync();
        GprInstance(stuckKey).EvidenceInconsistent = true;
        var stuck = await GprWorkerRunsAsync(stuckKey, 2);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);
        Assert.DoesNotContain((await GpOf(stuckProduct)).AuditIntents,
            x => x.Operation is ProductAuditOperation.GlobalProductRetirementEnded
                or ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired);

        // The reason is in the facts: two ends of the same request for different reasons are different facts.
        var at = DateTimeOffset.UtcNow;
        Assert.NotEqual(
            GlobalProductRetirementRequestAuditIntentFactory.Create(ended, 1, endedKey, _maker,
                ProductAuditOperation.GlobalProductRetirementEnded, GpRetirementReason, at,
                GlobalProductRetirementRequestWorkflowProcessor.ClosedWithoutDecision).EvidenceHash,
            GlobalProductRetirementRequestAuditIntentFactory.Create(ended, 1, endedKey, _maker,
                ProductAuditOperation.GlobalProductRetirementEnded, GpRetirementReason, at,
                GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived).EvidenceHash);
    }

    // ── 13: the retirement store's table is strict — a completed request never goes back to stuck ────────────────

    [Fact]
    public async Task GPF1_A_completed_retirement_request_cannot_be_put_into_manual_reconciliation()
    {
        var (_, key) = await GprAwaitingDecisionAsync();
        host.Engine.Approve(GprInstance(key), Guid.NewGuid());
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, (await GprWorkerRunsAsync(key, 4)).Checkpoint);
        var repository = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        var operation = await GpRetirementOf(key);
        var later = host.Clock.GetUtcNow().AddDays(1);
        var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint,
            [GlobalProductRetirementRequestCheckpoint.Completed], "probe", later.UtcTicks, later.AddMinutes(1).UtcTicks);
        Assert.NotNull(claim);

        var advanced = await repository.AdvanceAsync(claim!, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, later.UtcTicks,
            LastFailureCode: "PRODUCT_IDENTITY_STATE_CONFLICT"));

        Assert.False(advanced);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed, (await GpRetirementOf(key)).Checkpoint);
    }

    // ── 14: after its intent was compacted, only the exact repeat of a write is a replay ────────────────────────

    private LocalAuditIntent GpPlainIntent(Guid product, int expectedVersion, ProductAuditOperation operation, string key) =>
        new()
        {
            IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = product, PreVersion = expectedVersion, PostVersion = expectedVersion + 1, Operation = operation,
            ActorId = _maker.ToString("D"), CorrelationId = key, CausationId = key, CommandId = key,
            Sequence = expectedVersion + 1L, TimestampUtc = DateTimeOffset.UtcNow, EvidenceHash = "facts-" + key,
            IdempotencyKey = key
        };

    private static LocalAuditIntent Changed(LocalAuditIntent intent, Action<LocalAuditIntent> change)
    {
        var copy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LocalAuditIntent>(intent.ToBsonDocument());
        change(copy);
        return copy;
    }

    private Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowBinding GpPlainBinding(Guid product, string key) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "GlobalProduct", ObjectId = product, ObjectRef = $"GP:{product:D}",
        SubmitterSubjectId = _maker, StartIdempotencyKey = key, StartRequestFingerprint = "fingerprint-" + key,
        SubmittedAtUtc = DateTimeOffset.UtcNow
    };

    [Theory]
    [InlineData("UpdateDraft")]
    [InlineData("Submit")]
    [InlineData("Approve")]
    [InlineData("Withdraw")]
    [InlineData("Retire")]
    public async Task GPF1_After_its_intent_was_compacted_only_the_exact_repeat_of_a_write_is_a_replay(string write)
    {
        var store = new GlobalProductRepository(host.Database, Tenant());
        var product = await SeedProductAsync(write == "Retire"
            ? ProductIdentityLifecycleStatus.IdentityApproved : ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid().ToString("D");
        var at = 0;
        Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowBinding? binding = null;
        if (write is "Approve" or "Withdraw")
        {
            binding = GpPlainBinding(product, "submit-" + key);
            Assert.True((await store.SubmitIdentityAsync(product, 0, binding,
                GpPlainIntent(product, 0, ProductAuditOperation.GlobalProductIdentitySubmitted, "submit-" + key))).Succeeded);
            at = 1;
        }
        var operation = write switch
        {
            "UpdateDraft" => ProductAuditOperation.GlobalProductDraftUpdated,
            "Submit" => ProductAuditOperation.GlobalProductIdentitySubmitted,
            "Approve" => ProductAuditOperation.GlobalProductIdentityApproved,
            "Withdraw" => ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn,
            _ => ProductAuditOperation.GlobalProductIdentityRetired
        };
        var submitBinding = write == "Submit" ? GpPlainBinding(product, key) : null;
        var decision = binding is null ? null : new Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowDecisionEvidence
        {
            Decision = ProductIdentityDecisionKind.Approved, WorkflowInstanceId = binding.WorkflowInstanceId,
            ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
            WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = "GlobalProduct",
            ObjectId = product, ObjectRef = binding.ObjectRef, DecisionActorSubjectId = Guid.NewGuid(),
            DecisionAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 2, TaskStatus = "Approved",
            InstanceStatus = "Completed"
        };
        var cancellation = binding is null ? null : new Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence
        {
            WorkflowInstanceId = binding.WorkflowInstanceId, ApprovalTaskId = binding.ApprovalTaskId,
            WorkflowTemplateId = binding.WorkflowTemplateId, WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId,
            ObjectType = binding.ObjectType, ObjectId = product, ObjectRef = binding.ObjectRef,
            RequesterSubjectId = binding.SubmitterSubjectId, ReasonCode = "REQUESTER_WITHDRAWAL",
            CancelledAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 3, TransitionLogId = Guid.NewGuid(),
            TaskStatus = "Cancelled", InstanceStatus = "Cancelled", WorkflowInstanceVersion = 4,
            ApprovalTaskVersion = 6, IdempotencyKey = key
        };
        Task<GlobalProductLifecycleWriteResult> Write(LocalAuditIntent intent, int expectedVersion) => write switch
        {
            "UpdateDraft" => store.UpdateDraftAsync(product, $"Renamed {key[..8]}", $"RENAMED {key[..8]}".ToUpperInvariant(),
                expectedVersion, intent),
            "Submit" => store.SubmitIdentityAsync(product, expectedVersion, submitBinding!, intent),
            "Approve" => store.ReconcileIdentityDecisionAsync(product, expectedVersion, decision!, intent),
            "Withdraw" => store.WithdrawIdentityApprovalAsync(product, expectedVersion, cancellation!, intent),
            _ => store.RetireIdentityAsync(product, expectedVersion, intent)
        };
        var original = GpPlainIntent(product, at, operation, key);
        var first = await Write(original, at);
        Assert.True(first.Succeeded, first.ErrorCode);
        await CompactAsync(product, operation);
        var current = (await GpOf(product)).Version;

        var exact = await Write(original, at);
        var otherFacts = await Write(Changed(original, x => x.EvidenceHash = "other-facts"), at);
        var later = await Write(Changed(original, x =>
        {
            x.PreVersion = current;
            x.PostVersion = current + 1;
            x.Sequence = current + 1L;
        }), current);

        Assert.True(exact.Succeeded, exact.ErrorCode);
        Assert.True(exact.IsReplay);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", otherFacts.ErrorCode);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", later.ErrorCode);
        Assert.Equal(current, (await GpOf(product)).Version);

        // Flow 3 FIX1 item 10: the aggregate found at an OLDER version than the compacted write (a restore, a rolled
        // back write) — the exact repeat is not taken as done.
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Set(x => x.Version, at));
        var olderAggregate = await Write(original, at);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", olderAggregate.ErrorCode);
    }

    // ── 16: the retirement worker's options ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GPF1_The_retirement_worker_options_have_their_defaults_bounds_and_effective_ceiling()
    {
        var defaults = new GlobalProductRetirementRequestWorkflowWorkerOptions();
        Assert.Equal((60, 10, 30, 50, 30, 900, 120), (defaults.LeaseSeconds, defaults.RetryDelaySeconds,
            defaults.PollIntervalSeconds, defaults.BatchSize, defaults.QuarantineAfterFailedAttempts,
            defaults.MaxRetryDelaySeconds, defaults.StartSettleSeconds));
        Assert.True(defaults.IsValid());

        static bool Valid(Action<GlobalProductRetirementRequestWorkflowWorkerOptions> set)
        {
            var options = new GlobalProductRetirementRequestWorkflowWorkerOptions();
            set(options);
            return options.IsValid();
        }
        Assert.True(Valid(x => x.LeaseSeconds = 10)); Assert.False(Valid(x => x.LeaseSeconds = 9));
        Assert.True(Valid(x => x.LeaseSeconds = 900)); Assert.False(Valid(x => x.LeaseSeconds = 901));
        Assert.True(Valid(x => x.RetryDelaySeconds = 1)); Assert.False(Valid(x => x.RetryDelaySeconds = 0));
        Assert.True(Valid(x => x.RetryDelaySeconds = 3600)); Assert.False(Valid(x => x.RetryDelaySeconds = 3601));
        Assert.True(Valid(x => x.PollIntervalSeconds = 1)); Assert.False(Valid(x => x.PollIntervalSeconds = 0));
        Assert.True(Valid(x => x.PollIntervalSeconds = 3600)); Assert.False(Valid(x => x.PollIntervalSeconds = 3601));
        Assert.True(Valid(x => x.BatchSize = 1)); Assert.False(Valid(x => x.BatchSize = 0));
        Assert.True(Valid(x => x.BatchSize = 100)); Assert.False(Valid(x => x.BatchSize = 101));
        Assert.True(Valid(x => x.QuarantineAfterFailedAttempts = 0)); Assert.False(Valid(x => x.QuarantineAfterFailedAttempts = -1));
        Assert.True(Valid(x => x.QuarantineAfterFailedAttempts = 1000)); Assert.False(Valid(x => x.QuarantineAfterFailedAttempts = 1001));
        Assert.True(Valid(x => x.MaxRetryDelaySeconds = 1)); Assert.False(Valid(x => x.MaxRetryDelaySeconds = 0));
        Assert.True(Valid(x => x.MaxRetryDelaySeconds = 86_400)); Assert.False(Valid(x => x.MaxRetryDelaySeconds = 86_401));
        Assert.True(Valid(x => x.StartSettleSeconds = 1)); Assert.False(Valid(x => x.StartSettleSeconds = 0));
        Assert.True(Valid(x => x.StartSettleSeconds = 3600)); Assert.False(Valid(x => x.StartSettleSeconds = 3601));

        // A ceiling below the retry delay is raised to the delay; above it, it is kept.
        Assert.Equal(10, new GlobalProductRetirementRequestWorkflowWorkerOptions
            { RetryDelaySeconds = 10, MaxRetryDelaySeconds = 5 }.EffectiveMaxRetryDelaySeconds);
        Assert.Equal(900, new GlobalProductRetirementRequestWorkflowWorkerOptions
            { RetryDelaySeconds = 10, MaxRetryDelaySeconds = 900 }.EffectiveMaxRetryDelaySeconds);
    }

    // ── CT sabotage Q1 / Q6 (flow 2): the retirement's reconcile settle window and its release conflict ──────────

    // Q1: the requester's reconciling repeat that finds "nothing under this key" INSIDE the settle window keeps the lock
    // and asks again (503); after the window it ends the request and gives the product back.
    [Fact]
    public async Task GPF1_The_retirement_reconciling_repeat_keeps_the_lock_inside_the_settle_window_and_ends_the_request_after_it()
    {
        var (product, key) = await GprStuckOnARecordAnswerAsync();

        var inside = await GpRetirement().StartInteractiveAsync(_tenant, product, 0, key, _maker, GpRetirementReason,
            MakerToken(), GprTwoAttempts() with { RetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(10),
                StartSettleWindow = TimeSpan.FromSeconds(30) }, default);
        Assert.Equal(503, inside.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, inside.ErrorCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, (await GpRetirementOf(key)).Checkpoint);

        using var crossed = host.Clock.Shift(TimeSpan.FromSeconds(3));
        var after = await GpRetireAsync(product, key);
        Assert.Equal(409, after.StatusCode);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StartNotReceived, after.ErrorCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // Q6: a release that keeps meeting a concurrent write is counted and, at the limit, stuck for a person with that
    // cause — never released blind, never retried for ever.
    [Fact]
    public async Task GPF1_A_retirement_release_that_keeps_conflicting_is_stuck_on_release_conflict_for_a_person()
    {
        var (product, key) = await GprAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(GprInstance(key));

        var stuck = await GprWorkerRunsAsync(key, 5, RefusingProducts(
            nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT"));

        Assert.Equal(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductRetirementRequestWorkflowProcessor.StuckOnReleaseConflict, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GprGaugesAsync());
    }
}

/// <summary>Flow 2 FIX1 item 10: the startup check refuses a lifecycle map that would not ignore extra elements.</summary>
public sealed class GpLifecycleClassMapStartupCheckTests
{
    private sealed class MapThatDoesNotIgnore
    {
        public Guid Id { get; set; }
    }

    [Fact]
    public void GPF1_The_startup_check_refuses_a_map_that_does_not_ignore_extra_elements()
    {
        Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Register();
        if (!MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(typeof(MapThatDoesNotIgnore)))
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<MapThatDoesNotIgnore>(map => map.AutoMap());

        Assert.Throws<InvalidOperationException>(() =>
            Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Verify<MapThatDoesNotIgnore>());
        // The three lifecycle request documents pass it.
        Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Verify<GlobalProductCorrectionOperation>();
        Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Verify<GlobalProductRetirementRequestOperation>();
        Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Verify<GlobalProductIdentityWorkflowOperation>();
    }
}
