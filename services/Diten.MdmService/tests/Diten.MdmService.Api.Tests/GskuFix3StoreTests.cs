using System.Net;
using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3, on this host's throwaway mongod (BL-527): the recovery streak and the cycle report
/// (item 1), the lease of a step that throws (item 2), and Global Product's in-flight rule (item 6) — the last as
/// real races: a second client commits at the moment the repository sends its own command.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const string GlobalProducts = "mdm_global_products";

    // ── Item 1 (B13): the streak ────────────────────────────────────────────────────────────────────────────────

    // FIX4 item 1 (a) (replaces FIX3's "quarantined after the limit"): an engine that does not answer is a TRANSIENT
    // cause — 30+ worker attempts in a row are said (RETRY_LIMIT_REACHED, the gauge) but never quarantined, and when the
    // engine is back the correction heals by itself. The streak is counted in the store ($inc, FIX4 item 3).
    [Fact]
    public async Task B13_A_transient_outage_is_never_quarantined_and_heals_when_the_engine_is_back()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        var operations = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        GskuCorrectionWorkflowOperation operation;
        try
        {
            Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 5m)).IsSuccessStatusCode);
            operation = await CorrectionOf(approved.Id);
            Assert.Equal(0, operation.FailedAttemptCount); // the maker's attempt does not count (FIX4 item 2)
            var processor = CorrectionProcessor(new VersionedReferences(), crashAfterApply: false);
            var execution = new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
            for (var attempt = 0; attempt < 32; attempt++)
            {
                await processor.RecoverAsync(operation, "b13", execution, default);
                operation = (await operations.GetByOperationIdAsync(operation.OperationId))!;
                Assert.NotEqual(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
            }

            Assert.Equal(32, operation.FailedAttemptCount);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
            Assert.Equal(GskuCorrectionWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.True(await operations.CountManualReconciliationAsync() >= 1); // the gauge sees it
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

        await CorrectionProcessor(new VersionedReferences(), crashAfterApply: false).RecoverAsync(operation, "b13",
            new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)), default);

        var healed = (await operations.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.AwaitingDecision, healed.Checkpoint);
        Assert.Equal(0, healed.FailedAttemptCount);
    }

    // The streak ends with the first answer the engine gives: the count goes back to 0 and the correction carries on.
    [Fact]
    public async Task B13_The_streak_ends_when_the_engine_answers()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 6m)).IsSuccessStatusCode);
        await Task.Delay(1100);
        await host.RunAsync<GskuCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
        Assert.Equal(1, (await CorrectionOf(approved.Id)).FailedAttemptCount); // the worker's attempt only (FIX4 item 2)

        host.Engine.LookupFailure = null;
        await CorrectionCyclesUntilAsync(approved.Id, _ => CorrectionOf(approved.Id).Result.Checkpoint
            is GskuCorrectionWorkflowCheckpoint.AwaitingDecision);

        var operation = await CorrectionOf(approved.Id);
        Assert.Equal(0, operation.FailedAttemptCount);
    }

    // One line per cycle and one number per flow: what waits for a person is counted after the cycle (B13).
    [Fact]
    public async Task B13_The_cycle_reports_and_gauges_what_waits_in_manual_reconciliation()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 7m)).IsSuccessStatusCode);
        var operation = await CorrectionOf(approved.Id);
        await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == operation.OperationId,
                Builders<GskuCorrectionWorkflowOperation>.Update
                    .Set(x => x.Checkpoint, GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired)
                    .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired));

        GskuCorrectionRecoveryRunResult? result = null;
        await host.RunAsync<GskuCorrectionRecoveryRunner>(async runner => result = await runner.RunCycleAsync());

        Assert.True(result!.Cycle!.InManualReconciliation >= 1);
        Assert.Equal(result.Cycle!.InManualReconciliation,
            host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>().Current(GskuCorrectionRecoveryRunner.Flow));
    }

    // FIX4 item 1 (a) on the retirement request, through its worker (this host's limit: 4 in a row): an engine that
    // does not answer is said (RETRY_LIMIT_REACHED) and keeps being retried — never quarantined.
    [Fact]
    public async Task B13_A_retirement_request_past_the_limit_on_a_transient_cause_keeps_retrying()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            Assert.False((await PostAsync($"/api/gskus/{approved.Id:D}/retirement-requests",
                new { expectedGskuVersion = approved.Version, requestReason = "PRODUCT_DISCONTINUED" })).IsSuccessStatusCode);
            var rows = host.Database.GetCollection<GskuRetirementRequestOperation>(
                GskuRetirementRequestOperationRepository.CollectionName);
            GskuRetirementRequestOperation operation = null!;
            for (var cycle = 0; cycle < 20; cycle++)
            {
                operation = await rows.Find(x => x.GskuId == approved.Id).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();
                if (operation.LastFailureCode == GskuRetirementRequestWorkflowProcessor.RetryLimitReached) break;
                await Task.Delay(1100);
                await host.RunAsync<GskuRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
            }

            Assert.Equal(GskuRetirementRequestWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
            Assert.NotEqual(GskuRetirementRequestCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── Item 2 (B14): a step that throws gives its lease back ─────────────────────────────────────────────────────

    [Fact]
    public async Task B14_A_correction_step_that_throws_gives_its_lease_back()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 8m)).IsSuccessStatusCode);
        var operation = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        var tenant = Tenant();
        var throwing = DispatchProxy.Create<IProductIdentityWorkflowClient, ThrowingEngine>();
        ((ThrowingEngine)(object)throwing).Inner = host.Engine;
        var processor = new GskuCorrectionWorkflowProcessor(
            new GskuCorrectionWorkflowOperationRepository(host.Database, tenant), new GskuRepository(host.Database, tenant),
            new ProductDefinitionRevisionRepository(host.Database, tenant), throwing, new VersionedReferences(),
            new GskuCorrectionWorkflowStartRequestFactory(new(null, "GSKU-CORRECTION", [host.Approver], "GSKU_CORRECTION",
                false, false, null, null, "GSKU-IDENTITY"), TimeProvider.System),
            TimeProvider.System);

        await Assert.ThrowsAsync<IOException>(() => processor.RecoverAsync(operation, "b14",
            new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1)), default));

        var after = await CorrectionOf(approved.Id);
        Assert.Null(after.LeaseOwner);
        Assert.Null(after.LeaseUntilUtcTicksV1);
    }

    public class ThrowingEngine : DispatchProxy
    {
        public IProductIdentityWorkflowClient Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IProductIdentityWorkflowClient.GetTerminalEvidenceAsync)
                ? throw new IOException("the engine connection broke half-way")
                : method.Invoke(Inner, args);
    }

    private Task<GskuCorrectionWorkflowOperation> CorrectionOf(Guid gsku) =>
        host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .Find(x => x.GskuId == gsku).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();

    // ── Item 6: Global Product's in-flight rule ───────────────────────────────────────────────────────────────────

    // The measured case (FIX2): the engine has started the approval, the local "pending" is not yet written, and the
    // maker edits. The edit is refused and the product keeps the facts the approver sees (FIX2: the edit won, the
    // submit was quarantined and a live approval of the old facts stayed in the Task Center).
    [Fact]
    public async Task GP_An_edit_while_the_submit_is_at_the_engine_is_refused()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        await InsertProductOperationAsync(product, 0, GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, Guid.NewGuid());

        var response = await SendAsync(HttpMethod.Put, $"/api/global-products/{product:D}",
            new { globalProductName = "Renamed while submitted", expectedVersion = 0 },
            host.Token(_tenant, _maker, "mdm.global-products.update"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(GlobalProductRepository.SubmitInFlight, await response.Content.ReadAsStringAsync());
        Assert.StartsWith("Product ", (await ProductAsync(product)).GlobalProductName);
    }

    // Race, edit side: a submit's reservation commits between the edit's read and its write. The edit's transaction
    // conflicts, reads again, sees the submit and is refused.
    [Fact]
    public async Task GP_An_edit_racing_a_submit_reservation_is_refused()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var plain = host.Database;
        var raced = false;
        var hooked = Hooked("findAndModify", GlobalProducts, () =>
        {
            raced = true;
            using var session = plain.Client.StartSession();
            session.StartTransaction();
            plain.GetCollection<GlobalProductIdentityWorkflowOperation>(GlobalProductIdentityWorkflowOperationRepository.CollectionName)
                .InsertOne(session, ProductOperation(product, 0, GlobalProductIdentityWorkflowCheckpoint.Prepared, null));
            plain.GetCollection<GlobalProduct>(GlobalProducts).UpdateOne(session, x => x.Id == product,
                Builders<GlobalProduct>.Update.Inc(x => x.IdentitySubmitReservations, 1));
            session.CommitTransaction();
        });

        var result = await EditProductAsync(new GlobalProductRepository(hooked, Tenant()), product, 0, "Raced rename");

        Assert.True(raced);
        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductRepository.SubmitInFlight, result.ErrorCode);
        Assert.StartsWith("Product ", (await ProductAsync(product)).GlobalProductName);
    }

    // Race, submit side: an edit commits between the reservation's insert and its touch of the draft. The reservation
    // is refused and nothing is left behind — no approval can be started on facts the product no longer has.
    // Two moments: before the reservation's snapshot (its insert) — the touch's version check refuses it; inside it
    // (its touch) — the transaction's write conflict refuses it.
    [Theory]
    [InlineData("insert", GlobalProductIdentityWorkflowOperationRepository.CollectionName)]
    [InlineData("update", GlobalProducts)]
    public async Task GP_A_submit_reservation_racing_an_edit_is_refused(string command, string collection)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var plain = host.Database;
        var raced = false;
        var hooked = Hooked(command, collection, () =>
        {
            raced = true;
            plain.GetCollection<GlobalProduct>(GlobalProducts).UpdateOne(x => x.Id == product,
                Builders<GlobalProduct>.Update.Set(x => x.GlobalProductName, "Edited first").Inc(x => x.Version, 1));
        });

        var reserved = await new GlobalProductIdentityWorkflowOperationRepository(hooked, Tenant())
            .ReserveAsync(ProductOperation(product, 0, GlobalProductIdentityWorkflowCheckpoint.Prepared, null));

        Assert.True(raced);
        Assert.False(reserved.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowOperationRepository.DraftChanged, reserved.ErrorCode);
        Assert.Equal(0, await plain.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .CountDocumentsAsync(x => x.GlobalProductId == product));
    }

    // A submit the engine never received is the maker's to repeat — or to replace: the edit sets it aside.
    [Fact]
    public async Task GP_An_edit_sets_aside_a_submit_the_engine_never_received()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var operation = await InsertProductOperationAsync(product, 0, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, null);

        var result = await EditProductAsync(new GlobalProductRepository(host.Database, Tenant()), product, 0, "Replaced submit");

        Assert.True(result.Succeeded, result.ErrorCode);
        var setAside = await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.OperationId == operation).FirstAsync();
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, setAside.Checkpoint);
        Assert.Equal(GlobalProductRepository.SubmitReplacedByEdit, setAside.LastFailureCode);
    }

    // Global Product's accepted rule is kept: a submit in manual reconciliation does not lock the draft.
    [Fact]
    public async Task GP_A_submit_in_manual_reconciliation_does_not_lock_the_draft()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        await InsertProductOperationAsync(product, 0, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, Guid.NewGuid());

        var result = await EditProductAsync(new GlobalProductRepository(host.Database, Tenant()), product, 0, "Edited after quarantine");

        Assert.True(result.Succeeded, result.ErrorCode);
    }

    private async Task<GlobalProduct> ProductAsync(Guid id) =>
        await host.Database.GetCollection<GlobalProduct>(GlobalProducts).Find(x => x.Id == id).FirstAsync();

    private async Task<GlobalProductLifecycleWriteResult> EditProductAsync(GlobalProductRepository repository, Guid id,
        int version, string name)
    {
        var product = await ProductAsync(id);
        var normalized = GlobalProductNameRules.NormalizeDuplicateKey(name);
        var create = typeof(UpdateGlobalProductDraftHandler).GetMethod("CreateAuditIntent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var intent = (LocalAuditIntent)create.Invoke(null,
            [product, version, Guid.NewGuid(), _maker, name, normalized, DateTimeOffset.UtcNow])!;
        return await repository.UpdateDraftAsync(id, name, normalized, version, intent);
    }

    private async Task<Guid> InsertProductOperationAsync(Guid product, int version,
        GlobalProductIdentityWorkflowCheckpoint checkpoint, Guid? instance)
    {
        var operation = ProductOperation(product, version, checkpoint, instance);
        await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(operation);
        return operation.OperationId;
    }

    private GlobalProductIdentityWorkflowOperation ProductOperation(Guid product, int version,
        GlobalProductIdentityWorkflowCheckpoint checkpoint, Guid? instance)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new GlobalProductIdentityWorkflowOperation
        {
            Id = Guid.NewGuid(), TenantId = _tenant, OperationId = id, GlobalProductId = product,
            ExpectedProductVersion = version, MakerSubjectId = _maker, WorkflowTemplateCode = "GP-IDENTITY",
            CandidatePrincipalIds = [host.Approver], ReasonCode = "GLOBAL_PRODUCT_IDENTITY_APPROVAL",
            ObjectType = "GlobalProduct", ObjectId = product.ToString("D"), ObjectRef = $"GP/{product:N}",
            StartIdempotencyKey = $"gp-start-{id:N}", OperationFingerprint = $"fp-{id:N}", Checkpoint = checkpoint,
            WorkflowInstanceId = instance, TemporalStorageVersion = GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks, CreatedAt = now, UpdatedAt = now
        };
    }
}
