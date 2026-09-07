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
