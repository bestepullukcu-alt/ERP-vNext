using System.Net;
using System.Reflection;
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
/// WP-MDM-GP-WF-ANSWERS-01 · P-2026-10-06-GP-FLOW1-FIX1 — the independent review of flow 1 (124e30acc), on this host's
/// throwaway mongod (BL-527). Each test pins one finding: the compacted-intent blocker, the lost apply record, the name
/// conflict that had no way out, the runner and its gauges, the release write's conditions, the rules that stayed green
/// when removed, the endpoint over HTTP, the background scope, the settle window, rollback safety and status codes.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private GlobalProductCorrectionWorkflowStartRequestFactory GpCorrectionFactory() =>
        new(new(_gpTemplate, null, [_gpCandidate], "CORRECTION", false, false, null, _gpIdentityTemplate, null),
            host.Clock);

    private string GpProposedName(Guid key) => $"Corrected {key:N}"[..20];

    // The audit delivery worker's compaction of one intent, as AuditIntentDeliveryRepository writes it: the delivered
    // intent leaves AuditIntents and its receipt joins AuditIntentReceipts, in one write.
    private async Task CompactAsync(Guid product, ProductAuditOperation operation)
    {
        var products = host.Database.GetCollection<GlobalProduct>("mdm_global_products");
        var current = await products.Find(x => x.Id == product).SingleAsync();
        var intent = current.AuditIntents.Single(x => x.Operation == operation);
        var receipt = new LocalAuditIntentReceipt
        {
            SourceService = intent.SourceService, IntentId = intent.IntentId, TenantId = intent.TenantId,
            IdempotencyKey = intent.IdempotencyKey, CentralAcknowledgement = "central-ack-" + intent.IntentId.ToString("N"),
            CentralIdempotencyKey = intent.IdempotencyKey, ContractVersion = "1",
            AcknowledgedAt = DateTimeOffset.UtcNow, DeliveredAt = DateTimeOffset.UtcNow, CompactedAt = DateTimeOffset.UtcNow,
            CompactReceiptReference = "receipt-" + intent.IntentId.ToString("N"), EvidenceHash = intent.EvidenceHash
        };
        var result = await products.UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.PullFilter(x => x.AuditIntents, x => x.IntentId == intent.IntentId)
                .Push(x => x.AuditIntentReceipts, receipt));
        Assert.Equal(1, result.ModifiedCount);
    }

    // A maker's request cut off after it took the product's lock and before its start was recorded.
    private async Task<(Guid Product, Guid Key)> GpInterruptedAfterTheLockAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        host.Engine.NextStartThrows = true;
        await Assert.ThrowsAnyAsync<IOException>(() => GpCorrectAsync(product, key));
        Assert.Equal(GlobalProductCorrectionCheckpoint.Prepared, (await GpCorrectionOf(key)).Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        return (product, key);
    }

    private async Task AssertTheStoreRepeatsTheAcquireAsync(Guid product, Guid key)
    {
        var gp = await GpOf(product);
        var repeat = await new GlobalProductRepository(host.Database, Tenant()).AcquireLifecycleOperationAsync(product, 0,
            new(GlobalProductLifecycleOperationKind.Correction, key, 0),
            GlobalProductCorrectionAuditIntentFactory.Create(gp, 0, key, _maker,
                ProductAuditOperation.GlobalProductCorrectionRequested, GpProposedName(key), DateTimeOffset.UtcNow));
        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.True(repeat.IsReplay);
    }

    // ── 1 (blocker): a request cut off after taking the lock, its intent compacted, goes on ────────────────────────

    [Fact]
    public async Task GPC1_The_makers_repeat_of_a_request_cut_off_after_the_lock_goes_on_after_its_intent_was_compacted()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        await CompactAsync(product, ProductAuditOperation.GlobalProductCorrectionRequested);

        await AssertTheStoreRepeatsTheAcquireAsync(product, key);
        var repeat = await GpCorrectAsync(product, key);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, repeat.Operation!.Checkpoint);
        Assert.Equal(new GlobalProductActiveLifecycleOperationBinding(GlobalProductLifecycleOperationKind.Correction, key, 0),
            (await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_The_worker_settles_a_request_cut_off_after_the_lock_whose_start_arrived_after_its_intent_was_compacted()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        await CompactAsync(product, ProductAuditOperation.GlobalProductCorrectionRequested);
        // The maker's start reached the engine after all.
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, (await host.Engine.StartAsync(_tenant,
            GpCorrectionFactory().Rehydrate(await GpCorrectionOf(key)), MakerToken())).Outcome);

        await AssertTheStoreRepeatsTheAcquireAsync(product, key);
        var settled = await GpWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, settled.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, settled.RecoveryDisposition);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    // 1 (b): the product held by THIS request is its own lock — even when the store's answer to the repeated acquire
    // is "operation active" (a store that did not recognise the earlier write), the request goes on, never "ended".
    [Fact]
    public async Task GPC1_A_product_held_by_this_very_request_is_its_own_lock_not_an_end()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();

        var repeat = await GpCorrectAsync(product, key, RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_OPERATION_ACTIVE"));

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, repeat.Operation!.Checkpoint);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    // ── 2: an applied decision whose own record was lost completes, its intent compacted ─────────────────────────

    private async Task<(Guid Product, Guid Key, Guid Approver)> GpAppliedButNotRecordedAsync()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var approver = Guid.NewGuid();
        var repository = new GlobalProductCorrectionOperationRepository(host.Database, Tenant());
        var operation = await GpCorrectionOf(key);
        var now = DateTimeOffset.UtcNow;
        var claim = await repository.TryClaimAsync(key, operation.OperationFingerprint, [operation.Checkpoint], "probe",
            now.UtcTicks, now.AddMinutes(1).UtcTicks);
        Assert.True(await repository.AdvanceAsync(claim!, new(GlobalProductCorrectionCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, DecisionKind: ProductIdentityDecisionKind.Approved,
            DecisionActorSubjectId: approver, DecisionReasonCode: "APPROVED", DecisionAtUtcTicksV1: now.UtcTicks,
            DecisionTransitionSequence: 2, DecisionTaskStatus: "Approved", DecisionInstanceStatus: "Completed",
            ReleaseLease: true)));
        var gp = await GpOf(product);
        Assert.True((await new GlobalProductRepository(host.Database, Tenant()).ApplyCorrectionDecisionAsync(product, 1,
            new(GlobalProductLifecycleOperationKind.Correction, key, 0), GpProposedName(key),
            GlobalProductNameRules.NormalizeDuplicateKey(GpProposedName(key)),
            GlobalProductCorrectionAuditIntentFactory.Create(gp, 1, key, approver,
                ProductAuditOperation.GlobalProductCorrectionApplied, GpProposedName(key), now))).Succeeded);
        await CompactAsync(product, ProductAuditOperation.GlobalProductCorrectionApplied);
        return (product, key, approver);
    }

    [Fact]
    public async Task GPC1_An_applied_correction_whose_record_was_lost_completes_after_its_intent_was_compacted()
    {
        var (product, key, approver) = await GpAppliedButNotRecordedAsync();

        // The store answers the exact repeat of the compacted write as done (its receipt), not as a conflict.
        var before = await GpOf(product);
        var storeRepeat = await new GlobalProductRepository(host.Database, Tenant()).ApplyCorrectionDecisionAsync(product, 1,
            new(GlobalProductLifecycleOperationKind.Correction, key, 0), GpProposedName(key),
            GlobalProductNameRules.NormalizeDuplicateKey(GpProposedName(key)),
            GlobalProductCorrectionAuditIntentFactory.Create(before, 1, key, approver,
                ProductAuditOperation.GlobalProductCorrectionApplied, GpProposedName(key), DateTimeOffset.UtcNow));
        Assert.True(storeRepeat.Succeeded, storeRepeat.ErrorCode);
        Assert.True(storeRepeat.IsReplay);
        var done = await GpWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, done.Checkpoint);
        var gp = await GpOf(product);
        Assert.Equal(GpProposedName(key), gp.GlobalProductName);
        Assert.Equal(2, gp.Version); // applied once
        Assert.Equal((0L, 0L), await GpCorrectionGaugesAsync());
    }

    // The processor's own guard (the GSKU alreadyApplied branch): even a store that would not recognise the compacted
    // write — answering its repeat as an idempotency conflict — does not turn an applied correction into "a person".
    [Fact]
    public async Task GPC1_An_applied_correction_is_recognised_by_the_processor_before_any_repeat_of_its_write()
    {
        var (_, key, _) = await GpAppliedButNotRecordedAsync();

        var done = await GpWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ApplyCorrectionDecisionAsync), "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT"));

        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, done.Checkpoint);
    }

    // ── 3: an approved name another product took ends the request and frees the product ──────────────────────────

    [Fact]
    public async Task GPC1_An_approved_name_another_product_took_ends_the_request_and_frees_the_product()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        var other = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == other,
            Builders<GlobalProduct>.Update.Set(x => x.GlobalProductName, GpProposedName(key))
                .Set(x => x.GlobalProductNameNormalized, GlobalProductNameRules.NormalizeDuplicateKey(GpProposedName(key))));

        var ended = await GpWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.NameConflict, ended.LastFailureCode);
        var gp = await GpOf(product);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.StartsWith("Product ", gp.GlobalProductName);
        Assert.Equal(2, gp.Version); // the release only: no extra version jump
        Assert.Single(gp.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductCorrectionEnded);
        Assert.Equal((0L, 0L), await GpCorrectionGaugesAsync());

        // The requester's repeat says why — without reading the engine (an ended request has nothing to reconcile).
        host.Engine.NextTerminalEvidenceThrows = true;
        try
        {
            var repeat = await GpCorrectAsync(product, key);
            Assert.Equal(409, repeat.StatusCode);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.NameConflict, repeat.ErrorCode);
            Assert.True(host.Engine.NextTerminalEvidenceThrows); // not consumed: the engine was not read
        }
        finally
        {
            host.Engine.NextTerminalEvidenceThrows = false;
        }
    }

    // ── 4: the runner, its cycle and both gauges ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC1_The_runner_counts_a_stuck_correction_on_both_gauges()
    {
        var (_, key) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        var stuck = await GpWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ApplyCorrectionDecisionAsync), "PRODUCT_IDENTITY_STATE_CONFLICT"));
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stuck.RecoveryDisposition);

        GlobalProductCorrectionRecoveryRunResult? result = null;
        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(async runner => result = await runner.RunCycleAsync());

        Assert.NotNull(result!.Cycle);
        Assert.True(result.Cycle!.InManualReconciliation >= 1);
        Assert.True(result.Cycle.NeedingAPerson >= 1);
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        Assert.Equal(result.Cycle.InManualReconciliation, metrics.Current(GlobalProductCorrectionRecoveryRunner.Flow));
        Assert.Equal(result.Cycle.NeedingAPerson, metrics.NeedingAPerson(GlobalProductCorrectionRecoveryRunner.Flow));
    }

    // ── 5: the release write's tenant, version and capacity conditions, each on its own ─────────────────────────

    private LocalAuditIntent GpReleaseAudit(GlobalProduct gp, Guid key, int version, Guid? tenant = null) =>
        GlobalProductCorrectionAuditIntentFactory.Create(
            tenant is { } foreign ? new GlobalProduct { Id = gp.Id, TenantId = foreign } : gp,
            version, key, _maker, ProductAuditOperation.GlobalProductCorrectionEnded,
            GpProposedName(key), DateTimeOffset.UtcNow);

    [Fact]
    public async Task GPC1_The_release_write_does_not_reach_another_tenants_product()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var gp = await GpOf(product);
        var foreign = Guid.NewGuid();

        // A well-formed request of ANOTHER tenant (its own intent), naming this tenant's product.
        var result = await new GlobalProductRepository(host.Database, Tenant(foreign)).ReleaseLifecycleOperationAsync(
            product, gp.Version, gp.ActiveLifecycleOperation!, GpReleaseAudit(gp, key, gp.Version, foreign));

        Assert.False(result.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_NOT_FOUND", result.ErrorCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_The_release_write_is_refused_when_the_product_moved_past_its_base_version()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var gp = await GpOf(product);
        // Another writer moves the product on while this request holds it (the binding stays this request's).
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == product,
            Builders<GlobalProduct>.Update.Inc(x => x.Version, 1));

        var result = await new GlobalProductRepository(host.Database, Tenant()).ReleaseLifecycleOperationAsync(
            product, gp.Version, gp.ActiveLifecycleOperation!, GpReleaseAudit(gp, key, gp.Version));

        Assert.False(result.Succeeded);
        Assert.Equal(ProductIdentityWriteFaults.GlobalProductReleaseConflict, result.ErrorCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_The_release_write_at_the_audit_cap_waits_with_the_lock_held()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        var products = host.Database.GetCollection<GlobalProduct>("mdm_global_products");
        var requested = (await GpOf(product)).AuditIntents.Single();
        await products.UpdateOneAsync(x => x.Id == product, Builders<GlobalProduct>.Update.Set(x => x.AuditIntents,
            Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(i =>
            {
                var copy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LocalAuditIntent>(requested.ToBsonDocument());
                copy.IntentId = Guid.NewGuid();
                copy.IdempotencyKey = $"filler-{i}";
                return copy;
            }).ToList()));
        var gp = await GpOf(product);

        var result = await new GlobalProductRepository(host.Database, Tenant()).ReleaseLifecycleOperationAsync(
            product, gp.Version, gp.ActiveLifecycleOperation!, GpReleaseAudit(gp, key, gp.Version));

        Assert.False(result.Succeeded);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", result.ErrorCode);
        var after = await GpOf(product);
        Assert.NotNull(after.ActiveLifecycleOperation);
        Assert.Equal(AuditIntentLimits.MaxPerAggregate, after.AuditIntents.Count);
    }

    // ── 6: rules that stayed green when removed ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC1_After_the_window_the_worker_ends_a_lock_holding_request_nothing_arrived_for()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        using var crossed4 = host.Clock.Shift(TimeSpan.FromSeconds(3));

        Assert.Contains(key, (await new GlobalProductCorrectionOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(host.Clock.GetUtcNow().UtcTicks, 100)).Operations.Select(x => x.OperationId));
        var ended = await GpWorkerRunsAsync(key, 2);

        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // A row FIX1-era code parked in AwaitingMakerReplay holding the lock (what the previous version wrote).
    private async Task GpParkAsAwaitingMakerReplayAsync(Guid key) =>
        await host.Database.GetCollection<GlobalProductCorrectionOperation>(GlobalProductCorrectionOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == key, Builders<GlobalProductCorrectionOperation>.Update
                .Set(x => x.Checkpoint, GlobalProductCorrectionCheckpoint.AwaitingMakerReplay)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                .Set(x => x.LastFailureCode, "GLOBAL_PRODUCT_CORRECTION_MAKER_REPLAY_REQUIRED"));

    [Fact]
    public async Task GPC1_A_parked_request_holding_the_lock_is_discovered_and_ended_after_the_window()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        await GpParkAsAwaitingMakerReplayAsync(key);
        using var crossed5 = host.Clock.Shift(TimeSpan.FromSeconds(3));

        var page = await new GlobalProductCorrectionOperationRepository(host.Database, Tenant())
            .DiscoverRecoverableAsync(host.Clock.GetUtcNow().UtcTicks, 100);
        Assert.Contains(key, page.Operations.Select(x => x.OperationId));
        await GpCorrection().RecoverAsync(page.Operations.Single(x => x.OperationId == key), "gp-worker", GpTwoAttempts(), default);

        var ended = await GpCorrectionOf(key);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_A_release_that_keeps_conflicting_is_stuck_on_release_conflict_for_a_person()
    {
        var (product, key) = await GpAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(host.Engine.Open(GpCorrectionObject, key.ToString("D")));

        var stuck = await GpWorkerRunsAsync(key, 5, RefusingProducts(
            nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT"));

        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StuckOnReleaseConflict, stuck.LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal((1L, 1L), await GpCorrectionGaugesAsync());
    }

    // ── 7: the endpoint, over HTTP ───────────────────────────────────────────────────────────────────────────────

    private static readonly string[] GpCorrectionPermissions =
        ["mdm.global-products.read", "mdm.global-products.request-correction"];

    private Task<HttpResponseMessage> GpCorrectionHttpAsync(Guid product, Guid key, string name) =>
        SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/correction-requests",
            new { expectedVersion = 0, globalProductName = name },
            host.Token(_tenant, _maker, GpCorrectionPermissions), key);

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        Envelope.FirstError(await Envelope.ReadAsync(response));

    private async Task<Guid> GpOperationOverHttpAsync(Guid product)
    {
        var operations = host.Database.GetCollection<GlobalProductCorrectionOperation>(
            GlobalProductCorrectionOperationRepository.CollectionName);
        return (await operations.Find(x => x.GlobalProductId == product).SingleAsync()).OperationId;
    }

    [Fact]
    public async Task GPC1_Over_http_a_closed_approval_ends_the_request_and_the_repeat_says_why()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        var name = GpProposedName(key);

        var accepted = await GpCorrectionHttpAsync(product, key, name);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        host.Engine.CloseWithoutDecision(host.Engine.Open("GlobalProductCorrection", key.ToString("D")));
        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());

        var repeat = await GpCorrectionHttpAsync(product, key, name);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.ClosedWithoutDecision, await ErrorCodeAsync(repeat));
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_Over_http_inconsistent_evidence_is_settled_by_the_requesters_repeat()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        var name = GpProposedName(key);
        Assert.Equal(HttpStatusCode.Accepted, (await GpCorrectionHttpAsync(product, key, name)).StatusCode);
        var instance = host.Engine.Open("GlobalProductCorrection", key.ToString("D"));
        instance.EvidenceInconsistent = true;
        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, (await GpCorrectionOf(key)).Checkpoint);

        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, host.Approver);
        var repeat = await GpCorrectionHttpAsync(product, key, name);

        Assert.True(repeat.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(name, (await GpOf(product)).GlobalProductName);
    }

    [Fact]
    public async Task GPC1_Over_http_a_request_stuck_on_a_record_answer_is_a_persons()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var key = Guid.NewGuid();
        var name = GpProposedName(key);
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await GpCorrectionHttpAsync(product, key, name)).StatusCode);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict;
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            for (var cycle = 0; cycle < 8 && (await GpCorrectionOf(key)).Checkpoint
                     != GlobalProductCorrectionCheckpoint.ManualReconciliationRequired; cycle++)
            {
                await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
                await Task.Delay(TimeSpan.FromTicks(Math.Max(0, ((await GpCorrectionOf(key)).NextAttemptAtUtcTicksV1 ?? 0)
                    - DateTimeOffset.UtcNow.UtcTicks)) + TimeSpan.FromMilliseconds(10));
            }
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StuckOnRecordAnswer, (await GpCorrectionOf(key)).LastFailureCode);

            var repeat = await GpCorrectionHttpAsync(product, key, name);
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal(GlobalProductCorrectionWorkflowProcessor.ManualReconciliationNeeded, await ErrorCodeAsync(repeat));
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── 8: the worker follows the legal-entity scope rule ────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC1_In_a_suspended_tenant_the_worker_does_not_apply_and_in_an_enforced_one_only_in_scope()
    {
        var (inScope, inKey) = await GpAwaitingDecisionAsync();
        var (outOfScope, outKey) = await GpAwaitingDecisionAsync();
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, inKey.ToString("D")), host.Approver);
        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, outKey.ToString("D")), host.Approver);
        var states = host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states");
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.FailClosedSuspended;
        await states.InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, inScope, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));

        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, (await GpCorrectionOf(inKey)).Checkpoint);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, (await GpCorrectionOf(outKey)).Checkpoint);

        await states.UpdateOneAsync(x => x.Id == rollout.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(x => x.Mode, ProductLegalEntityScopeRolloutMode.Enforced));
        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());

        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, (await GpCorrectionOf(inKey)).Checkpoint);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, (await GpCorrectionOf(outKey)).Checkpoint);
        Assert.NotNull((await GpOf(outOfScope)).ActiveLifecycleOperation);
    }

    // ── 9: the settle window and the parked row's way back ───────────────────────────────────────────────────────

    // A retry ceiling no longer than the window: every retry rewrites the row, so a window read from the last write
    // would never close and the lock would be held for ever.
    [Fact]
    public async Task GPC1_The_settle_window_closes_even_when_retries_are_faster_than_the_window()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        var fast = GpTwoAttempts() with { RetryDelay = TimeSpan.FromMilliseconds(50),
            MaxRetryDelay = TimeSpan.FromMilliseconds(50), QuarantineAfterFailedAttempts = 0 };
        var worker = GpCorrection();
        var until = DateTimeOffset.UtcNow.AddSeconds(4);
        while (DateTimeOffset.UtcNow < until)
        {
            var current = await GpCorrectionOf(key);
            if (current.Checkpoint == GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) break;
            await worker.RecoverAsync(current, "gp-fast-worker", fast, default);
            await Task.Delay(60);
        }

        var ended = await GpCorrectionOf(key);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, ended.LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task GPC1_A_parked_requests_repeat_whose_lookup_cannot_answer_waits_where_the_worker_looks()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();
        await GpParkAsAwaitingMakerReplayAsync(key);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        host.Engine.LookupFailureCode = "PRODUCT_WORKFLOW_TIMEOUT";
        try
        {
            var repeat = await GpCorrectAsync(product, key);

            Assert.Equal(503, repeat.StatusCode);
            var waiting = await GpCorrectionOf(key);
            Assert.Equal(GlobalProductCorrectionCheckpoint.StartOutcomeUnknown, waiting.Checkpoint);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
            Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── 10: rollback safety ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC1_A_correction_document_with_a_field_this_binary_does_not_know_still_reads()
    {
        var (_, key) = await GpAwaitingDecisionAsync();
        await host.Database.GetCollection<BsonDocument>(GlobalProductCorrectionOperationRepository.CollectionName)
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Set("FieldOfALaterVersion", "x"));

        var read = await GpCorrectionOf(key);

        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, read.Checkpoint);
    }

    // ── 11: status codes and the lock already given back ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GPC1_A_request_refused_at_the_audit_cap_answers_503()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);

        var refused = await GpCorrectAsync(product, Guid.NewGuid(), RefusingProducts(
            nameof(IGlobalProductRepository.AcquireLifecycleOperationAsync), "AUDIT_INTENT_CAPACITY_EXCEEDED"));

        Assert.Equal(503, refused.StatusCode);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", refused.ErrorCode);
    }

    [Fact]
    public async Task GPC1_A_lock_already_given_back_is_an_end_not_a_refusal()
    {
        var (_, key) = await GpAwaitingDecisionAsync();
        host.Engine.CloseWithoutDecision(host.Engine.Open(GpCorrectionObject, key.ToString("D")));

        var ended = await GpWorkerRunsAsync(key, 2, RefusingProducts(
            nameof(IGlobalProductRepository.ReleaseLifecycleOperationAsync), "GLOBAL_PRODUCT_LIFECYCLE_NOT_HELD"));

        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.ClosedWithoutDecision, ended.LastFailureCode);
    }

    // ── 12: documents written before the fields existed (in Mongo a missing field is not "empty", "false" or "0") ──

    // A Global Product written before the lifecycle fields existed: no child-admission list, no receipts, no
    // reservation count and no lock field at all — not empty ones.
    private async Task<Guid> SeedOldProductAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await host.Database.GetCollection<BsonDocument>("mdm_global_products").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(product, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset("ChildCreationAdmissions").Unset("AuditIntentReceipts")
                .Unset("IdentitySubmitReservations").Unset("ActiveLifecycleOperation"));
        var raw = await host.Database.GetCollection<BsonDocument>("mdm_global_products")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(product, GuidRepresentation.Standard))).SingleAsync();
        Assert.False(raw.Contains("ChildCreationAdmissions"));
        Assert.False(raw.Contains("ActiveLifecycleOperation"));
        return product;
    }

    private async Task UnsetRequestFieldsAsync(string collection, Guid key) =>
        await host.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(key, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset("FailedAttemptCount").Unset("LastStartSentAtUtcTicksV1"));

    [Fact]
    public async Task GPC1_A_correction_on_a_product_and_request_written_before_the_new_fields_goes_through_end_to_end()
    {
        var product = await SeedOldProductAsync();
        var key = Guid.NewGuid();

        var started = await GpCorrectAsync(product, key);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, started.Operation!.Checkpoint);
        await UnsetRequestFieldsAsync(GlobalProductCorrectionOperationRepository.CollectionName, key);

        host.Engine.Approve(host.Engine.Open(GpCorrectionObject, key.ToString("D")), Guid.NewGuid());
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, (await GpWorkerRunsAsync(key, 3)).Checkpoint);
        var gp = await GpOf(product);
        Assert.Equal(GpProposedName(key), gp.GlobalProductName);
        Assert.Null(gp.ActiveLifecycleOperation);
    }

    // ── CT sabotage G1 / G6 (124e30acc): the settle window on the requester's repeat and in the runner ───────────

    // G1: the requester's reconciling repeat that finds "nothing under this key" INSIDE the settle window keeps the lock
    // and asks again (503); after the window it ends the request and gives the product back.
    [Fact]
    public async Task GPC1_The_reconciling_repeat_keeps_the_lock_inside_the_settle_window_and_ends_the_request_after_it()
    {
        var (product, key) = await GpStuckOnARecordAnswerAsync(); // stuck within the window of its start

        // Inside the window (a 30 s window for this call, so the machine's load cannot move the test past it).
        var inside = await GpCorrection().StartInteractiveAsync(_tenant, product, 0, key, _maker, GpProposedName(key),
            MakerToken(), GpTwoAttempts() with { RetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(10),
                StartSettleWindow = TimeSpan.FromSeconds(30) }, default);
        Assert.Equal(503, inside.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, inside.ErrorCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, (await GpCorrectionOf(key)).Checkpoint);

        using var crossed6 = host.Clock.Shift(TimeSpan.FromSeconds(3));
        var after = await GpCorrectAsync(product, key);
        Assert.Equal(409, after.StatusCode);
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, after.ErrorCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }

    // G6: the runner passes the configured StartSettleSeconds (2 s in this host, not the 120 s default) to the
    // processor — inside the window the lock is kept, after it the request ends.
    [Fact]
    public async Task GPC1_The_runner_honours_the_configured_settle_window()
    {
        var (product, key) = await GpInterruptedAfterTheLockAsync();

        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartSettling, (await GpCorrectionOf(key)).LastFailureCode);
        Assert.NotNull((await GpOf(product)).ActiveLifecycleOperation);

        using var crossed7 = host.Clock.Shift(TimeSpan.FromSeconds(3));
        await host.RunAsync<GlobalProductCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(GlobalProductCorrectionWorkflowProcessor.StartNotReceived, (await GpCorrectionOf(key)).LastFailureCode);
        Assert.Null((await GpOf(product)).ActiveLifecycleOperation);
    }
}
