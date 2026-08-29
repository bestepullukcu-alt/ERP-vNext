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
public sealed class LskuIdentityLifecycleMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<Lsku> _collection = null!;

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
        _collection = _database.GetCollection<Lsku>("mdm_lskus");
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Submit_approve_and_compacted_receipt_replay_are_atomic_and_tenant_fenced()
    {
        var lsku = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository(_tenantId);
        var binding = Binding(lsku);
        var submitAudit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmit(lsku, 0, binding);
        var submitted = await repository.SubmitIdentityAsync(lsku.Id, 0, binding, submitAudit);
        Assert.True(submitted.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, submitted.Lsku!.LifecycleStatus);
        Assert.Equal(1, submitted.Lsku.Version);

        var evidence = Decision(binding, ProductIdentityDecisionKind.Approved);
        var approvalAudit = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(
            submitted.Lsku, 1, evidence);
        var approved = await repository.ReconcileIdentityDecisionAsync(lsku.Id, 1, evidence, approvalAudit);
        Assert.True(approved.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, approved.Lsku!.LifecycleStatus);
        Assert.Equal(2, approved.Lsku.Version);

        var receipt = Receipt(approvalAudit);
        var compacted = await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.Id == lsku.Id
                && x.AuditIntents.Any(i => i.IntentId == approvalAudit.IntentId),
            Builders<Lsku>.Update
                .PullFilter(x => x.AuditIntents, i => i.IntentId == approvalAudit.IntentId)
                .Push(x => x.AuditIntentReceipts, receipt));
        Assert.Equal(1, compacted.ModifiedCount);
        var replay = await repository.ReconcileIdentityDecisionAsync(lsku.Id, 1, evidence, approvalAudit);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(2, replay.Lsku!.Version);
        Assert.Null(await Repository(_otherTenantId).GetByIdAsync(lsku.Id));
    }

    [Fact]
    public async Task Reject_returns_to_draft_and_exact_replay_drift_conflicts()
    {
        var lsku = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository(_tenantId);
        var binding = Binding(lsku);
        var submit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmit(lsku, 0, binding);
        var pending = (await repository.SubmitIdentityAsync(lsku.Id, 0, binding, submit)).Lsku!;
        var evidence = Decision(binding, ProductIdentityDecisionKind.Rejected);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(pending, 1, evidence);
        var rejected = await repository.ReconcileIdentityDecisionAsync(lsku.Id, 1, evidence, audit);
        var replay = await repository.ReconcileIdentityDecisionAsync(lsku.Id, 1, evidence, audit);
        evidence.ReasonCode = "DIFFERENT";
        var drift = await repository.ReconcileIdentityDecisionAsync(lsku.Id, 1, evidence, audit);

        Assert.Equal(ProductIdentityLifecycleStatus.Draft, rejected.Lsku!.LifecycleStatus);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("LSKU_IDENTITY_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Equal(2, rejected.Lsku.Version);
    }

    [Fact]
    public async Task Retire_is_versioned_exactly_replayable_and_soft_deleted_rows_are_not_mutated()
    {
        var lsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var repository = Repository(_tenantId);
        var operationId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetire(
            lsku, 2, operationId, actor, "SUPERSEDED", "bounded", DateTimeOffset.UtcNow);
        var retired = await repository.RetireIdentityAsync(lsku.Id, 2, audit);
        var replay = await repository.RetireIdentityAsync(lsku.Id, 2, audit);
        Assert.True(retired.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retired.Lsku!.LifecycleStatus);
        Assert.Equal(3, retired.Lsku.Version);

        var deleted = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2, deleted: true);
        var deletedAudit = LskuIdentityLifecycleAuditIntentFactory.CreateRetire(
            deleted, 2, Guid.NewGuid(), actor, "SUPERSEDED", null, DateTimeOffset.UtcNow);
        var denied = await repository.RetireIdentityAsync(deleted.Id, 2, deletedAudit);
        Assert.False(denied.Succeeded);
        Assert.Equal("LSKU_IDENTITY_NOT_FOUND", denied.ErrorCode);
    }

    [Fact]
    public async Task Lifecycle_write_rejects_stale_version_and_document_at_one_megabyte_fence()
    {
        var stale = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var repository = Repository(_tenantId);
        var staleAudit = LskuIdentityLifecycleAuditIntentFactory.CreateRetire(
            stale, 1, Guid.NewGuid(), Guid.NewGuid(), "SUPERSEDED", null, DateTimeOffset.UtcNow);
        Assert.Equal("LSKU_IDENTITY_VERSION_CONFLICT",
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
            Builders<Lsku>.Update.Set(x => x.AuditIntentReceipts,
                new List<LocalAuditIntentReceipt> { oversizedReceipt }));
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetire(
            oversized, 2, Guid.NewGuid(), Guid.NewGuid(), "SUPERSEDED", null, DateTimeOffset.UtcNow);
        var result = await repository.RetireIdentityAsync(oversized.Id, 2, audit);
        Assert.False(result.Succeeded);
        Assert.Equal("LSKU_DOCUMENT_SIZE_LIMIT_EXCEEDED", result.ErrorCode);
    }

    public async Task DisposeAsync() => await _collection.DeleteManyAsync(
        x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);

    private LskuRepository Repository(Guid tenantId) => new(_database, new Tenant(tenantId));

    private async Task<Lsku> SeedAsync(
        ProductIdentityLifecycleStatus status, int version = 0, bool deleted = false)
    {
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = Guid.NewGuid(),
            CanonicalCode = $"LS-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), MarketCode = "TR", LifecycleStatus = status,
            Version = version, IsDeleted = deleted, DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
            MarketSelection = new ReferenceCatalogSelection
            {
                SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
                CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };
        await _collection.InsertOneAsync(lsku);
        return lsku;
    }

    private static ProductIdentityWorkflowBinding Binding(Lsku lsku) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "lsku", ObjectId = lsku.Id, ObjectRef = lsku.CanonicalCode,
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
