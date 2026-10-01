using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductRetirementRequestOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProductRetirementRequestOperation> _collection = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<GlobalProductRetirementRequestOperation>(
            GlobalProductRetirementRequestOperationRepository.CollectionName);
    }

    [Fact]
    public async Task Reserve_replay_tenant_and_lease_are_fail_closed()
    {
        var repository = new GlobalProductRetirementRequestOperationRepository(_database, new Tenant(_tenantId));
        var operation = Candidate();
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Copy(operation));
        var drift = Copy(operation); drift.GlobalProductId = Guid.NewGuid();
        var conflict = await repository.ReserveAsync(drift);
        var reasonDrift = Copy(operation); reasonDrift.RequestReason = "A different business reason";
        var reasonConflict = await repository.ReserveAsync(reasonDrift);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GlobalProductRetirementRequestCheckpoint.Prepared], "worker", now, now + TimeSpan.TicksPerMinute);
        var duplicateClaim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GlobalProductRetirementRequestCheckpoint.Prepared], "other", now, now + TimeSpan.TicksPerMinute);

        Assert.True(first.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(conflict.Succeeded);
        Assert.False(reasonConflict.Succeeded);
        Assert.NotNull(claim);
        Assert.Null(duplicateClaim);
        Assert.Null(await new GlobalProductRetirementRequestOperationRepository(_database,
            new Tenant(Guid.NewGuid())).GetByOperationIdAsync(operation.OperationId));
    }

    [Fact]
    public async Task Checkpoints_survive_repository_recreation_and_terminal_rows_are_not_recoverable()
    {
        var operation = Candidate();
        Assert.True((await new GlobalProductRetirementRequestOperationRepository(_database,
            new Tenant(_tenantId)).ReserveAsync(operation)).Succeeded);
        var checkpoints = new[] { GlobalProductRetirementRequestCheckpoint.WorkflowStarted,
            GlobalProductRetirementRequestCheckpoint.AwaitingDecision,
            GlobalProductRetirementRequestCheckpoint.DecisionObserved,
            GlobalProductRetirementRequestCheckpoint.DecisionApplied,
            GlobalProductRetirementRequestCheckpoint.Completed };
        var current = GlobalProductRetirementRequestCheckpoint.Prepared;
        foreach (var next in checkpoints)
        {
            var repository = new GlobalProductRetirementRequestOperationRepository(_database,
                new Tenant(_tenantId));
            var now = DateTimeOffset.UtcNow.UtcTicks;
            var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [current], $"worker-{next}", now, now + TimeSpan.TicksPerMinute);
            Assert.NotNull(claim);
            Assert.True(await repository.AdvanceAsync(claim!, new(next,
                ProductIdentityWorkflowRecoveryDisposition.None, now, ReleaseLease: true)));
            current = next;
        }
        var final = new GlobalProductRetirementRequestOperationRepository(_database, new Tenant(_tenantId));
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Completed,
            (await final.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Empty((await final.DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations);
    }

    [Fact]
    public async Task Concurrent_same_command_with_advancing_clock_replays_the_frozen_persisted_due_at()
    {
        var operationId = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2
        };
        var template = Guid.NewGuid();
        var principal = Guid.NewGuid();
        var configuration = new GlobalProductRetirementRequestStartConfiguration(template, null,
            [principal], "RETIRE", true, true, TimeSpan.FromHours(1),
            Guid.NewGuid(), null, Guid.NewGuid(), null);
        var firstPlan = new GlobalProductRetirementRequestWorkflowStartRequestFactory(configuration,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero)))
            .Create(product, operationId, maker, "Business reason");
        var racingPlan = new GlobalProductRetirementRequestWorkflowStartRequestFactory(configuration,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 10, 0, 1, TimeSpan.Zero)))
            .Create(product, operationId, maker, "Business reason");
        Assert.NotEqual(firstPlan.Operation.DueAtUtcTicksV1, racingPlan.Operation.DueAtUtcTicksV1);
        Assert.NotEqual(firstPlan.Operation.OperationFingerprint, racingPlan.Operation.OperationFingerprint);
        var repository = new GlobalProductRetirementRequestOperationRepository(_database, new Tenant(_tenantId));

        var first = await repository.ReserveAsync(firstPlan.Operation);
        var replay = await repository.ReserveAsync(racingPlan.Operation);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(firstPlan.Operation.DueAtUtcTicksV1, replay.Operation!.DueAtUtcTicksV1);
        Assert.Equal(firstPlan.Operation.OperationFingerprint, replay.Operation.OperationFingerprint);
    }

    [Fact]
    public async Task Due_at_drift_cannot_hide_an_unrelated_fingerprint_drift()
    {
        var operationId = Guid.NewGuid();
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2
        };
        var configuration = new GlobalProductRetirementRequestStartConfiguration(Guid.NewGuid(), null,
            [Guid.NewGuid()], "RETIRE", true, true, TimeSpan.FromHours(1),
            Guid.NewGuid(), null, Guid.NewGuid(), null);
        var first = new GlobalProductRetirementRequestWorkflowStartRequestFactory(configuration,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero)))
            .Create(product, operationId, Guid.NewGuid(), "Business reason").Operation;
        var forgedReplay = Copy(first);
        forgedReplay.DueAtUtcTicksV1 += TimeSpan.TicksPerSecond;
        forgedReplay.OperationFingerprint = new string('b', 64);
        var repository = new GlobalProductRetirementRequestOperationRepository(_database, new Tenant(_tenantId));

        Assert.True((await repository.ReserveAsync(first)).Succeeded);
        var conflict = await repository.ReserveAsync(forgedReplay);

        Assert.False(conflict.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_IDEMPOTENCY_CONFLICT", conflict.ErrorCode);
        var persisted = await repository.GetByOperationIdAsync(operationId);
        Assert.Equal(first.DueAtUtcTicksV1, persisted!.DueAtUtcTicksV1);
        Assert.Equal(first.OperationFingerprint, persisted.OperationFingerprint);
    }

    [Theory]
    [InlineData(" RETIREMENT ", "Identity is obsolete")]
    [InlineData("RETIREMENT\n", "Identity is obsolete")]
    [InlineData("RETIREMENT", " Identity is obsolete")]
    [InlineData("RETIREMENT", "Identity is obsolete\n")]
    public async Task Non_exact_reason_facts_are_rejected_before_operation_persistence(
        string reasonCode,
        string requestReason)
    {
        var repository = new GlobalProductRetirementRequestOperationRepository(_database, new Tenant(_tenantId));
        var candidate = Candidate();
        candidate.ReasonCode = reasonCode;
        candidate.RequestReason = requestReason;

        var result = await repository.ReserveAsync(candidate);

        Assert.False(result.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_OPERATION_INVALID", result.ErrorCode);
        Assert.Null(await repository.GetByOperationIdAsync(candidate.OperationId));
    }

    [Fact]
    public async Task Tenant_partition_discovery_does_not_require_a_resolved_tenant_context()
    {
        var writer = new GlobalProductRetirementRequestOperationRepository(
            _database,
            new Tenant(_tenantId));
        Assert.True((await writer.ReserveAsync(Candidate())).Succeeded);
        var repository = new GlobalProductRetirementRequestOperationRepository(
            _database,
            new UnresolvedTenant());

        var page = await repository.DiscoverTenantPartitionsAsync(null, 100);

        Assert.Contains(_tenantId, page.TenantIds);
    }

    public Task DisposeAsync() => _collection.DeleteManyAsync(x => x.TenantId == _tenantId);

    private GlobalProductRetirementRequestOperation Candidate()
    {
        var id = Guid.NewGuid();
        return new() { Id = id, TenantId = _tenantId, OperationId = id,
            GlobalProductId = Guid.NewGuid(), BaseProductVersion = 2, MakerSubjectId = Guid.NewGuid(),
            RequestReason = "Identity is obsolete",
            WorkflowTemplateId = Guid.NewGuid(), CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "RETIREMENT", ObjectType = "GlobalProductRetirement", ObjectId = id.ToString("D"),
            ObjectRef = "GP-1", StartIdempotencyKey = $"global-product-retirement:{_tenantId:D}:{id:D}",
            OperationFingerprint = new string('a', 64), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks,
            UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks };
    }

    private static GlobalProductRetirementRequestOperation Copy(GlobalProductRetirementRequestOperation x) => new()
    {
        Id = x.Id, TenantId = x.TenantId, OperationId = x.OperationId, GlobalProductId = x.GlobalProductId,
        BaseProductVersion = x.BaseProductVersion, MakerSubjectId = x.MakerSubjectId,
        RequestReason = x.RequestReason,
        WorkflowTemplateId = x.WorkflowTemplateId, CandidatePrincipalIds = [.. x.CandidatePrincipalIds],
        ReasonCode = x.ReasonCode, ObjectType = x.ObjectType, ObjectId = x.ObjectId, ObjectRef = x.ObjectRef,
        CommentRequired = x.CommentRequired, EvidenceRequired = x.EvidenceRequired,
        ConfiguredDueAfterSeconds = x.ConfiguredDueAfterSeconds, DueAtUtcTicksV1 = x.DueAtUtcTicksV1,
        StartIdempotencyKey = x.StartIdempotencyKey, OperationFingerprint = x.OperationFingerprint,
        CreatedAtUtcTicksV1 = x.CreatedAtUtcTicksV1, UpdatedAtUtcTicksV1 = x.UpdatedAtUtcTicksV1
    };

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class UnresolvedTenant : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("TenantId has not been resolved.");
        public bool IsResolved => false;
        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
