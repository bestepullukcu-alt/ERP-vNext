using Diten.MdmService.Application.Common;
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
public sealed class FirstGskuIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<FirstGskuIdentityWorkflowOperation> _collection = null!;
    private IMongoCollection<ProductDefinitionRevision> _revisions = null!;
    private IMongoCollection<Gsku> _gskus = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            MdmTestMongo.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<FirstGskuIdentityWorkflowOperation>(
            FirstGskuIdentityWorkflowOperationRepository.CollectionName);
        _revisions = _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_pair_scoped_and_tenant_isolated()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("pair-key", "fingerprint-a");
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Operation(
            "pair-key", "fingerprint-a", operation.OperationId,
            operation.ProductDefinitionRevisionId, operation.GskuId));
        var drift = await repository.ReserveAsync(Operation(
            "pair-key", "fingerprint-b", operation.OperationId,
            operation.ProductDefinitionRevisionId, operation.GskuId));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Null(await Repository(_tenantBId).GetByOperationIdAsync(operation.OperationId));
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Claim_checkpoint_proofs_are_scoped_and_maker_replay_can_persist_exact_start_proof()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("claim", "claim-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.Prepared], $"worker-{i}", now,
            now + TimeSpan.FromMinutes(1).Ticks))));
        var winner = Assert.Single(claims, item => item is not null)!;
        Assert.False(await repository.AdvanceAsync(winner, new(
            FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ApprovalPackApplicabilitySelection: Selection("pack-applicability", operation.PackApplicabilityCode),
            ApprovalPackUomSelection: Selection("uom", operation.PackUomCode),
            ReferencesValidatedAtUtcTicksV1: now,
            ApprovalReferenceProofFingerprint: "proof-a",
            ReleaseLease: true)));
        Assert.True(await repository.AdvanceAsync(winner, new(
            FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1, ReleaseLease: true)));
        var unknown = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown], "lookup", now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(unknown);
        Assert.True(await repository.AdvanceAsync(unknown!, new(
            FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now + 3, ReleaseLease: true)));
        var makerReplay = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay], "maker", now + 4,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(makerReplay);
        Assert.True(await repository.AdvanceAsync(makerReplay!, new(
            FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 5,
            WorkflowInstanceId: Guid.NewGuid(), WorkflowTemplateId: Guid.NewGuid(),
            WorkflowTemplateVersionId: Guid.NewGuid(), ApprovalTaskId: Guid.NewGuid(),
            AssignmentSnapshotId: Guid.NewGuid(), StartTransitionLogId: Guid.NewGuid(),
            WorkflowStartedAtUtcTicksV1: now + 5, ReleaseLease: true)));
        Assert.False(await repository.AdvanceAsync(makerReplay, new(
            FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 6,
            WorkflowInstanceId: Guid.NewGuid())));

        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted, stored!.Checkpoint);
        Assert.NotNull(stored.WorkflowInstanceId);
        Assert.Null(stored.ApprovalReferenceProofFingerprint);
        Assert.Null(stored.LeaseOwner);
    }

    [Fact]
    public async Task Recoverable_discovery_excludes_active_lease_and_pages_null_then_same_tick_by_operation_id()
    {
        var repository = Repository(_tenantId);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var nullDue = Operation("null", "fp-null", Guid.Parse("01000000-0000-0000-0000-000000000001"));
        var first = Operation("first", "fp-first", Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var second = Operation("second", "fp-second", Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var leased = Operation("leased", "fp-leased");
        foreach (var item in new[] { nullDue, first, second, leased }) Assert.True((await repository.ReserveAsync(item)).Succeeded);
        await _collection.UpdateManyAsync(x => x.TenantId == _tenantId && new[] { first.OperationId, second.OperationId }.Contains(x.OperationId),
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now));
        await _collection.UpdateOneAsync(x => x.TenantId == _tenantId && x.OperationId == leased.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now)
                .Set(x => x.LeaseOwner, "active").Set(x => x.LeaseUntilUtcTicksV1, now + 1000));

        var p1 = await repository.DiscoverRecoverableAsync(now, 1);
        var p2 = await repository.DiscoverRecoverableAsync(now, 1, p1.NextCursor);
        var p3 = await repository.DiscoverRecoverableAsync(now, 1, p2.NextCursor);
        Assert.Equal(nullDue.OperationId, Assert.Single(p1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(p2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(p3.Operations).OperationId);
    }

    [Fact]
    public async Task Approval_proof_is_durable_before_revision_mutation_and_cannot_be_overwritten_after_crash()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("approval-proof", "approval-proof-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == operation.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
                .Set(item => item.DecisionKind, ProductIdentityDecisionKind.Approved));

        var decisionClaim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.DecisionObserved],
            "approval-validator",
            now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(decisionClaim);
        var originalPack = Selection("pack-applicability", operation.PackApplicabilityCode);
        var originalUom = Selection("uom", operation.PackUomCode);
        var originalProof = new string('a', 64);
        Assert.True(await repository.AdvanceAsync(decisionClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ApprovalPackApplicabilitySelection: originalPack,
            ApprovalPackUomSelection: originalUom,
            ReferencesValidatedAtUtcTicksV1: now + 1,
            ApprovalReferenceProofFingerprint: originalProof,
            ReleaseLease: true)));

        var afterCrash = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated, afterCrash!.Checkpoint);
        Assert.Equal(originalProof, afterCrash.ApprovalReferenceProofFingerprint);
        Assert.Equal(originalPack.ValueCode, afterCrash.ApprovalPackApplicabilitySelection!.ValueCode);
        Assert.Equal(originalUom.ValueCode, afterCrash.ApprovalPackUomSelection!.ValueCode);

        var recoveryClaim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated],
            "approval-recovery",
            now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(recoveryClaim);
        Assert.False(await repository.AdvanceAsync(recoveryClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 3,
            ApprovalPackApplicabilitySelection: Selection("pack-applicability", "DRIFT"),
            ApprovalPackUomSelection: originalUom,
            ReferencesValidatedAtUtcTicksV1: now + 3,
            ApprovalReferenceProofFingerprint: new string('b', 64))));
        Assert.True(await repository.AdvanceAsync(recoveryClaim, new(
            FirstGskuIdentityWorkflowCheckpoint.RevisionApproved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 3,
            ReleaseLease: true)));

        var completedRevisionStep = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.RevisionApproved, completedRevisionStep!.Checkpoint);
        Assert.Equal(originalProof, completedRevisionStep.ApprovalReferenceProofFingerprint);
        Assert.Equal(originalPack.ValueCode, completedRevisionStep.ApprovalPackApplicabilitySelection!.ValueCode);
    }

    [Fact]
    public async Task Parent_dependency_manual_state_can_atomically_return_to_decision_observed_only_for_exact_failure()
    {
        var repository = Repository(_tenantId);
        var exact = Operation("parent-dependency", "parent-dependency-fingerprint");
        var legacyAuth = Operation("legacy-auth", "legacy-auth-fingerprint");
        var legacyForbidden = Operation("legacy-forbidden", "legacy-forbidden-fingerprint");
        var unrelated = Operation("unrelated-manual", "unrelated-manual-fingerprint");
        Assert.True((await repository.ReserveAsync(exact)).Succeeded);
        Assert.True((await repository.ReserveAsync(legacyAuth)).Succeeded);
        Assert.True((await repository.ReserveAsync(legacyForbidden)).Succeeded);
        Assert.True((await repository.ReserveAsync(unrelated)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var decisionFields = Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            .Set(item => item.RecoveryDisposition,
                ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            .Set(item => item.DecisionKind, ProductIdentityDecisionKind.Approved)
            .Set(item => item.LeaseOwner, null)
            .Set(item => item.LeaseUntilUtcTicksV1, null);
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == exact.OperationId,
            decisionFields.Set(item => item.LastFailureCode, "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED"));
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == legacyAuth.OperationId,
            decisionFields.Set(item => item.LastFailureCode, "REFERENCE_UNAUTHENTICATED"));
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == legacyForbidden.OperationId,
            decisionFields.Set(item => item.LastFailureCode, "REFERENCE_FORBIDDEN"));
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == unrelated.OperationId,
            decisionFields.Set(item => item.LastFailureCode, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT"));

        var exactClaim = await repository.TryClaimAsync(new(
            exact.OperationId, exact.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker", now, now + TimeSpan.FromMinutes(1).Ticks));
        var unrelatedClaim = await repository.TryClaimAsync(new(
            unrelated.OperationId, unrelated.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker", now, now + TimeSpan.FromMinutes(1).Ticks));
        var legacyAuthClaim = await repository.TryClaimAsync(new(
            legacyAuth.OperationId, legacyAuth.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker", now, now + TimeSpan.FromMinutes(1).Ticks));
        var legacyForbiddenClaim = await repository.TryClaimAsync(new(
            legacyForbidden.OperationId, legacyForbidden.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired],
            "interactive-maker", now, now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(exactClaim);
        Assert.NotNull(legacyAuthClaim);
        Assert.NotNull(legacyForbiddenClaim);
        Assert.NotNull(unrelatedClaim);

        var exactAdvanced = await repository.AdvanceAsync(exactClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ReleaseLease: true));
        var unrelatedAdvanced = await repository.AdvanceAsync(unrelatedClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ReleaseLease: true));
        var legacyAuthAdvanced = await repository.AdvanceAsync(legacyAuthClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ReleaseLease: true));
        var legacyForbiddenAdvanced = await repository.AdvanceAsync(legacyForbiddenClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ReleaseLease: true));

        Assert.True(exactAdvanced);
        Assert.True(legacyAuthAdvanced);
        Assert.False(legacyForbiddenAdvanced);
        var forbiddenStored = await repository.GetByOperationIdAsync(legacyForbidden.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, forbiddenStored!.Checkpoint);
        Assert.Equal("REFERENCE_FORBIDDEN", forbiddenStored.LastFailureCode);
        Assert.False(unrelatedAdvanced);
        var exactStored = await repository.GetByOperationIdAsync(exact.OperationId);
        var unrelatedStored = await repository.GetByOperationIdAsync(unrelated.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, exactStored!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, exactStored.RecoveryDisposition);
        Assert.Null(exactStored.LastFailureCode);
        Assert.Null(exactStored.LeaseOwner);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            unrelatedStored!.Checkpoint);
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", unrelatedStored.LastFailureCode);
    }

    [Fact]
    public async Task Withdrawal_transaction_updates_both_pair_members_and_operation_or_none()
    {
        var repository = Repository(_tenantId);
        var now = DateTimeOffset.UtcNow;
        var commandId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var operation = Operation("withdraw-pair", "withdraw-fingerprint");
        operation.WorkflowInstanceId = workflowId;
        operation.ApprovalTaskId = taskId;
        operation.WorkflowTemplateVersionId = Guid.NewGuid();
        operation.MakerSubjectId = makerId;
        operation.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved;
        operation.WithdrawalCommandId = commandId;
        operation.WithdrawalFingerprint = "withdrawal-fingerprint";
        operation.WithdrawalRequesterSubjectId = makerId;
        operation.WithdrawalExpectedGskuVersion = 1;
        operation.WithdrawalReasonCode = "REQUESTER_WITHDRAWAL";
        operation.WithdrawalExpectedWorkflowInstanceVersion = 3;
        operation.WithdrawalExpectedApprovalTaskVersion = 5;
        operation.WithdrawalTransitionLogId = Guid.NewGuid();
        operation.WithdrawalObservedAtUtcTicksV1 = now.UtcTicks;
        operation.WithdrawalTransitionSequence = 3;
        operation.WithdrawalResultWorkflowInstanceVersion = 4;
        operation.WithdrawalResultApprovalTaskVersion = 6;
        operation.WithdrawalTaskStatus = "Cancelled";
        operation.WithdrawalInstanceStatus = "Cancelled";
        operation.WithdrawalObjectRef = operation.ObjectRef;
        operation.LeaseOwner = "withdraw-worker";
        operation.LeaseGeneration = 1;
        operation.LeaseUntilUtcTicksV1 = now.AddMinutes(1).UtcTicks;
        operation.Id = Guid.NewGuid();
        operation.CreatedAt = now;
        operation.UpdatedAt = now;
        operation.UpdatedAtUtcTicksV1 = now.UtcTicks;
        operation.IsDeleted = false;

        var binding = new FirstGskuIdentityWorkflowBinding
        {
            WorkflowInstanceId = workflowId,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId.Value,
            ApprovalTaskId = taskId,
            AssignmentSnapshotId = Guid.NewGuid(),
            StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "gsku",
            GskuId = operation.GskuId,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = makerId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = now
        };
        var revision = new ProductDefinitionRevision
        {
            Id = operation.ProductDefinitionRevisionId, TenantId = _tenantId,
            GlobalProductId = operation.GlobalProductId, RevisionIdentifier = "REV-001",
            CreationCommandId = operation.CreationCommandId, Version = 1,
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = binding, CreatedAt = now, UpdatedAt = now
        };
        var gsku = new Gsku
        {
            Id = operation.GskuId, TenantId = _tenantId,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId,
            CanonicalCode = "GS-WITHDRAW", CreationCommandId = operation.CreationCommandId,
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            PackApplicabilitySelection = operation.PackApplicabilitySelection,
            PackUomSelection = operation.PackUomSelection, Version = 1,
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = binding, CreatedAt = now, UpdatedAt = now
        };
        await _collection.InsertOneAsync(operation);
        await _revisions.InsertOneAsync(revision);
        await _gskus.InsertOneAsync(gsku);

        var evidence = new ProductIdentityWorkflowCancellationEvidence
        {
            WorkflowInstanceId = workflowId, ApprovalTaskId = taskId,
            WorkflowTemplateId = operation.WorkflowTemplateId.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId.Value,
            ObjectType = "gsku", ObjectId = operation.GskuId, ObjectRef = operation.ObjectRef,
            RequesterSubjectId = makerId, ReasonCode = "REQUESTER_WITHDRAWAL",
            CancelledAtUtc = now, TransitionSequence = 3,
            TransitionLogId = operation.WithdrawalTransitionLogId.Value,
            TaskStatus = "Cancelled", InstanceStatus = "Cancelled",
            WorkflowInstanceVersion = 4, ApprovalTaskVersion = 6,
            IdempotencyKey = commandId.ToString("D")
        };
        var revisionAudit = WithdrawalAudit(
            operation.ProductDefinitionRevisionId, AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn, 1, commandId, makerId, now);
        var gskuAudit = WithdrawalAudit(
            operation.GskuId, AuditAggregateType.Gsku,
            ProductAuditOperation.GskuIdentityApprovalWithdrawn, 1, commandId, makerId, now);
        var claim = new FirstGskuIdentityWorkflowClaim(
            _tenantId, operation.OperationId, operation.OperationFingerprint, "withdraw-worker", 1,
            FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved, operation.LeaseUntilUtcTicksV1.Value);

        var first = await repository.ApplyWithdrawalAsync(
            claim, operation, evidence, revisionAudit, gskuAudit, now.AddSeconds(1).UtcTicks);
        var replay = await repository.ApplyWithdrawalAsync(
            claim, operation, evidence, revisionAudit, gskuAudit, now.AddSeconds(2).UtcTicks);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, first.Revision!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, first.Gsku!.LifecycleStatus);
        Assert.Equal(2, first.Revision.Version);
        Assert.Equal(2, first.Gsku.Version);
        Assert.Single(first.Revision.AuditIntents,
            x => x.IdempotencyKey == commandId.ToString("D"));
        Assert.Single(first.Gsku.AuditIntents,
            x => x.IdempotencyKey == commandId.ToString("D"));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
    }

    // ── GSKU FIX1 on the real store ──────────────────────────────────────────────────────────────────────────────

    private async Task<(FirstGskuIdentityWorkflowOperation Operation, FirstGskuIdentityWorkflowClaim Claim)> SeedPairAsync(
        FirstGskuIdentityWorkflowCheckpoint checkpoint, int gskuVersion = 1)
    {
        var now = DateTimeOffset.UtcNow;
        var makerId = Guid.NewGuid();
        var operation = Operation($"seed-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}");
        operation.Id = Guid.NewGuid();
        operation.WorkflowInstanceId = Guid.NewGuid();
        operation.ApprovalTaskId = Guid.NewGuid();
        operation.WorkflowTemplateVersionId = Guid.NewGuid();
        operation.MakerSubjectId = makerId;
        operation.Checkpoint = checkpoint;
        operation.DecisionKind = ProductIdentityDecisionKind.Approved;
        operation.DecisionActorSubjectId = Guid.NewGuid();
        operation.LeaseOwner = "worker";
        operation.LeaseGeneration = 1;
        operation.LeaseUntilUtcTicksV1 = now.AddMinutes(1).UtcTicks;
        operation.CreatedAt = now;
        operation.UpdatedAt = now;
        operation.UpdatedAtUtcTicksV1 = now.UtcTicks;
        var binding = new FirstGskuIdentityWorkflowBinding
        {
            WorkflowInstanceId = operation.WorkflowInstanceId.Value,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId.Value,
            ApprovalTaskId = operation.ApprovalTaskId.Value,
            AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "gsku", GskuId = operation.GskuId,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId,
            ObjectRef = operation.ObjectRef, SubmitterSubjectId = makerId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint, SubmittedAtUtc = now
        };
        await _collection.InsertOneAsync(operation);
        await _revisions.InsertOneAsync(new ProductDefinitionRevision
        {
            Id = operation.ProductDefinitionRevisionId, TenantId = _tenantId,
            GlobalProductId = operation.GlobalProductId, RevisionIdentifier = "REV-001",
            CreationCommandId = operation.CreationCommandId, Version = 1,
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = binding, CreatedAt = now, UpdatedAt = now
        });
        await _gskus.InsertOneAsync(new Gsku
        {
            Id = operation.GskuId, TenantId = _tenantId,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId,
            CanonicalCode = $"GS-{operation.GskuId:N}"[..20], CreationCommandId = operation.CreationCommandId,
            CodeReservationId = Guid.NewGuid(), // unique per tenant
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            PackApplicabilitySelection = operation.PackApplicabilitySelection,
            PackUomSelection = operation.PackUomSelection, Version = gskuVersion,
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = binding, CreatedAt = now, UpdatedAt = now
        });
        var claim = new FirstGskuIdentityWorkflowClaim(_tenantId, operation.OperationId,
            operation.OperationFingerprint, "worker", 1, checkpoint, operation.LeaseUntilUtcTicksV1.Value);
        return (operation, claim);
    }

    private LocalAuditIntent NotApplicableAudit(
        Guid aggregateId, AuditAggregateType type, ProductAuditOperation auditOperation, Guid operationId) =>
        WithdrawalAudit(aggregateId, type, auditOperation, 1, operationId, Guid.NewGuid(), DateTimeOffset.UtcNow) is var intent
            ? Rekey(intent, FirstGskuIdentityDecisionNotApplicable.Key(operationId))
            : null!;

    private static LocalAuditIntent Rekey(LocalAuditIntent intent, string key)
    {
        intent.IdempotencyKey = key;
        intent.CommandId = key;
        intent.CorrelationId = key;
        return intent;
    }

    // B2 (b): "the engine approved, MDM could not apply" — both drafts, both intents, the operation final: all or none.
    [Fact]
    public async Task DecisionNotApplicable_returns_both_pair_members_to_draft_and_ends_the_operation_in_one_transaction()
    {
        var repository = Repository(_tenantId);
        var (operation, claim) = await SeedPairAsync(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved);

        var result = await repository.ApplyDecisionNotApplicableAsync(claim, operation,
            "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED",
            NotApplicableAudit(operation.ProductDefinitionRevisionId, AuditAggregateType.ProductDefinitionRevision,
                ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable, operation.OperationId),
            NotApplicableAudit(operation.GskuId, AuditAggregateType.Gsku,
                ProductAuditOperation.GskuIdentityDecisionNotApplicable, operation.OperationId),
            DateTimeOffset.UtcNow.UtcTicks);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal((ProductIdentityLifecycleStatus.Draft, 2), (result.Revision!.LifecycleStatus, result.Revision.Version));
        Assert.Equal((ProductIdentityLifecycleStatus.Draft, 2), (result.Gsku!.LifecycleStatus, result.Gsku.Version));
        Assert.Single(result.Gsku.AuditIntents, item => item.Operation == ProductAuditOperation.GskuIdentityDecisionNotApplicable);
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, stored.Checkpoint);
        Assert.Equal("FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED", stored.LastFailureCode);
        Assert.Null(stored.LeaseOwner);
        Assert.Equal(FirstGskuIdentitySubmitStateKind.None,
            (await repository.GetSubmitStateAsync(operation.GskuId, operation.ExpectedGskuVersion + 1)).Kind);
    }

    [Fact]
    public async Task DecisionNotApplicable_writes_nothing_when_one_pair_member_moved()
    {
        var repository = Repository(_tenantId);
        var (operation, claim) = await SeedPairAsync(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, gskuVersion: 7);

        var result = await repository.ApplyDecisionNotApplicableAsync(claim, operation, "REFERENCE_DATA_CONTRACT_CONFLICT",
            NotApplicableAudit(operation.ProductDefinitionRevisionId, AuditAggregateType.ProductDefinitionRevision,
                ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable, operation.OperationId),
            NotApplicableAudit(operation.GskuId, AuditAggregateType.Gsku,
                ProductAuditOperation.GskuIdentityDecisionNotApplicable, operation.OperationId),
            DateTimeOffset.UtcNow.UtcTicks);

        Assert.False(result.Succeeded);
        var revision = await _revisions.Find(x => x.Id == operation.ProductDefinitionRevisionId).SingleAsync();
        Assert.Equal((ProductIdentityLifecycleStatus.PendingIdentityApproval, 1), (revision.LifecycleStatus, revision.Version));
        Assert.Empty(revision.AuditIntents);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
    }

    // B1: forgetting a withdrawal clears every field a withdrawal writes — through the explicit flag only.
    [Fact]
    public async Task Forgetting_a_withdrawal_clears_all_eighteen_withdrawal_fields()
    {
        var repository = Repository(_tenantId);
        var (operation, claim) = await SeedPairAsync(FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        var update = Builders<FirstGskuIdentityWorkflowOperation>.Update.Combine(
            typeof(FirstGskuIdentityWorkflowOperation).GetProperties()
                .Where(property => property.Name.StartsWith("Withdrawal", StringComparison.Ordinal))
                .Select(property => Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(property.Name,
                    property.PropertyType == typeof(string) ? (object)"x"
                    : Nullable.GetUnderlyingType(property.PropertyType) == typeof(Guid) ? Guid.NewGuid()
                    : Nullable.GetUnderlyingType(property.PropertyType) == typeof(long) ? 1L : (object)1)));
        await _collection.UpdateOneAsync(x => x.OperationId == operation.OperationId, update);

        Assert.True(await repository.AdvanceAsync(claim, new(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None, DateTimeOffset.UtcNow.UtcTicks,
            ReleaseLease: true, ClearWithdrawal: true)));

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        var fields = typeof(FirstGskuIdentityWorkflowOperation).GetProperties()
            .Where(property => property.Name.StartsWith("Withdrawal", StringComparison.Ordinal)).ToList();
        Assert.Equal(18, fields.Count);
        Assert.All(fields, property => Assert.True(property.GetValue(stored) is null, property.Name));
    }

    // B9: the requester who is present is not paced by the worker's schedule (never by a held lease).
    [Fact]
    public async Task An_interactive_claim_passes_the_workers_retry_schedule_and_the_workers_does_not()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("schedule", "schedule-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(x => x.OperationId == operation.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1,
                now + TimeSpan.FromSeconds(30).Ticks));

        var worker = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.Prepared], "worker", now, now + TimeSpan.FromMinutes(1).Ticks));
        var requester = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.Prepared], "requester", now, now + TimeSpan.FromMinutes(1).Ticks,
            IgnoreRetrySchedule: true));
        var second = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.Prepared], "requester-2", now + 1, now + TimeSpan.FromMinutes(1).Ticks,
            IgnoreRetrySchedule: true));

        Assert.Null(worker);
        Assert.NotNull(requester);
        Assert.Null(second); // the lease is held
    }

    // A withdrawal an earlier version parked in AwaitingMakerReplay is found; an ordinary one is not.
    [Fact]
    public async Task Discovery_includes_a_parked_withdrawal_and_not_an_ordinary_maker_replay()
    {
        var repository = Repository(_tenantId);
        var (parked, _) = await SeedPairAsync(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        var (ordinary, _) = await SeedPairAsync(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        await _collection.UpdateManyAsync(x => x.TenantId == _tenantId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.LeaseOwner, null)
                .Set(x => x.LeaseUntilUtcTicksV1, null));
        await _collection.UpdateOneAsync(x => x.OperationId == parked.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.WithdrawalCommandId, Guid.NewGuid()));

        var listed = (await repository.DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10))
            .Operations.Select(x => x.OperationId).ToList();

        Assert.Contains(parked.OperationId, listed);
        Assert.DoesNotContain(ordinary.OperationId, listed);
    }

    // B7 + FIX2 item 1 (changed): FIX1 counted every non-final checkpoint as "unfinished" and froze the draft for good
    // when a submit never reached the engine (AwaitingMakerReplay) or waited for reconciliation. Now each state has
    // its own way out: in flight waits for the worker; not received → resubmit or edit; reconciliation → the
    // submitter's repeat; ended at this version → edit. Tenant-bound. (BL-527, FIX3 S9: the two classifications FIX2
    // added here — WorkflowStarted, AbandonedBeforeWorkflowStart — moved to the HTTP host's throwaway mongod,
    // GskuFix3AddendumTests.S9_The_latest_submit_is_classified_on_the_real_store.)
    [Theory]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown, false, FirstGskuIdentitySubmitStateKind.InFlight)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, true, FirstGskuIdentitySubmitStateKind.ResubmitRequired)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, false, FirstGskuIdentitySubmitStateKind.ReconciliationRequired)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.Completed, false, FirstGskuIdentitySubmitStateKind.None)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, false, FirstGskuIdentitySubmitStateKind.None)]
    public async Task The_latest_submit_of_a_gsku_is_classified_with_its_way_out(
        FirstGskuIdentityWorkflowCheckpoint checkpoint, bool neverStarted, FirstGskuIdentitySubmitStateKind expected)
    {
        var repository = Repository(_tenantId);
        var (operation, _) = await SeedPairAsync(checkpoint);
        if (neverStarted)
            await _collection.UpdateOneAsync(x => x.OperationId == operation.OperationId,
                Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.WorkflowInstanceId, null));

        Assert.Equal(expected, (await repository.GetSubmitStateAsync(operation.GskuId, operation.ExpectedGskuVersion)).Kind);
        Assert.Equal(FirstGskuIdentitySubmitStateKind.None,
            (await Repository(_tenantBId).GetSubmitStateAsync(operation.GskuId, operation.ExpectedGskuVersion)).Kind); // tenant-bound
    }

    // The index-shape test lives in the MDM HTTP test project (GskuIndexShapeHttpTests): it counts indexes on a
    // throwaway mongod, never on the shared test database (which collects every branch's indexes) and without a
    // per-run database there (the MongoTestDatabaseGuard forbids it).

    public async Task DisposeAsync()
    {
        await _collection.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _tenantBId);
        await _revisions.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _tenantBId);
        await _gskus.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _tenantBId);
    }

    private FirstGskuIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private FirstGskuIdentityWorkflowOperation Operation(
        string key, string fingerprint, Guid? operationId = null, Guid? revisionId = null, Guid? gskuId = null)
    {
        var revision = revisionId ?? Guid.NewGuid();
        var gsku = gskuId ?? Guid.NewGuid();
        return new()
        {
            TenantId = _tenantId, OperationId = operationId ?? Guid.NewGuid(),
            ProductDefinitionRevisionId = revision, GskuId = gsku, GlobalProductId = Guid.NewGuid(),
            CreationCommandId = $"create-{key}", ExpectedRevisionVersion = 0, ExpectedGskuVersion = 0,
            MakerSubjectId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "FIRST_GSKU_IDENTITY_APPROVAL",
            ObjectType = "gsku", ObjectId = gsku.ToString("D"), ObjectRef = $"GS:{gsku:D}",
            StartIdempotencyKey = key, OperationFingerprint = fingerprint,
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            PackApplicabilitySelection = Selection("pack-applicability", "PACK"),
            PackUomSelection = Selection("uom", "EA"), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private static ReferenceCatalogSelection Selection(string set, string code) => new()
    {
        SetCode = set, ValueCode = code, CatalogVersionId = Guid.NewGuid(), CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Pinned,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private LocalAuditIntent WithdrawalAudit(
        Guid aggregateId,
        AuditAggregateType aggregateType,
        ProductAuditOperation operation,
        int preVersion,
        Guid commandId,
        Guid makerId,
        DateTimeOffset timestamp) => new()
    {
        IntentId = Guid.NewGuid(), TenantId = _tenantId,
        AggregateType = aggregateType, AggregateId = aggregateId,
        PreVersion = preVersion, PostVersion = preVersion + 1,
        Operation = operation, ActorId = makerId.ToString("D"),
        CorrelationId = commandId.ToString("D"), CausationId = commandId.ToString("D"),
        CommandId = commandId.ToString("D"), Sequence = preVersion + 2,
        TimestampUtc = timestamp, TimestampUtcTicksV1 = timestamp.UtcTicks,
        EvidenceHash = $"EVIDENCE-{aggregateType}",
        IdempotencyKey = commandId.ToString("D"), DeliveryState = AuditIntentDeliveryState.Pending
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
