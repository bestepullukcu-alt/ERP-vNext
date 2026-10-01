using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class LskuIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<LskuIdentityWorkflowOperation> _collection = null!;

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
        _collection = _database.GetCollection<LskuIdentityWorkflowOperation>(
            LskuIdentityWorkflowOperationRepository.CollectionName);
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Withdrawal_is_atomic_replayable_and_preserves_identity_tuple()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("withdraw", new string('6', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow;
        var workflowId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var binding = new ProductIdentityWorkflowBinding
        {
            WorkflowInstanceId = workflowId, WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = templateVersionId, ApprovalTaskId = taskId,
            AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "lsku", ObjectId = operation.LskuId, ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId, SubmittedAtUtc = now,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint
        };
        var lsku = new Lsku
        {
            Id = operation.LskuId, TenantId = _tenantId, GskuId = operation.GskuId,
            MarketCode = "TR", MarketSelection = operation.MarketSelection,
            CanonicalCode = "LS-WITHDRAW", CreationCommandId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = binding, Version = 1, CreatedAt = now, UpdatedAt = now
        };
        await _database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        await _collection.UpdateOneAsync(x => x.TenantId == _tenantId && x.OperationId == operation.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.WithdrawalObserved)
                .Set(x => x.WorkflowInstanceId, workflowId).Set(x => x.ApprovalTaskId, taskId)
                .Set(x => x.WorkflowTemplateVersionId, templateVersionId)
                .Set(x => x.WithdrawalCommandId, commandId)
                .Set(x => x.WithdrawalExpectedLskuVersion, 1));
        operation = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.WithdrawalObserved], "withdrawer", now.UtcTicks,
            now.AddMinutes(1).UtcTicks));
        var cancellation = new ProductIdentityWorkflowCancellationEvidence
        {
            WorkflowInstanceId = workflowId, ApprovalTaskId = taskId,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = templateVersionId, ObjectType = "lsku", ObjectId = operation.LskuId,
            ObjectRef = operation.ObjectRef, RequesterSubjectId = operation.MakerSubjectId,
            ReasonCode = "REQUESTER_WITHDRAWAL", CancelledAtUtc = now, TransitionSequence = 2,
            TransitionLogId = Guid.NewGuid(), TaskStatus = "Cancelled", InstanceStatus = "Cancelled",
            WorkflowInstanceVersion = 2, ApprovalTaskVersion = 2, IdempotencyKey = commandId.ToString("D")
        };
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateWithdrawal(lsku, 1, cancellation);
        var first = await repository.ApplyWithdrawalAsync(claim!, operation, cancellation, audit, now.AddTicks(1).UtcTicks);
        var replay = await repository.ApplyWithdrawalAsync(claim!, operation, cancellation, audit, now.AddTicks(2).UtcTicks);
        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, replay.Lsku!.LifecycleStatus);
        Assert.Equal(operation.GskuId, replay.Lsku.GskuId);
        Assert.Equal("TR", replay.Lsku.MarketCode);
        Assert.Single(replay.Lsku.AuditIntents, x => x.Operation == ProductAuditOperation.LskuIdentityApprovalWithdrawn);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.WithdrawalApplied,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
    }

    [Fact]
    public async Task Cancellation_observation_accepts_exact_object_reference_without_rewriting_preflight()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("cancel-observation", new string('7', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        await _collection.UpdateOneAsync(x => x.TenantId == _tenantId && x.OperationId == operation.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.Checkpoint,
                LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved));
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved], "observer", now, now + TimeSpan.TicksPerMinute));
        var mutation = new LskuIdentityWorkflowCheckpointMutation(LskuIdentityWorkflowCheckpoint.WithdrawalObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1,
            WithdrawalTransitionLogId: Guid.NewGuid(), WithdrawalObservedAtUtcTicksV1: now,
            WithdrawalTransitionSequence: 2, WithdrawalResultWorkflowInstanceVersion: 2,
            WithdrawalResultApprovalTaskVersion: 2, WithdrawalTaskStatus: "Cancelled",
            WithdrawalInstanceStatus: "Cancelled", WithdrawalObjectRef: operation.ObjectRef, ReleaseLease: true);
        Assert.False(await repository.AdvanceAsync(claim!, mutation with { WithdrawalObjectRef = null }));
        Assert.False(await repository.AdvanceAsync(claim!, mutation with { WithdrawalExpectedWorkflowInstanceVersion = 1 }));
        Assert.True(await repository.AdvanceAsync(claim!, mutation));
        Assert.Equal(operation.ObjectRef, (await repository.GetByOperationIdAsync(operation.OperationId))!.WithdrawalObjectRef);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_and_tenant_isolated()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("reserve", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(ReplayOf(operation));
        var drifted = ReplayOf(operation);
        drifted.OperationFingerprint =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var drift = await repository.ReserveAsync(drifted);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("LSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Null(await Repository(_otherTenantId).GetByOperationIdAsync(operation.OperationId));
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Claim_has_one_winner_and_expired_lease_increments_generation()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("claim", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], $"worker-{index}", now,
            now + TimeSpan.FromMinutes(1).Ticks))));
        var winner = Assert.Single(claims, value => value is not null)!;
        Assert.Equal(1, winner.LeaseGeneration);
        Assert.Null(await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], "early", now + 1,
            now + TimeSpan.FromMinutes(1).Ticks)));
        var recovered = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], "recovered",
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
            Builders<LskuIdentityWorkflowOperation>.Update.Set(
                item => item.Checkpoint, LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay));
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay], "maker", now,
            now + TimeSpan.FromMinutes(1).Ticks));

        Assert.NotNull(claim);
        var workflowInstanceId = Guid.NewGuid();
        var workflowTemplateId = Guid.NewGuid();
        var workflowTemplateVersionId = Guid.NewGuid();
        var approvalTaskId = Guid.NewGuid();
        var assignmentSnapshotId = Guid.NewGuid();
        var startTransitionLogId = Guid.NewGuid();
        Assert.True(await repository.AdvanceAsync(claim!, new(
            LskuIdentityWorkflowCheckpoint.WorkflowStarted,
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
        Assert.Equal(LskuIdentityWorkflowCheckpoint.WorkflowStarted, stored!.Checkpoint);
        Assert.Equal(workflowInstanceId, stored.WorkflowInstanceId);
        Assert.Equal(approvalTaskId, stored.ApprovalTaskId);
    }

    [Fact]
    public async Task Approval_proof_is_atomic_exact_and_immutable_after_crash()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("approval", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var sequence = 7L;
        await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.OperationId == operation.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.DecisionObserved)
                .Set(x => x.DecisionKind, ProductIdentityDecisionKind.Approved)
                .Set(x => x.DecisionTransitionSequence, sequence));
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.DecisionObserved], "validator", now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);
        var selection = Selection();
        var proof = ApprovalProof(operation, sequence, selection, now + 1);
        Assert.True(await repository.AdvanceAsync(claim!, new(
            LskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1,
            ApprovalMarketSelection: selection,
            MarketValidatedAtUtcTicksV1: now + 1,
            ApprovalMarketProofFingerprint: proof,
            ReleaseLease: true)));
        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ApprovalValidated, stored!.Checkpoint);
        Assert.Equal(proof, stored.ApprovalMarketProofFingerprint);
        var recovery = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.ApprovalValidated], "recovery", now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(recovery);
        Assert.False(await repository.AdvanceAsync(recovery!, new(
            LskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 3,
            ApprovalMarketSelection: Selection(), MarketValidatedAtUtcTicksV1: now + 3,
            ApprovalMarketProofFingerprint: new string('e', 64), ReleaseLease: true)));
        var unchanged = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(proof, unchanged!.ApprovalMarketProofFingerprint);
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
            Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now));
        var p1 = await repository.DiscoverRecoverableAsync(now, 1);
        var p2 = await repository.DiscoverRecoverableAsync(now, 1, p1.NextCursor);
        var p3 = await repository.DiscoverRecoverableAsync(now, 1, p2.NextCursor);
        Assert.Equal(nullDue.OperationId, Assert.Single(p1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(p2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(p3.Operations).OperationId);

        var indexes = await (await _collection.Indexes.ListAsync()).ToListAsync();
        var owned = indexes.Where(x => x["name"].AsString.Contains("lsku_identity_workflow", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, owned.Length);
        Assert.All(owned, index => Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name));
    }

    [Fact]
    public async Task Exact_legacy_market_contract_failure_can_resume_to_persisted_decision_once()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("manual-resume", new string('4', 64));
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == operation.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(item => item.RecoveryDisposition,
                    ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(item => item.LastFailureCode, "REFERENCE_CONTRACT_MISMATCH")
                .Set(item => item.DecisionKind, ProductIdentityDecisionKind.Approved));

        var claim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker",
            now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);

        Assert.True(await repository.AdvanceAsync(claim!, new(
            LskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ReleaseLease: true)));
        var resumed = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionObserved, resumed!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, resumed.RecoveryDisposition);
        Assert.Null(resumed.LastFailureCode);

        var wrongFailure = Operation("manual-reject", new string('5', 64));
        Assert.True((await repository.ReserveAsync(wrongFailure)).Succeeded);
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == wrongFailure.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(item => item.RecoveryDisposition,
                    ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(item => item.LastFailureCode, "REFERENCE_MARKET_NOT_FOUND")
                .Set(item => item.DecisionKind, ProductIdentityDecisionKind.Approved));
        var rejectedClaim = await repository.TryClaimAsync(new(
            wrongFailure.OperationId,
            wrongFailure.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker",
            now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(rejectedClaim);
        Assert.False(await repository.AdvanceAsync(rejectedClaim!, new(
            LskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 3,
            ReleaseLease: true)));
    }

    public async Task DisposeAsync()
    {
        await _collection.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
        await _database.GetCollection<Lsku>("mdm_lskus").DeleteManyAsync(
            x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
    }

    private LskuIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private LskuIdentityWorkflowOperation Operation(
        string key, string fingerprint, Guid? operationId = null, Guid? lskuId = null,
        Guid? gskuId = null, Guid? revisionId = null, ReferenceCatalogSelection? selection = null)
    {
        var lsku = lskuId ?? Guid.NewGuid();
        var operation = operationId ?? Guid.NewGuid();
        return new()
        {
            Id = operation, TenantId = _tenantId,
            OperationId = operation, LskuId = lsku,
            GskuId = gskuId ?? Guid.NewGuid(), ProductDefinitionRevisionId = revisionId ?? Guid.NewGuid(),
            ExpectedLskuVersion = 0, MakerSubjectId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "LSKU_IDENTITY_APPROVAL",
            ObjectType = "lsku", ObjectId = lsku.ToString("D"), ObjectRef = $"LS:{lsku:D}",
            StartIdempotencyKey = $"lsku-{key}", OperationFingerprint = fingerprint,
            MarketCode = "TR", MarketSelection = selection ?? Selection(),
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private static ReferenceCatalogSelection Selection() => new()
    {
        SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
        CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private static string ApprovalProof(
        LskuIdentityWorkflowOperation operation, long sequence,
        ReferenceCatalogSelection selection, long validatedAt)
    {
        var facts = string.Join('|', "lsku-approval-market-v1", operation.TenantId.ToString("D"),
            operation.OperationId.ToString("D"), operation.LskuId.ToString("D"), operation.MarketCode,
            sequence.ToString(CultureInfo.InvariantCulture), selection.SetCode, selection.ValueCode,
            selection.CatalogVersionId.ToString("D"),
            selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
            ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture),
            selection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            validatedAt.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static LskuIdentityWorkflowOperation ReplayOf(LskuIdentityWorkflowOperation source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, OperationId = source.OperationId,
        LskuId = source.LskuId, GskuId = source.GskuId,
        ProductDefinitionRevisionId = source.ProductDefinitionRevisionId,
        ExpectedLskuVersion = source.ExpectedLskuVersion, MakerSubjectId = source.MakerSubjectId,
        WorkflowTemplateId = source.WorkflowTemplateId, WorkflowTemplateCode = source.WorkflowTemplateCode,
        CandidatePrincipalIds = [.. source.CandidatePrincipalIds], ReasonCode = source.ReasonCode,
        CommentRequired = source.CommentRequired, EvidenceRequired = source.EvidenceRequired,
        DueAtUtcTicksV1 = source.DueAtUtcTicksV1, ObjectType = source.ObjectType,
        ObjectId = source.ObjectId, ObjectRef = source.ObjectRef,
        StartIdempotencyKey = source.StartIdempotencyKey,
        OperationFingerprint = source.OperationFingerprint, MarketCode = source.MarketCode,
        MarketSelection = source.MarketSelection, CreatedAtUtcTicksV1 = source.CreatedAtUtcTicksV1
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
