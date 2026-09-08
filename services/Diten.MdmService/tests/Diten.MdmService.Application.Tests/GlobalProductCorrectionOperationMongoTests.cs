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
public sealed class GlobalProductCorrectionOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProductCorrectionOperation> _collection = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<GlobalProductCorrectionOperation>(
            GlobalProductCorrectionOperationRepository.CollectionName);
    }

    [Fact]
    public async Task Reserve_is_exactly_idempotent_tenant_scoped_and_lease_fenced()
    {
        var repository = new GlobalProductCorrectionOperationRepository(_database, new Tenant(_tenantId));
        var operation = Candidate();
        var first = await repository.ReserveAsync(operation);
        var replayCandidate = Candidate(operation.OperationId);
        replayCandidate.GlobalProductId = operation.GlobalProductId;
        replayCandidate.MakerSubjectId = operation.MakerSubjectId;
        replayCandidate.WorkflowTemplateId = operation.WorkflowTemplateId;
        replayCandidate.CandidatePrincipalIds = [.. operation.CandidatePrincipalIds];
        replayCandidate.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        replayCandidate.UpdatedAtUtcTicksV1 = operation.UpdatedAtUtcTicksV1;
        var replay = await repository.ReserveAsync(replayCandidate);
        var drift = replayCandidate; drift.ProposedGlobalProductName = "Drift";
        var conflict = await repository.ReserveAsync(drift);
        drift.ProposedGlobalProductName = operation.ProposedGlobalProductName;
        drift.ReasonCode = "DRIFTED_REASON";
        var startFactConflict = await repository.ReserveAsync(drift);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GlobalProductCorrectionCheckpoint.Prepared], "worker", now, now + TimeSpan.TicksPerMinute);
        var secondClaim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GlobalProductCorrectionCheckpoint.Prepared], "other", now, now + TimeSpan.TicksPerMinute);

        Assert.True(first.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(conflict.Succeeded);
        Assert.False(startFactConflict.Succeeded);
        Assert.NotNull(claim);
        Assert.Null(secondClaim);
        Assert.Null(await new GlobalProductCorrectionOperationRepository(_database, new Tenant(Guid.NewGuid()))
            .GetByOperationIdAsync(operation.OperationId));
    }

    [Fact]
    public async Task Every_recovery_checkpoint_survives_repository_recreation_and_exact_cas_reclaim()
    {
        var operation = Candidate();
        Assert.True((await new GlobalProductCorrectionOperationRepository(_database, new Tenant(_tenantId))
            .ReserveAsync(operation)).Succeeded);
        var checkpoints = new[]
        {
            GlobalProductCorrectionCheckpoint.StartOutcomeUnknown,
            GlobalProductCorrectionCheckpoint.WorkflowStarted,
            GlobalProductCorrectionCheckpoint.AwaitingDecision,
            GlobalProductCorrectionCheckpoint.DecisionObserved,
            GlobalProductCorrectionCheckpoint.DecisionApplied,
            GlobalProductCorrectionCheckpoint.Completed
        };
        var current = GlobalProductCorrectionCheckpoint.Prepared;
        foreach (var next in checkpoints)
        {
            var repository = new GlobalProductCorrectionOperationRepository(_database, new Tenant(_tenantId));
            var now = DateTimeOffset.UtcNow.UtcTicks;
            var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [current], $"worker-{(int)next}", now, now + TimeSpan.TicksPerMinute);
            Assert.NotNull(claim);
            Assert.True(await repository.AdvanceAsync(claim!, new(next,
                ProductIdentityWorkflowRecoveryDisposition.None, now, ReleaseLease: true)));
            Assert.Equal(next, (await new GlobalProductCorrectionOperationRepository(
                _database, new Tenant(_tenantId)).GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
            current = next;
        }
    }

    [Fact]
    public async Task Concurrent_same_command_with_advancing_clocks_replays_persisted_due_at_and_fingerprint()
    {
        var repository = new GlobalProductCorrectionOperationRepository(_database, new Tenant(_tenantId));
        var operationId = Guid.NewGuid();
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = "GP-CORRECTION-REPLAY",
            GlobalProductName = "Current", GlobalProductNameNormalized = "CURRENT",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 4
        };
        var template = Guid.NewGuid();
        var identityTemplate = Guid.NewGuid();
        var principal = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var configuration = new GlobalProductCorrectionStartConfiguration(template, null, [principal],
            "CORRECTION", true, true, TimeSpan.FromHours(1), identityTemplate, null);
        var firstClock = new FixedTimeProvider(new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero));
        var secondClock = new FixedTimeProvider(firstClock.GetUtcNow().AddSeconds(1));
        var firstCandidate = new GlobalProductCorrectionWorkflowStartRequestFactory(configuration, firstClock)
            .Create(product, operationId, maker, "Corrected").Operation;
        var racingCandidate = new GlobalProductCorrectionWorkflowStartRequestFactory(configuration, secondClock)
            .Create(product, operationId, maker, "Corrected").Operation;

        Assert.NotEqual(firstCandidate.DueAtUtcTicksV1, racingCandidate.DueAtUtcTicksV1);
        Assert.NotEqual(firstCandidate.OperationFingerprint, racingCandidate.OperationFingerprint);
        var first = await repository.ReserveAsync(firstCandidate);
        var replay = await repository.ReserveAsync(racingCandidate);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(firstCandidate.DueAtUtcTicksV1, replay.Operation!.DueAtUtcTicksV1);
        Assert.Equal(firstCandidate.OperationFingerprint, replay.Operation.OperationFingerprint);
    }

    [Fact]
    public async Task Non_exact_reason_is_rejected_before_operation_persistence()
    {
        var repository = new GlobalProductCorrectionOperationRepository(_database, new Tenant(_tenantId));
        var candidate = Candidate();
        candidate.ReasonCode = " CORRECTION ";

        var result = await repository.ReserveAsync(candidate);

        Assert.False(result.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_OPERATION_INVALID", result.ErrorCode);
        Assert.Null(await repository.GetByOperationIdAsync(candidate.OperationId));
    }

    public Task DisposeAsync() => _collection.DeleteManyAsync(x => x.TenantId == _tenantId);

    private GlobalProductCorrectionOperation Candidate(Guid? operationId = null)
    {
        var id = operationId ?? Guid.NewGuid();
        return new()
        {
            Id = id, TenantId = _tenantId, OperationId = id, GlobalProductId = Guid.NewGuid(),
            BaseProductVersion = 2, MakerSubjectId = Guid.NewGuid(), ProposedGlobalProductName = "Corrected",
            ProposedGlobalProductNameNormalized = "CORRECTED", WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "CORRECTION",
            ObjectType = "GlobalProductCorrection", ObjectId = id.ToString("D"), ObjectRef = "GP-1",
            StartIdempotencyKey = $"global-product-correction:{_tenantId:D}:{id:D}",
            OperationFingerprint = new string('a', 64), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks,
            UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
