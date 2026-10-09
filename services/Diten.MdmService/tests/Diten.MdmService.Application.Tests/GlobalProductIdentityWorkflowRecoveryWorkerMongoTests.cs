using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductIdentityWorkflowRecoveryWorkerMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _operationId = Guid.NewGuid();
    private readonly Guid _makerId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _templateVersionId = Guid.NewGuid();
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();
    private readonly Guid _snapshotId = Guid.NewGuid();
    private readonly Guid _startLogId = Guid.NewGuid();
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    }

    [Fact]
    public Task Restart_worker_discovers_tenant_and_applies_terminal_decision_exactly_once() => RunWorkerAsync(null);

    // S1 — a Global Product behaviour that changed on purpose with WP-MDM-GSKU-ACCEPT-01 FIX1: in an Enforced tenant
    // the worker used to recover nothing (the decision taken in the Task Center never reached MDM). Now it applies the
    // decision of a product whose scope policy is in force, and leaves a product without one waiting.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task RUN_In_an_enforced_tenant_the_worker_applies_an_in_scope_decision_and_defers_the_rest(bool inScope) =>
        RunWorkerAsync(inScope);

    private async Task RunWorkerAsync(bool? enforcedInScope)
    {
        if (enforcedInScope is { } inScope)
        {
            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenantId, Guid.NewGuid(), Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-1));
            rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
            await _database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
                .InsertOneAsync(rollout);
            await _database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
                .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenantId, inScope ? _productId : Guid.NewGuid(),
                    Guid.NewGuid(), ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var binding = new ProductIdentityWorkflowBinding
        {
            WorkflowInstanceId = _instanceId,
            WorkflowTemplateId = _templateId,
            WorkflowTemplateVersionId = _templateVersionId,
            ApprovalTaskId = _taskId,
            AssignmentSnapshotId = _snapshotId,
            StartTransitionLogId = _startLogId,
            ObjectType = "GlobalProduct",
            ObjectId = _productId,
            ObjectRef = "GP-WORKER",
            SubmitterSubjectId = _makerId,
            StartIdempotencyKey = $"worker:{_operationId:D}",
            StartRequestFingerprint = "worker-fingerprint",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        await _database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new()
        {
            Id = _productId,
            TenantId = _tenantId,
            CanonicalCode = "GP-WORKER",
            GlobalProductName = "Worker Product",
            GlobalProductNameNormalized = "WORKER PRODUCT",
            // FIX3 item 6: seeded as the draft the submit reserves (its reservation touches it), made pending after.
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0
        });
        var operationRepository = new GlobalProductIdentityWorkflowOperationRepository(
            _database, new Tenant(_tenantId));
        var operation = new GlobalProductIdentityWorkflowOperation
        {
            Id = _operationId,
            TenantId = _tenantId,
            OperationId = _operationId,
            GlobalProductId = _productId,
            ExpectedProductVersion = 0,
            MakerSubjectId = _makerId,
            WorkflowTemplateId = _templateId,
            CandidatePrincipalIds = [_approverId],
            ReasonCode = "IDENTITY_APPROVAL",
            CommentRequired = true,
            EvidenceRequired = true,
            ObjectType = "GlobalProduct",
            ObjectId = _productId.ToString("D"),
            ObjectRef = "GP-WORKER",
            StartIdempotencyKey = binding.StartIdempotencyKey,
            OperationFingerprint = binding.StartRequestFingerprint,
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
        Assert.True((await operationRepository.ReserveAsync(operation)).Succeeded);
        await _database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(x => x.Id == _productId,
            Builders<GlobalProduct>.Update
                .Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                .Set(x => x.WorkflowBinding, binding)
                .Set(x => x.Version, 1));
        await AdvanceToAwaitingDecisionAsync(operationRepository, operation);

        using var services = BuildServices(new StaticWorkflowClient(new(
            _instanceId, _taskId, _templateId, _templateVersionId, "GlobalProduct",
            _productId.ToString("D"), "GP-WORKER", "Approve", _approverId.ToString("D"),
            "APPROVED", DateTimeOffset.UtcNow, 2, "Approved", "Completed", "corr")));
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(WorkerOptions()),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance);

        var first = await runner.RunCycleAsync();
        var second = await runner.RunCycleAsync();
        var storedOperation = await operationRepository.GetByOperationIdAsync(_operationId);
        if (enforcedInScope == false)
        {
            Assert.Equal((0, 1, 0), (first.CompletedCount, first.DeferredCount, first.FailedCount));
            Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, storedOperation!.Checkpoint);
            Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await _database
                .GetCollection<GlobalProduct>("mdm_global_products")
                .Find(item => item.TenantId == _tenantId && item.Id == _productId).SingleAsync()).LifecycleStatus);
            return;
        }

        Assert.True(first.CompletedCount == 1,
            $"completed={first.CompletedCount}; failed={first.FailedCount}; deferred={first.DeferredCount}; " +
            $"checkpoint={storedOperation?.Checkpoint}; failure={storedOperation?.LastFailureCode}; " +
            $"decision={storedOperation?.DecisionKind}; actor={storedOperation?.DecisionActorSubjectId}; " +
            $"objectType={storedOperation?.DecisionObjectType}; objectId={storedOperation?.DecisionObjectId}; " +
            $"template={storedOperation?.DecisionWorkflowTemplateId}; version={storedOperation?.DecisionWorkflowTemplateVersionId}; " +
            $"at={storedOperation?.DecisionAtUtcTicksV1}; sequence={storedOperation?.DecisionTransitionSequence}; " +
            $"task={storedOperation?.DecisionTaskStatus}; instance={storedOperation?.DecisionInstanceStatus}");
        Assert.Equal(0, first.FailedCount);
        Assert.Equal(0, second.OperationCount);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, storedOperation!.Checkpoint);
        var storedProduct = await _database.GetCollection<GlobalProduct>("mdm_global_products")
            .Find(item => item.TenantId == _tenantId && item.Id == _productId).SingleAsync();
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, storedProduct.LifecycleStatus);
        Assert.Equal(2, storedProduct.Version);
        Assert.Single(storedProduct.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.GlobalProductIdentityApproved);
    }

    public async Task DisposeAsync()
    {
        await _database.GetCollection<GlobalProduct>("mdm_global_products")
            .DeleteManyAsync(item => item.TenantId == _tenantId);
        await _database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .DeleteManyAsync(item => item.TenantId == _tenantId);
        await _database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .DeleteManyAsync(item => item.TenantId == _tenantId);
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .DeleteManyAsync(item => item.TenantId == _tenantId);
    }

    private ServiceProvider BuildServices(IProductIdentityWorkflowClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_database);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<IGlobalProductIdentityWorkflowOperationRepository>(provider =>
            new GlobalProductIdentityWorkflowOperationRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        services.AddScoped<IProductLegalEntityScopeRolloutStateRepository>(provider =>
            new ProductLegalEntityScopeRolloutStateRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        services.AddScoped<IProductLegalEntityScopePolicyRepository>(provider =>
            new ProductLegalEntityScopePolicyRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        services.AddScoped<Diten.MdmService.Application.Features.ProductLegalEntityScopes.ProductIdentityBackgroundScope>();
        services.AddSingleton<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>(
            new GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository(_database));
        services.AddScoped<IGlobalProductRepository>(provider =>
            new GlobalProductRepository(_database, provider.GetRequiredService<ITenantContext>()));
        services.AddSingleton<IProductIdentityWorkflowClient>(client);
        services.AddSingleton(new ProductIdentityWorkflowStartConfiguration(
            _templateId, null, [_approverId], "IDENTITY_APPROVAL", true, true, null));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProductIdentityWorkflowStartRequestFactory>();
        services.AddLogging();
        services.AddSingleton<IProductIdentityWorkflowTransportReadiness>(new Configured());
        services.AddGlobalProductIdentityWorkflowProcessor(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private ProductIdentityWorkflowWorkerOptions WorkerOptions() => new()
    {
        Enabled = true,
        PollIntervalSeconds = 1,
        TenantPageSize = 10,
        OperationPageSize = 10,
        LeaseSeconds = 60,
        RetryDelaySeconds = 1,
        LeaseOwner = $"test-worker-{_tenantId:N}"
    };

    private async Task AdvanceToAwaitingDecisionAsync(
        GlobalProductIdentityWorkflowOperationRepository repository,
        GlobalProductIdentityWorkflowOperation operation)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var current = GlobalProductIdentityWorkflowCheckpoint.Prepared;
        foreach (var next in new[]
                 {
                     GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                     GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
                     GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied,
                     GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                 })
        {
            var claim = await repository.TryClaimAsync(new(
                operation.OperationId, operation.OperationFingerprint, [current],
                $"seed-{(int)next}", now++, now + TimeSpan.FromMinutes(1).Ticks));
            Assert.NotNull(claim);
            Assert.True(await repository.AdvanceAsync(claim!, new(
                next,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now++,
                WorkflowInstanceId: _instanceId,
                WorkflowTemplateId: _templateId,
                WorkflowTemplateVersionId: _templateVersionId,
                ApprovalTaskId: _taskId,
                AssignmentSnapshotId: _snapshotId,
                StartTransitionLogId: _startLogId,
                WorkflowStartedAtUtcTicksV1: operation.CreatedAtUtcTicksV1,
                ReleaseLease: true)));
            current = next;
        }
    }

    private sealed class StaticWorkflowClient(ProductIdentityWorkflowTerminalEvidence evidence)
        : IProductIdentityWorkflowClient
    {
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Start is not expected.");

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Lookup is not expected.");

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(evidence));
    }

    private sealed class Configured : IProductIdentityWorkflowTransportReadiness
    {
        public string? FindConfigurationFault() => null;
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
