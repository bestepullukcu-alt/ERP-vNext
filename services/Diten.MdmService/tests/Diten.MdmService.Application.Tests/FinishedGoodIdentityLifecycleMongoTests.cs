using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FinishedGoodIdentityLifecycleMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<FinishedGood> _collection = null!;
    private IMongoCollection<Gsku> _gskus = null!;

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
        _collection = _database.GetCollection<FinishedGood>("mdm_finished_goods");
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Submit_approve_and_compacted_receipt_replay_are_atomic_and_tenant_fenced()
    {
        var finishedGood = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository(_tenantId);
        var binding = Binding(finishedGood);
        var submitAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateSubmit(finishedGood, 0, binding);
        var submitted = await repository.SubmitIdentityAsync(finishedGood.Id, 0, binding, submitAudit);
        Assert.True(submitted.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, submitted.FinishedGood!.LifecycleStatus);
        Assert.Equal(1, submitted.FinishedGood.Version);

        var evidence = Decision(binding, ProductIdentityDecisionKind.Approved);
        var approvalAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateDecision(
            submitted.FinishedGood, 1, evidence);
        var approved = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, approvalAudit);
        Assert.True(approved.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, approved.FinishedGood!.LifecycleStatus);
        Assert.Equal(2, approved.FinishedGood.Version);

        var receipt = Receipt(approvalAudit);
        var compacted = await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.Id == finishedGood.Id
                && x.AuditIntents.Any(i => i.IntentId == approvalAudit.IntentId),
            Builders<FinishedGood>.Update
                .PullFilter(x => x.AuditIntents, i => i.IntentId == approvalAudit.IntentId)
                .Push(x => x.AuditIntentReceipts, receipt));
        Assert.Equal(1, compacted.ModifiedCount);
        var replay = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, approvalAudit);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(2, replay.FinishedGood!.Version);
        Assert.Null(await Repository(_otherTenantId).GetByIdAsync(finishedGood.Id));
    }

    [Fact]
    public async Task Reject_returns_to_draft_and_exact_replay_drift_conflicts()
    {
        var finishedGood = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository(_tenantId);
        var binding = Binding(finishedGood);
        var submit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateSubmit(finishedGood, 0, binding);
        var pending = (await repository.SubmitIdentityAsync(finishedGood.Id, 0, binding, submit)).FinishedGood!;
        var evidence = Decision(binding, ProductIdentityDecisionKind.Rejected);
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateDecision(pending, 1, evidence);
        var rejected = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, audit);
        var replay = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, audit);
        evidence.ReasonCode = "DIFFERENT";
        var drift = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, audit);

        Assert.Equal(ProductIdentityLifecycleStatus.Draft, rejected.FinishedGood!.LifecycleStatus);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Equal(2, rejected.FinishedGood.Version);
    }

    [Fact]
    public async Task Decision_full_binding_drift_is_fenced_before_mutation()
    {
        var finishedGood = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository(_tenantId);
        var binding = Binding(finishedGood);
        var submit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateSubmit(finishedGood, 0, binding);
        var pending = (await repository.SubmitIdentityAsync(finishedGood.Id, 0, binding, submit)).FinishedGood!;
        var evidence = Decision(binding, ProductIdentityDecisionKind.Approved);
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateDecision(pending, 1, evidence);

        await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.Id == finishedGood.Id,
            Builders<FinishedGood>.Update.Set(
                x => x.IdentityWorkflowBinding!.StartRequestFingerprint, new string('b', 64)));
        var result = await repository.ReconcileIdentityDecisionAsync(
            finishedGood.Id, 1, binding, evidence, audit);
        var stored = await repository.GetByIdAsync(finishedGood.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("WORKFLOW_BINDING_CONFLICT", result.ErrorCode);
        Assert.Equal(1, stored!.Version);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, stored.LifecycleStatus);
        Assert.Null(stored.IdentityWorkflowBinding!.TerminalDecision);
        Assert.DoesNotContain(stored.AuditIntents, item => item.IntentId == audit.IntentId);
    }

    [Fact]
    public async Task Retire_is_versioned_exactly_replayable_and_soft_deleted_rows_are_not_mutated()
    {
        var finishedGood = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var repository = Repository(_tenantId);
        var operationId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            finishedGood, 2, operationId, actor, "SUPERSEDED", "bounded", DateTimeOffset.UtcNow);
        var retired = await repository.RetireIdentityAsync(finishedGood.Id, 2, audit);
        var replay = await repository.RetireIdentityAsync(finishedGood.Id, 2, audit);
        Assert.True(retired.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retired.FinishedGood!.LifecycleStatus);
        Assert.Equal(3, retired.FinishedGood.Version);

        var deleted = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2, deleted: true);
        var deletedAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            deleted, 2, Guid.NewGuid(), actor, "SUPERSEDED", null, DateTimeOffset.UtcNow);
        var denied = await repository.RetireIdentityAsync(deleted.Id, 2, deletedAudit);
        Assert.False(denied.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_NOT_FOUND", denied.ErrorCode);
    }

    [Fact]
    public async Task Lifecycle_write_rejects_stale_version_and_document_at_one_megabyte_fence()
    {
        var stale = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var repository = Repository(_tenantId);
        var staleAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            stale, 1, Guid.NewGuid(), Guid.NewGuid(), "SUPERSEDED", null, DateTimeOffset.UtcNow);
        Assert.Equal("FINISHED_GOOD_IDENTITY_VERSION_CONFLICT",
            (await repository.RetireIdentityAsync(stale.Id, 1, staleAudit)).ErrorCode);

        var oversized = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var oversizedReceipt = new LocalAuditIntentReceipt
        {
            IntentId = Guid.NewGuid(), TenantId = _tenantId, SourceService = "Diten.MdmService",
            IdempotencyKey = "seed", EvidenceHash = "hash",
            CentralAcknowledgement = new string('x', 1024 * 1024),
            CentralIdempotencyKey = "central", ContractVersion = "v1",
            AcknowledgedAt = DateTimeOffset.UtcNow, DeliveredAt = DateTimeOffset.UtcNow,
            CompactedAt = DateTimeOffset.UtcNow, CompactReceiptReference = "receipt"
        };
        await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.Id == oversized.Id,
            Builders<FinishedGood>.Update.Set(x => x.AuditIntentReceipts,
                new List<LocalAuditIntentReceipt> { oversizedReceipt }));
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            oversized, 2, Guid.NewGuid(), Guid.NewGuid(), "SUPERSEDED", null, DateTimeOffset.UtcNow);
        var result = await repository.RetireIdentityAsync(oversized.Id, 2, audit);
        Assert.False(result.Succeeded);
        Assert.Equal("FINISHED_GOOD_DOCUMENT_SIZE_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task Finished_good_retire_CAS_clears_GSKU_blocker_without_cascade()
    {
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId,
            ProductDefinitionRevisionId = Guid.NewGuid(), CanonicalCode = $"GS-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 0
        };
        await _gskus.InsertOneAsync(gsku);
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = gsku.Id,
            CanonicalCode = $"FG-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2
        };
        await _collection.InsertOneAsync(finishedGood);
        var gskuRepository = new GskuRepository(_database, new Tenant(_tenantId));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await gskuRepository.FindRetirementBlockerAsync(gsku.Id));

        var finishedGoodAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            finishedGood, 2, Guid.NewGuid(), Guid.NewGuid(), "SUPERSEDED", null, DateTimeOffset.UtcNow);
        var finishedGoodRetired = await Repository(_tenantId).RetireIdentityAsync(
            finishedGood.Id, 2, finishedGoodAudit);
        Assert.True(finishedGoodRetired.Succeeded);
        Assert.Null(await gskuRepository.FindRetirementBlockerAsync(gsku.Id));

        var operationId = Guid.NewGuid();
        const string fingerprint = "finished-good-blocker-cleared";
        var fence = await gskuRepository.CloseChildAdmissionFenceAsync(
            gsku.Id, 0, operationId, fingerprint);
        Assert.True(fence.Succeeded);
        var fencedGsku = fence.Aggregate!;
        var gskuAudit = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenantId, AggregateType = AuditAggregateType.Gsku,
            AggregateId = gsku.Id, PreVersion = fencedGsku.Version, PostVersion = fencedGsku.Version + 1,
            Operation = ProductAuditOperation.GskuIdentityRetired, ActorId = Guid.NewGuid().ToString("D"),
            CorrelationId = operationId.ToString("D"), CausationId = operationId.ToString("D"),
            CommandId = operationId.ToString("D"), Sequence = fencedGsku.Version + 1,
            TimestampUtc = DateTimeOffset.UtcNow, EvidenceHash = "finished-good-cleared",
            IdempotencyKey = operationId.ToString("D")
        };
        var gskuRetired = await gskuRepository.RetireIdentityAsync(
            gsku.Id, fencedGsku.Version, operationId, fingerprint, gskuAudit);
        var storedFinishedGood = await Repository(_tenantId).GetByIdAsync(finishedGood.Id);

        Assert.True(gskuRetired.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, gskuRetired.Aggregate!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, storedFinishedGood!.LifecycleStatus);
        Assert.Equal(3, storedFinishedGood.Version);
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId && x.Id == finishedGood.Id).ToListAsync());
    }

    public async Task DisposeAsync()
    {
        await _collection.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
        await _gskus.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
    }

    private FinishedGoodRepository Repository(Guid tenantId) => new(_database, new Tenant(tenantId));

    private async Task<FinishedGood> SeedAsync(
        ProductIdentityLifecycleStatus status, int version = 0, bool deleted = false)
    {
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = Guid.NewGuid(),
            CanonicalCode = $"FG-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = status,
            Version = version, IsDeleted = deleted, DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _collection.InsertOneAsync(finishedGood);
        return finishedGood;
    }

    private static ProductIdentityWorkflowBinding Binding(FinishedGood finishedGood) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "finished-good", ObjectId = finishedGood.Id, ObjectRef = finishedGood.CanonicalCode,
        SubmitterSubjectId = Guid.NewGuid(), StartIdempotencyKey = $"start-{Guid.NewGuid():N}",
        StartRequestFingerprint = new string('a', 64), SubmittedAtUtc = DateTimeOffset.UtcNow
    };

    private static ProductIdentityWorkflowDecisionEvidence Decision(
        ProductIdentityWorkflowBinding binding, ProductIdentityDecisionKind decision) => new()
    {
        Decision = decision, WorkflowInstanceId = binding.WorkflowInstanceId,
        ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
        WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = binding.ObjectType,
        ObjectId = binding.ObjectId, ObjectRef = binding.ObjectRef,
        DecisionActorSubjectId = Guid.NewGuid(), ReasonCode = decision == ProductIdentityDecisionKind.Rejected
            ? "REJECTED" : null, DecisionAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 2,
        TaskStatus = decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        InstanceStatus = decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected"
    };

    private static LocalAuditIntentReceipt Receipt(LocalAuditIntent intent) => new()
    {
        IntentId = intent.IntentId, TenantId = intent.TenantId, SourceService = intent.SourceService,
        IdempotencyKey = intent.IdempotencyKey, EvidenceHash = intent.EvidenceHash,
        CentralAcknowledgement = "ack", CentralIdempotencyKey = intent.IdempotencyKey,
        ContractVersion = "v1", AcknowledgedAt = DateTimeOffset.UtcNow,
        DeliveredAt = DateTimeOffset.UtcNow, CompactedAt = DateTimeOffset.UtcNow,
        CompactReceiptReference = $"receipt/{intent.IntentId:D}"
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
