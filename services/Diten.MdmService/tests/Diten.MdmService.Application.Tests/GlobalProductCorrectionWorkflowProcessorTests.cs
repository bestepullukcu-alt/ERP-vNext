using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductCorrectionWorkflowProcessorTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _templateVersionId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProduct> _products = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _products = _database.GetCollection<GlobalProduct>("mdm_global_products");
    }

    [Fact]
    public async Task Approve_applies_proposal_once_and_terminal_replay_is_stable()
    {
        var product = await SeedAsync("Original");
        var maker = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId, product.CanonicalCode,
            approver);
        var processor = Processor(workflow);

        var started = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Corrected", "delegated-token", new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var recovered = await processor.RecoverAsync(started.Operation!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var replay = await processor.RecoverAsync(recovered.Operation!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var persisted = await new GlobalProductRepository(_database, new Tenant(_tenantId))
            .GetByIdAsync(product.Id);

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, started.Operation!.Checkpoint);
        Assert.True(recovered.Succeeded);
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal("Corrected", persisted!.GlobalProductName);
        Assert.Equal(2, persisted.Version);
        Assert.Null(persisted.ActiveLifecycleOperation);
        Assert.Single(persisted.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductCorrectionApplied);
        Assert.Equal(1, workflow.StartCalls);
    }

    [Fact]
    public async Task Maker_approval_is_quarantined_and_never_changes_product()
    {
        var product = await SeedAsync("Original Maker");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, maker);
        var processor = Processor(workflow);
        var started = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Forbidden Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        var recovered = await processor.RecoverAsync(started.Operation!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var persisted = await new GlobalProductRepository(_database, new Tenant(_tenantId))
            .GetByIdAsync(product.Id);

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.False(recovered.Succeeded, recovered.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            recovered.Operation!.Checkpoint);
        Assert.Equal("Original Maker", persisted!.GlobalProductName);
        Assert.NotNull(persisted.ActiveLifecycleOperation);
    }

    [Fact]
    public async Task Background_crash_recovery_waits_for_maker_then_exact_interactive_replay_starts_once()
    {
        var product = await SeedAsync("Original Replay");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, Guid.NewGuid());
        var tenant = new Tenant(_tenantId);
        var operationRepository = new GlobalProductCorrectionOperationRepository(_database, tenant);
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null,
            Guid.NewGuid(), null), TimeProvider.System);
        var processor = new GlobalProductCorrectionWorkflowProcessor(operationRepository,
            new GlobalProductRepository(_database, tenant), workflow, factory, TimeProvider.System);
        var plan = factory.Create(product, operationId, maker, "Recovered Change");
        Assert.True((await operationRepository.ReserveAsync(plan.Operation)).Succeeded);

        var background = await processor.RecoverAsync(plan.Operation, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var interactive = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Recovered Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.False(background.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_MAKER_REPLAY_REQUIRED", background.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingMakerReplay, background.Operation!.Checkpoint);
        Assert.True(interactive.Succeeded, interactive.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingDecision, interactive.Operation!.Checkpoint);
        Assert.Equal(1, workflow.StartCalls);
        Assert.Equal(1, workflow.StartResultCalls);
    }

    [Fact]
    public async Task Applied_product_with_unadvanced_operation_recovers_as_exact_replay_not_name_conflict()
    {
        var product = await SeedAsync("Original Crash");
        var maker = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, approver);
        var tenant = new Tenant(_tenantId);
        var operationRepository = new GlobalProductCorrectionOperationRepository(_database, tenant);
        var productRepository = new GlobalProductRepository(_database, tenant);
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null,
            Guid.NewGuid(), null), TimeProvider.System);
        var processor = new GlobalProductCorrectionWorkflowProcessor(operationRepository,
            productRepository, workflow, factory, TimeProvider.System);
        var started = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Crash Applied", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var awaiting = started.Operation!;
        var now = DateTimeOffset.UtcNow;
        var decisionClaim = await operationRepository.TryClaimAsync(operationId, awaiting.OperationFingerprint,
            [GlobalProductCorrectionCheckpoint.AwaitingDecision], "inject-crash", now.UtcTicks,
            now.AddMinutes(1).UtcTicks);
        Assert.NotNull(decisionClaim);
        Assert.True(await operationRepository.AdvanceAsync(decisionClaim!, new(
            GlobalProductCorrectionCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: ProductIdentityDecisionKind.Approved,
            DecisionActorSubjectId: approver, DecisionReasonCode: "APPROVED",
            DecisionAtUtcTicksV1: now.UtcTicks, DecisionTransitionSequence: 2,
            DecisionTaskStatus: "Approved", DecisionInstanceStatus: "Completed", ReleaseLease: true)));
        var observed = (await operationRepository.GetByOperationIdAsync(operationId))!;
        var binding = new GlobalProductActiveLifecycleOperationBinding(
            GlobalProductLifecycleOperationKind.Correction, operationId, 0);
        var current = (await productRepository.GetByIdAsync(product.Id))!;
        var audit = GlobalProductCorrectionAuditIntentFactory.Create(current, 1, operationId, approver,
            ProductAuditOperation.GlobalProductCorrectionApplied, "Crash Applied", now);
        Assert.True((await productRepository.ApplyCorrectionDecisionAsync(product.Id, 1, binding,
            "Crash Applied", "CRASH APPLIED", audit)).Succeeded);

        var recovered = await processor.RecoverAsync(observed, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.True(recovered.Succeeded, recovered.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.Equal("Crash Applied", (await productRepository.GetByIdAsync(product.Id))!.GlobalProductName);
    }

    [Fact]
    public async Task Config_drift_on_interactive_replay_is_conflict_before_workflow_start()
    {
        var product = await SeedAsync("Original Config");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var tenant = new Tenant(_tenantId);
        var repository = new GlobalProductCorrectionOperationRepository(_database, tenant);
        var initialFactory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "INITIAL", false, false, null, Guid.NewGuid(), null),
            TimeProvider.System);
        Assert.True((await repository.ReserveAsync(
            initialFactory.Create(product, operationId, maker, "Config Change").Operation)).Succeeded);
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, Guid.NewGuid());
        var driftedFactory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "DRIFTED", true, true, TimeSpan.FromMinutes(5),
            Guid.NewGuid(), null), TimeProvider.System);
        var processor = new GlobalProductCorrectionWorkflowProcessor(repository,
            new GlobalProductRepository(_database, tenant), workflow, driftedFactory, TimeProvider.System);

        var result = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Config Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.False(result.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_OPERATION_CONFLICT", result.ErrorCode);
        Assert.Equal(0, workflow.StartCalls);
    }

    [Fact]
    public async Task Cancel_terminal_evidence_is_fail_closed_in_non_cancellable_mvp()
    {
        var product = await SeedAsync("Original Cancel");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, maker, "Cancel", "Cancelled", "Cancelled");
        var processor = Processor(workflow);
        var started = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Cancelled Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        var recovered = await processor.RecoverAsync(started.Operation!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.False(recovered.Succeeded);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            recovered.Operation!.Checkpoint);
        Assert.Equal("Original Cancel", (await new GlobalProductRepository(
            _database, new Tenant(_tenantId)).GetByIdAsync(product.Id))!.GlobalProductName);
    }

    [Fact]
    public async Task Permanent_start_failure_after_not_found_is_manual_not_retry_loop()
    {
        var product = await SeedAsync("Original Permanent");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, Guid.NewGuid())
        {
            StartFailure = ProductIdentityWorkflowTransportOutcome.Forbidden
        };
        var tenant = new Tenant(_tenantId);
        var repository = new GlobalProductCorrectionOperationRepository(_database, tenant);
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null, Guid.NewGuid(), null),
            TimeProvider.System);
        var operation = factory.Create(product, operationId, maker, "Permanent Change").Operation;
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var processor = new GlobalProductCorrectionWorkflowProcessor(repository,
            new GlobalProductRepository(_database, tenant), workflow, factory, TimeProvider.System);
        var background = await processor.RecoverAsync(operation, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        Assert.Equal(GlobalProductCorrectionCheckpoint.AwaitingMakerReplay, background.Operation!.Checkpoint);

        var replay = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Permanent Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.False(replay.Succeeded);
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, replay.Operation!.Checkpoint);
        Assert.Equal("START_FORBIDDEN", replay.ErrorCode);
        Assert.Null(replay.Operation.NextAttemptAtUtcTicksV1);
    }

    [Fact]
    public async Task Conflict_audit_failure_never_advances_operation_to_manual_checkpoint()
    {
        var product = await SeedAsync("Original Audit Fence");
        var maker = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workflow = new WorkflowClient(_templateId, _templateVersionId, operationId,
            product.CanonicalCode, Guid.NewGuid());
        var processor = Processor(workflow);
        var started = await processor.StartInteractiveAsync(_tenantId, product.Id, 0, operationId, maker,
            "Audit Failure Change", "delegated-token",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)), default);
        Assert.True(started.Succeeded, started.ErrorCode);

        var admitted = await _products.Find(x => x.TenantId == _tenantId && x.Id == product.Id).SingleAsync();
        var requestedAudit = Assert.Single(admitted.AuditIntents);
        await _products.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.Id == product.Id,
            Builders<GlobalProduct>.Update.Set(x => x.AuditIntents,
                Enumerable.Repeat(requestedAudit, AuditIntentLimits.MaxPerAggregate).ToList()));
        _ = await SeedAsync("Audit Failure Change");

        var recovered = await processor.RecoverAsync(started.Operation!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)), default);
        var persistedProduct = await new GlobalProductRepository(_database, new Tenant(_tenantId))
            .GetByIdAsync(product.Id);

        Assert.False(recovered.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_MANUAL_AUDIT_NOT_PERSISTED", recovered.ErrorCode);
        Assert.Equal(GlobalProductCorrectionCheckpoint.DecisionObserved, recovered.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable,
            recovered.Operation.RecoveryDisposition);
        Assert.NotNull(recovered.Operation.NextAttemptAtUtcTicksV1);
        Assert.NotNull(persistedProduct!.ActiveLifecycleOperation);
        Assert.Equal(AuditIntentLimits.MaxPerAggregate, persistedProduct.AuditIntents.Count);
        Assert.DoesNotContain(persistedProduct.AuditIntents,
            audit => audit.Operation == ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired);
    }

    public async Task DisposeAsync() => await Task.WhenAll(
        _products.DeleteManyAsync(x => x.TenantId == _tenantId),
        _database.GetCollection<GlobalProductCorrectionOperation>(
            GlobalProductCorrectionOperationRepository.CollectionName)
            .DeleteManyAsync(x => x.TenantId == _tenantId));

    private GlobalProductCorrectionWorkflowProcessor Processor(IProductIdentityWorkflowClient workflow)
    {
        var tenant = new Tenant(_tenantId);
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            _templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null,
            Guid.NewGuid(), null), TimeProvider.System);
        return new(new GlobalProductCorrectionOperationRepository(_database, tenant),
            new GlobalProductRepository(_database, tenant), workflow, factory, TimeProvider.System);
    }

    private async Task<GlobalProduct> SeedAsync(string name)
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = $"GP-{Guid.NewGuid():N}",
            GlobalProductName = name,
            GlobalProductNameNormalized = GlobalProductNameRules.NormalizeDuplicateKey(name),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _products.InsertOneAsync(product);
        return product;
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class WorkflowClient(
        Guid templateId,
        Guid templateVersionId,
        Guid operationId,
        string objectRef,
        Guid decisionActor,
        string terminalAction = "Approve",
        string taskStatus = "Approved",
        string instanceStatus = "Completed") : IProductIdentityWorkflowClient
    {
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly Guid _taskId = Guid.NewGuid();
        public int StartCalls { get; private set; }
        public int StartResultCalls { get; private set; }
        public ProductIdentityWorkflowTransportOutcome? StartFailure { get; init; }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            if (StartFailure.HasValue)
                return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>
                    .Fail(StartFailure.Value, "START_FORBIDDEN"));
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>
                .Success(new(_instanceId, templateId, templateVersionId, _taskId, Guid.NewGuid(), Guid.NewGuid(),
                    objectRef, "Running", "Approval", "Review", DateTimeOffset.UtcNow, null, false, "corr")));
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default)
        {
            StartResultCalls++;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_START_RESULT_NOT_FOUND"));
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>>
            GetTerminalEvidenceAsync(Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
                CancellationToken cancellationToken = default) => Task.FromResult(
                ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(new(
                    _instanceId, _taskId, templateId, templateVersionId, "GlobalProductCorrection",
                    operationId.ToString("D"), objectRef, terminalAction, decisionActor.ToString("D"), "APPROVED",
                    DateTimeOffset.UtcNow, 2, taskStatus, instanceStatus, "corr")));
    }
}
