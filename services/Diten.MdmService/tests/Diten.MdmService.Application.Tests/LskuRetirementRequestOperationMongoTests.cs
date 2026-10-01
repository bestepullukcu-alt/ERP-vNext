using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class LskuRetirementRequestOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenant = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<LskuRetirementRequestOperation> _collection = null!;
    private IMongoCollection<Lsku> _lskus = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<LskuRetirementRequestOperation>(
            LskuRetirementRequestOperationRepository.CollectionName);
        _lskus = _database.GetCollection<Lsku>("mdm_lskus");
    }

    [Fact]
    public async Task One_active_operation_exact_replay_and_tenant_isolation_are_enforced()
    {
        var repository = new LskuRetirementRequestOperationRepository(_database, new Tenant(_tenant));
        var operation = Candidate();
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Copy(operation));
        var competing = Candidate(operation.LskuId);
        var conflict = await repository.ReserveAsync(competing);

        Assert.True(first.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(conflict.Succeeded);
        Assert.Null(await new LskuRetirementRequestOperationRepository(_database,
            new Tenant(Guid.NewGuid())).GetByOperationIdAsync(operation.OperationId));
    }

    [Fact]
    public async Task Completed_operation_leaves_recovery_and_releases_active_operation_fence()
    {
        var repository = new LskuRetirementRequestOperationRepository(_database, new Tenant(_tenant));
        var operation = Candidate();
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var current = LskuRetirementRequestCheckpoint.Prepared;
        foreach (var next in new[] { LskuRetirementRequestCheckpoint.WorkflowStarted,
            LskuRetirementRequestCheckpoint.AwaitingDecision, LskuRetirementRequestCheckpoint.DecisionObserved,
            LskuRetirementRequestCheckpoint.DecisionApplied, LskuRetirementRequestCheckpoint.Completed })
        {
            var now = DateTimeOffset.UtcNow.UtcTicks;
            var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [current], $"worker-{next}", now, now + TimeSpan.TicksPerMinute);
            Assert.NotNull(claim);
            Assert.True(await repository.AdvanceAsync(claim!, new(next,
                ProductIdentityWorkflowRecoveryDisposition.None, now, ReleaseLease: true)));
            current = next;
        }
        Assert.Empty((await repository.DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations);
        Assert.True((await repository.ReserveAsync(Candidate(operation.LskuId))).Succeeded);
    }

    [Fact]
    public async Task Tenant_partition_discovery_does_not_require_resolved_tenant()
    {
        Assert.True((await new LskuRetirementRequestOperationRepository(_database, new Tenant(_tenant))
            .ReserveAsync(Candidate())).Succeeded);
        var page = await new LskuRetirementRequestOperationRepository(_database, new UnresolvedTenant())
            .DiscoverTenantPartitionsAsync(null, 100);
        Assert.Contains(_tenant, page.TenantIds);
    }

    [Theory]
    [InlineData(true, ProductIdentityLifecycleStatus.Retired)]
    [InlineData(false, ProductIdentityLifecycleStatus.IdentityApproved)]
    public async Task Admission_and_terminal_decision_are_atomic_and_preserve_immutable_identity(
        bool approved, ProductIdentityLifecycleStatus expectedStatus)
    {
        var id = Guid.NewGuid(); var operationId = Guid.NewGuid(); var actor = Guid.NewGuid();
        var originalGsku = Guid.NewGuid(); var originalMarket = approved ? "TR" : "DE";
        var lsku = new Lsku { Id = id, TenantId = _tenant, CanonicalCode = $"LS-{id:N}",
            GskuId = originalGsku, MarketCode = originalMarket, CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("D"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            Version = 2, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await _lskus.InsertOneAsync(lsku);
        var repository = new LskuRepository(_database, new Tenant(_tenant));
        var binding = new Diten.MdmService.Domain.ValueObjects.LskuActiveLifecycleOperationBinding(
            Diten.MdmService.Domain.ValueObjects.LskuLifecycleOperationKind.Retirement, operationId, 2);
        var requested = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, 2,
            operationId, actor, ProductAuditOperation.LskuRetirementRequested, "Obsolete", DateTimeOffset.UtcNow);

        var admitted = await repository.AcquireLifecycleOperationAsync(id, 2, binding, requested);
        var current = await repository.GetByIdAsync(id);
        var terminal = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(current!, 3,
            operationId, Guid.NewGuid(), approved ? ProductAuditOperation.LskuIdentityRetired
                : ProductAuditOperation.LskuRetirementRejected, "Obsolete", DateTimeOffset.UtcNow);
        var decided = await repository.ApplyRetirementDecisionAsync(id, 3, binding, approved, terminal);
        var replay = await repository.ApplyRetirementDecisionAsync(id, 3, binding, approved, terminal);

        Assert.True(admitted.Succeeded);
        Assert.True(decided.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(expectedStatus, decided.Lsku!.LifecycleStatus);
        Assert.Null(decided.Lsku.ActiveLifecycleOperation);
        Assert.Equal(originalGsku, decided.Lsku.GskuId);
        Assert.Equal(originalMarket, decided.Lsku.MarketCode);
        Assert.Single(decided.Lsku.AuditIntents, x => x.Operation == ProductAuditOperation.LskuRetirementRequested);
        Assert.Single(decided.Lsku.AuditIntents, x => x.Operation == terminal.Operation);
    }

    [Fact]
    public async Task Lease_expiry_stale_owner_fingerprint_and_tenant_are_fenced()
    {
        var repository = new LskuRetirementRequestOperationRepository(_database, new Tenant(_tenant));
        var operation = Candidate();
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => repository.TryClaimAsync(
            operation.OperationId, operation.OperationFingerprint, [LskuRetirementRequestCheckpoint.Prepared],
            $"worker-{i}", now, now + TimeSpan.TicksPerSecond)));
        var first = Assert.Single(claims, x => x is not null)!;
        var mutation = new Diten.MdmService.Domain.Repositories.LskuRetirementRequestMutation(
            LskuRetirementRequestCheckpoint.AwaitingMakerReplay,
            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now + 1, ReleaseLease: true);
        Assert.False(await repository.AdvanceAsync(first with { TenantId = Guid.NewGuid() }, mutation));
        Assert.False(await repository.AdvanceAsync(first with { OperationFingerprint = new string('f', 64) }, mutation));
        Assert.False(await repository.AdvanceAsync(first, mutation with { UpdatedAtUtcTicks = first.LeaseUntilUtcTicksV1 }));
        var recovered = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [LskuRetirementRequestCheckpoint.Prepared], "recovery", first.LeaseUntilUtcTicksV1 + 1,
            first.LeaseUntilUtcTicksV1 + TimeSpan.TicksPerMinute);
        Assert.NotNull(recovered);
        Assert.Equal(first.LeaseGeneration + 1, recovered!.LeaseGeneration);
        Assert.False(await repository.AdvanceAsync(first, mutation));
        Assert.True(await repository.AdvanceAsync(recovered, mutation with { UpdatedAtUtcTicks = first.LeaseUntilUtcTicksV1 + 2 }));
        var alien = Candidate(); alien.TenantId = Guid.NewGuid();
        Assert.False((await repository.ReserveAsync(alien)).Succeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Real_repository_terminal_readback_crash_replay_and_conflicting_binding_fail_closed(bool approved)
    {
        var operation = Candidate();
        var id = operation.LskuId;
        var lsku = new Lsku { Id = id, TenantId = _tenant, CanonicalCode = operation.ObjectRef,
            GskuId = Guid.NewGuid(), MarketCode = "TR", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("D"), Version = 2,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        await _lskus.InsertOneAsync(lsku);
        var repository = new LskuRepository(_database, new Tenant(_tenant));
        var operations = new LskuRetirementRequestOperationRepository(_database, new Tenant(_tenant));
        Assert.True((await operations.ReserveAsync(operation)).Succeeded);
        var binding = new Diten.MdmService.Domain.ValueObjects.LskuActiveLifecycleOperationBinding(
            Diten.MdmService.Domain.ValueObjects.LskuLifecycleOperationKind.Retirement, operation.OperationId, 2);
        var requested = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, 2,
            operation.OperationId, operation.MakerSubjectId, ProductAuditOperation.LskuRetirementRequested,
            operation.RequestReason, new(operation.CreatedAtUtcTicksV1, TimeSpan.Zero));
        Assert.True((await repository.AcquireLifecycleOperationAsync(id, 2, binding, requested)).Succeeded);
        Assert.True((await repository.AcquireLifecycleOperationAsync(id, 2, binding, requested)).IsReplay);
        Assert.False((await repository.AcquireLifecycleOperationAsync(id, 2,
            binding with { OperationId = Guid.NewGuid() }, requested)).Succeeded);
        Assert.False((await repository.AcquireLifecycleOperationAsync(id, 2,
            binding with { BaseLskuVersion = 1 }, requested)).Succeeded);
        var workflowId = Guid.NewGuid(); var task = Guid.NewGuid(); var version = Guid.NewGuid();
        await _collection.UpdateOneAsync(x => x.TenantId == _tenant && x.OperationId == operation.OperationId,
            Builders<LskuRetirementRequestOperation>.Update
                .Set(x => x.Checkpoint, LskuRetirementRequestCheckpoint.AwaitingDecision)
                .Set(x => x.WorkflowInstanceId, workflowId).Set(x => x.ApprovalTaskId, task)
                .Set(x => x.WorkflowTemplateVersionId, version));
        operation = (await operations.GetByOperationIdAsync(operation.OperationId))!;
        var evidence = new Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTerminalEvidence(
            workflowId, task, operation.WorkflowTemplateId!.Value, version,
            operation.ObjectType, operation.ObjectId, operation.ObjectRef, approved ? "Approve" : "Reject",
            Guid.NewGuid().ToString("D"), "DECISION", DateTimeOffset.UtcNow, 3,
            approved ? "Approved" : "Rejected", approved ? "Completed" : "Rejected", null);
        var client = new TerminalClient(evidence);
        var processor = new LskuRetirementRequestWorkflowProcessor(operations, repository, client,
            new(new(operation.WorkflowTemplateId, null, [Guid.NewGuid()], "RETIRE", true, true, null),
                TimeProvider.System), TimeProvider.System);
        var execution = new LskuRetirementRequestExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));
        var result = await processor.RecoverAsync(operation, "mongo-test", execution, default);
        Assert.True(result.Succeeded, result.ErrorCode);
        var current = (await repository.GetByIdAsync(id))!;
        Assert.Equal(4, current.Version);
        Assert.Equal(approved ? ProductIdentityLifecycleStatus.Retired : ProductIdentityLifecycleStatus.IdentityApproved,
            current.LifecycleStatus);
        Assert.Null(current.ActiveLifecycleOperation);
        var terminal = Assert.Single(current.AuditIntents, x => x.Operation != ProductAuditOperation.LskuRetirementRequested);
        Assert.False((await repository.ApplyRetirementDecisionAsync(id, 3,
            binding with { OperationId = Guid.NewGuid() }, approved, terminal)).Succeeded);
        Assert.False((await repository.ApplyRetirementDecisionAsync(id, 3,
            binding with { BaseLskuVersion = 1 }, approved, terminal)).Succeeded);
        // Reproduce a crash after the aggregate CAS but before advancing the operation checkpoint.
        await _collection.UpdateOneAsync(x => x.TenantId == _tenant && x.OperationId == operation.OperationId,
            Builders<LskuRetirementRequestOperation>.Update.Set(x => x.Checkpoint,
                LskuRetirementRequestCheckpoint.DecisionObserved).Set(x => x.IsActive, true));
        result = await processor.RecoverAsync((await operations.GetByOperationIdAsync(operation.OperationId))!,
            "recovery", execution, default);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, (await repository.GetByIdAsync(id))!.AuditIntents.Count);
        // An acknowledged checkpoint alone cannot hide a mismatching source product.
        await _lskus.UpdateOneAsync(x => x.TenantId == _tenant && x.Id == id, Builders<Lsku>.Update.Inc(x => x.Version, 1));
        await _collection.UpdateOneAsync(x => x.TenantId == _tenant && x.OperationId == operation.OperationId,
            Builders<LskuRetirementRequestOperation>.Update.Set(x => x.Checkpoint, LskuRetirementRequestCheckpoint.DecisionApplied)
                .Set(x => x.IsActive, true));
        result = await processor.RecoverAsync((await operations.GetByOperationIdAsync(operation.OperationId))!,
            "readback", execution, default);
        Assert.False(result.Succeeded);
        Assert.Equal("LSKU_RETIREMENT_READBACK_CONFLICT", result.ErrorCode);
    }

    private sealed class TerminalClient(Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTerminalEvidence evidence)
        : Diten.MdmService.Application.Contracts.Workflow.IProductIdentityWorkflowClient
    {
        public Task<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportResult<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenant, Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTerminalEvidenceRequest request, CancellationToken ct = default) =>
            Task.FromResult(Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportResult<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTerminalEvidence>.Success(evidence));
        public Task<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportResult<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenant, Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowStartRequest request, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportResult<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenant, Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowStartResultRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    public async Task DisposeAsync()
    {
        await _collection.DeleteManyAsync(x => x.TenantId == _tenant);
        await _lskus.DeleteManyAsync(x => x.TenantId == _tenant);
    }

    private LskuRetirementRequestOperation Candidate(Guid? lskuId = null)
    {
        var id = Guid.NewGuid();
        var lsku = new Lsku { Id = lskuId ?? Guid.NewGuid(), TenantId = _tenant, CanonicalCode = "LS-1",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2 };
        return new LskuRetirementRequestWorkflowStartRequestFactory(new(Guid.NewGuid(), null,
            [Guid.NewGuid()], "RETIRE", true, true, null), TimeProvider.System)
            .Create(lsku, id, Guid.NewGuid(), "Identity is obsolete").Operation;
    }

    private static LskuRetirementRequestOperation Copy(LskuRetirementRequestOperation x) => new()
    {
        Id=x.Id,TenantId=x.TenantId,OperationId=x.OperationId,LskuId=x.LskuId,
        BaseLskuVersion=x.BaseLskuVersion,MakerSubjectId=x.MakerSubjectId,RequestReason=x.RequestReason,
        WorkflowTemplateId=x.WorkflowTemplateId,WorkflowTemplateCode=x.WorkflowTemplateCode,
        CandidatePrincipalIds=[..x.CandidatePrincipalIds],ReasonCode=x.ReasonCode,
        CommentRequired=x.CommentRequired,EvidenceRequired=x.EvidenceRequired,
        ConfiguredDueAfterSeconds=x.ConfiguredDueAfterSeconds,DueAtUtcTicksV1=x.DueAtUtcTicksV1,
        ObjectType=x.ObjectType,ObjectId=x.ObjectId,ObjectRef=x.ObjectRef,
        StartIdempotencyKey=x.StartIdempotencyKey,OperationFingerprint=x.OperationFingerprint,
        CreatedAtUtcTicksV1=x.CreatedAtUtcTicksV1,UpdatedAtUtcTicksV1=x.UpdatedAtUtcTicksV1
    };

    private sealed class Tenant(Guid value) : ITenantContext
    {
        public Guid TenantId { get; private set; } = value;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
    private sealed class UnresolvedTenant : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("Unresolved");
        public bool IsResolved => false;
        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
    }
}
