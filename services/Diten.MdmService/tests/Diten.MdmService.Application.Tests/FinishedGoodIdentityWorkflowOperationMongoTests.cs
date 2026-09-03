using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FinishedGoodIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<FinishedGoodIdentityWorkflowOperation> _collection = null!;

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
        _collection = _database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
            FinishedGoodIdentityWorkflowOperationRepository.CollectionName);
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_tenant_isolated_and_size_bounded()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("reserve", new string('a', 64));
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(ReplayOf(operation));
        var drifted = ReplayOf(operation);
        drifted.OperationFingerprint = new string('b', 64);
        var drift = await repository.ReserveAsync(drifted);
        var oversized = Operation("oversized", new string('c', 64));
        oversized.ObjectRef = new string('x', 1024 * 1024);
        var rejected = await repository.ReserveAsync(oversized);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.False(rejected.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_INVALID", rejected.ErrorCode);
        Assert.Null(await Repository(_otherTenantId).GetByOperationIdAsync(operation.OperationId));
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Claim_has_one_winner_and_expired_lease_increments_generation()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("claim", new string('c', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared], $"worker-{index}", now,
            now + TimeSpan.FromMinutes(1).Ticks))));
        var winner = Assert.Single(claims, value => value is not null)!;
        Assert.Equal(1, winner.LeaseGeneration);
        Assert.Null(await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared], "early", now + 1,
            now + TimeSpan.FromMinutes(1).Ticks)));
        var recovered = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared], "recovered",
            now + TimeSpan.FromMinutes(1).Ticks + 1,
            now + TimeSpan.FromMinutes(2).Ticks));
        Assert.NotNull(recovered);
        Assert.Equal(2, recovered!.LeaseGeneration);
    }

    [Fact]
    public async Task Maker_replay_can_persist_the_started_workflow_proof()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("maker-replay", new string('f', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == operation.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(
                item => item.Checkpoint, FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay));
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay], "maker", now,
            now + TimeSpan.FromMinutes(1).Ticks));

        Assert.NotNull(claim);
        var workflowInstanceId = Guid.NewGuid();
        var workflowTemplateId = Guid.NewGuid();
        var workflowTemplateVersionId = Guid.NewGuid();
        var approvalTaskId = Guid.NewGuid();
        var assignmentSnapshotId = Guid.NewGuid();
        var startTransitionLogId = Guid.NewGuid();
        Assert.True(await repository.AdvanceAsync(claim!, new(
            FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1,
            WorkflowInstanceId: workflowInstanceId,
            WorkflowTemplateId: workflowTemplateId,
            WorkflowTemplateVersionId: workflowTemplateVersionId,
            ApprovalTaskId: approvalTaskId,
            AssignmentSnapshotId: assignmentSnapshotId,
            StartTransitionLogId: startTransitionLogId,
            WorkflowStartedAtUtcTicksV1: now + 1,
            ReleaseLease: true)));
        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted, stored!.Checkpoint);
        Assert.Equal(workflowInstanceId, stored.WorkflowInstanceId);
        Assert.Equal(approvalTaskId, stored.ApprovalTaskId);
    }

    [Fact]
    public async Task Approval_parent_proof_is_atomic_exact_and_immutable_after_crash()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("approval", new string('d', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        const long sequence = 7;
        await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.OperationId == operation.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved)
                .Set(x => x.DecisionKind, ProductIdentityDecisionKind.Approved)
                .Set(x => x.DecisionTransitionSequence, sequence));
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved], "validator", now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);
        var proof = ApprovalProof(operation, sequence, now + 1);
        Assert.True(await repository.AdvanceAsync(claim!, new(
            FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1,
            ApprovalParentValidatedAtUtcTicksV1: now + 1,
            ApprovalParentProofFingerprint: proof,
            ReleaseLease: true)));
        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated, stored!.Checkpoint);
        Assert.Equal(proof, stored.ApprovalParentProofFingerprint);
        var recovery = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated], "recovery", now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(recovery);
        Assert.False(await repository.AdvanceAsync(recovery!, new(
            FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 3,
            ApprovalParentValidatedAtUtcTicksV1: now + 3,
            ApprovalParentProofFingerprint: new string('e', 64), ReleaseLease: true)));
        var unchanged = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(proof, unchanged!.ApprovalParentProofFingerprint);
    }

    [Fact]
    public async Task Recovery_order_is_null_then_due_tick_then_operation_id_and_indexes_are_tenant_first()
    {
        var repository = Repository(_tenantId);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var nullDue = Operation("null", new string('1', 64), Guid.Parse("01000000-0000-0000-0000-000000000001"));
        var first = Operation("first", new string('2', 64), Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var second = Operation("second", new string('3', 64), Guid.Parse("20000000-0000-0000-0000-000000000001"));
        foreach (var item in new[] { nullDue, first, second })
            Assert.True((await repository.ReserveAsync(item)).Succeeded);
        await _collection.UpdateManyAsync(
            x => x.TenantId == _tenantId && new[] { first.OperationId, second.OperationId }.Contains(x.OperationId),
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now));
        var p1 = await repository.DiscoverRecoverableAsync(now, 1);
        var p2 = await repository.DiscoverRecoverableAsync(now, 1, p1.NextCursor);
        var p3 = await repository.DiscoverRecoverableAsync(now, 1, p2.NextCursor);
        Assert.Equal(nullDue.OperationId, Assert.Single(p1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(p2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(p3.Operations).OperationId);

        var indexes = await (await _collection.Indexes.ListAsync()).ToListAsync();
        var owned = indexes.Where(x => x["name"].AsString.Contains(
            "finished_good_identity_workflow", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, owned.Length);
        Assert.All(owned, index => Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name));
    }

    public async Task DisposeAsync() => await _collection.DeleteManyAsync(
        x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);

    private FinishedGoodIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private FinishedGoodIdentityWorkflowOperation Operation(
        string key, string fingerprint, Guid? operationId = null, Guid? finishedGoodId = null,
        Guid? gskuId = null, Guid? revisionId = null)
    {
        var finishedGood = finishedGoodId ?? Guid.NewGuid();
        var operation = operationId ?? Guid.NewGuid();
        return new()
        {
            Id = operation, TenantId = _tenantId,
            OperationId = operation, FinishedGoodId = finishedGood,
            GskuId = gskuId ?? Guid.NewGuid(), ProductDefinitionRevisionId = revisionId ?? Guid.NewGuid(),
            ExpectedFinishedGoodVersion = 0, MakerSubjectId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "FINISHED_GOOD_IDENTITY_APPROVAL",
            ObjectType = "finished-good", ObjectId = finishedGood.ToString("D"),
            ObjectRef = $"FG:{finishedGood:D}", StartIdempotencyKey = $"finished-good-{key}",
            OperationFingerprint = fingerprint, CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private static string ApprovalProof(
        FinishedGoodIdentityWorkflowOperation operation, long sequence, long validatedAt)
    {
        var facts = string.Join('|', "finished-good-approval-parent-v1", operation.TenantId.ToString("D"),
            operation.OperationId.ToString("D"), operation.FinishedGoodId.ToString("D"),
            operation.GskuId.ToString("D"), operation.ProductDefinitionRevisionId.ToString("D"),
            sequence.ToString(CultureInfo.InvariantCulture), validatedAt.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static FinishedGoodIdentityWorkflowOperation ReplayOf(FinishedGoodIdentityWorkflowOperation source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, OperationId = source.OperationId,
        FinishedGoodId = source.FinishedGoodId, GskuId = source.GskuId,
        ProductDefinitionRevisionId = source.ProductDefinitionRevisionId,
        ExpectedFinishedGoodVersion = source.ExpectedFinishedGoodVersion, MakerSubjectId = source.MakerSubjectId,
        WorkflowTemplateId = source.WorkflowTemplateId, WorkflowTemplateCode = source.WorkflowTemplateCode,
        CandidatePrincipalIds = [.. source.CandidatePrincipalIds], ReasonCode = source.ReasonCode,
        CommentRequired = source.CommentRequired, EvidenceRequired = source.EvidenceRequired,
        DueAtUtcTicksV1 = source.DueAtUtcTicksV1, ObjectType = source.ObjectType,
        ObjectId = source.ObjectId, ObjectRef = source.ObjectRef,
        StartIdempotencyKey = source.StartIdempotencyKey,
        OperationFingerprint = source.OperationFingerprint, CreatedAtUtcTicksV1 = source.CreatedAtUtcTicksV1
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
