using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FirstGskuIdentityLifecycleMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<ProductDefinitionRevision> _revisions = null!;
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
        _revisions = _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
    }

    [Fact]
    public async Task Pair_submit_and_approve_are_atomic_per_document_exactly_replayable_and_tenant_fenced()
    {
        var pair = await SeedPairAsync(_tenantId);
        var binding = Binding(pair.Revision.Id, pair.Gsku.Id);
        var revisionRepository = new ProductDefinitionRevisionRepository(_database, new Tenant(_tenantId));
        var gskuRepository = new GskuRepository(_database, new Tenant(_tenantId));

        var revisionSubmit = Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted, 0, "submit-revision");
        var gskuSubmit = Intent(pair.Gsku, ProductAuditOperation.GskuIdentitySubmitted, 0, "submit-gsku");
        var r1 = await revisionRepository.MarkIdentityPendingAsync(pair.Revision.Id, 0, binding, revisionSubmit);
        var g1 = await gskuRepository.MarkIdentityPendingAsync(pair.Gsku.Id, 0, binding, gskuSubmit);
        var rReplay = await revisionRepository.MarkIdentityPendingAsync(pair.Revision.Id, 0, binding, revisionSubmit);
        var gReplay = await gskuRepository.MarkIdentityPendingAsync(pair.Gsku.Id, 0, binding, gskuSubmit);
        Assert.True(r1.Succeeded && g1.Succeeded);
        Assert.True(rReplay.IsReplay && gReplay.IsReplay);

        binding.TerminalDecision = Decision(binding);
        var r2 = await revisionRepository.ApproveIdentityAsync(pair.Revision.Id, 1, binding,
            Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentityApproved, 1, "approve-revision"));
        var g2 = await gskuRepository.ApproveIdentityAsync(pair.Gsku.Id, 1, binding,
            Intent(pair.Gsku, ProductAuditOperation.GskuIdentityApproved, 1, "approve-gsku"));
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, r2.Aggregate!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, g2.Aggregate!.LifecycleStatus);
        Assert.Equal(2, r2.Aggregate.Version);
        Assert.Equal(2, g2.Aggregate.Version);
        Assert.Null(await new ProductDefinitionRevisionRepository(_database, new Tenant(_otherTenantId)).GetByIdAsync(pair.Revision.Id));
        Assert.Null(await new GskuRepository(_database, new Tenant(_otherTenantId)).GetByIdAsync(pair.Gsku.Id));
    }

    [Fact]
    public async Task Pair_rejection_restores_GSKU_then_revision_without_cascade_and_drift_conflicts()
    {
        var pair = await SeedPairAsync(_tenantId);
        var binding = Binding(pair.Revision.Id, pair.Gsku.Id);
        var revisions = new ProductDefinitionRevisionRepository(_database, new Tenant(_tenantId));
        var gskus = new GskuRepository(_database, new Tenant(_tenantId));
        Assert.True((await revisions.MarkIdentityPendingAsync(pair.Revision.Id, 0, binding,
            Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted, 0, "submit-r"))).Succeeded);
        Assert.True((await gskus.MarkIdentityPendingAsync(pair.Gsku.Id, 0, binding,
            Intent(pair.Gsku, ProductAuditOperation.GskuIdentitySubmitted, 0, "submit-g"))).Succeeded);
        binding.TerminalDecision = Decision(binding, ProductIdentityDecisionKind.Rejected);

        var gskuRejected = await gskus.RestoreDraftAfterRejectionAsync(pair.Gsku.Id, 1, binding,
            Intent(pair.Gsku, ProductAuditOperation.GskuIdentityRejected, 1, "reject-g"));
        var revisionRejected = await revisions.RestoreDraftAfterRejectionAsync(pair.Revision.Id, 1, binding,
            Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentityRejected, 1, "reject-r"));
        var drift = await gskus.RestoreDraftAfterRejectionAsync(pair.Gsku.Id, 1, binding,
            Intent(pair.Gsku, ProductAuditOperation.GskuIdentityRejected, 1, "different-key"));

        Assert.Equal(ProductIdentityLifecycleStatus.Draft, gskuRejected.Aggregate!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, revisionRejected.Aggregate!.LifecycleStatus);
        Assert.False(drift.Succeeded);
        Assert.Equal("FIRST_GSKU_CONCURRENCY_CONFLICT", drift.ErrorCode);
        Assert.Equal(2, await _revisions.CountDocumentsAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId));
        Assert.Equal(2, await _gskus.CountDocumentsAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId));
    }

    [Fact]
    public async Task Decision_requires_exact_terminal_tuple_and_replay_compares_full_immutable_binding()
    {
        var pair = await SeedPairAsync(_tenantId);
        var binding = Binding(pair.Revision.Id, pair.Gsku.Id);
        var revisions = new ProductDefinitionRevisionRepository(_database, new Tenant(_tenantId));
        var gskus = new GskuRepository(_database, new Tenant(_tenantId));
        var revisionSubmit = Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted, 0, "strict-r");
        var gskuSubmit = Intent(pair.Gsku, ProductAuditOperation.GskuIdentitySubmitted, 0, "strict-g");
        Assert.True((await revisions.MarkIdentityPendingAsync(pair.Revision.Id, 0, binding, revisionSubmit)).Succeeded);
        Assert.True((await gskus.MarkIdentityPendingAsync(pair.Gsku.Id, 0, binding, gskuSubmit)).Succeeded);

        var drifted = CloneBinding(binding);
        drifted.AssignmentSnapshotId = Guid.NewGuid();
        var revisionReplayDrift = await revisions.MarkIdentityPendingAsync(pair.Revision.Id, 0, drifted, revisionSubmit);
        var gskuReplayDrift = await gskus.MarkIdentityPendingAsync(pair.Gsku.Id, 0, drifted, gskuSubmit);
        Assert.Equal("FIRST_GSKU_IDENTITY_IDEMPOTENCY_CONFLICT", revisionReplayDrift.ErrorCode);
        Assert.Equal("FIRST_GSKU_IDENTITY_IDEMPOTENCY_CONFLICT", gskuReplayDrift.ErrorCode);

        var revisionApprove = Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentityApproved, 1, "strict-approve-r");
        var gskuApprove = Intent(pair.Gsku, ProductAuditOperation.GskuIdentityApproved, 1, "strict-approve-g");
        var noEvidenceRevision = await revisions.ApproveIdentityAsync(pair.Revision.Id, 1, binding, revisionApprove);
        var noEvidenceGsku = await gskus.ApproveIdentityAsync(pair.Gsku.Id, 1, binding, gskuApprove);
        Assert.Equal("FIRST_GSKU_WORKFLOW_DECISION_INVALID", noEvidenceRevision.ErrorCode);
        Assert.Equal("FIRST_GSKU_WORKFLOW_DECISION_INVALID", noEvidenceGsku.ErrorCode);

        var corrupt = CloneBinding(binding);
        corrupt.TerminalDecision = Decision(corrupt);
        corrupt.TerminalDecision.DecisionActorSubjectId = corrupt.SubmitterSubjectId;
        corrupt.TerminalDecision.TaskStatus = "Rejected";
        Assert.Equal("FIRST_GSKU_WORKFLOW_DECISION_INVALID",
            (await revisions.ApproveIdentityAsync(pair.Revision.Id, 1, corrupt, revisionApprove)).ErrorCode);
        Assert.Equal("FIRST_GSKU_WORKFLOW_DECISION_INVALID",
            (await gskus.ApproveIdentityAsync(pair.Gsku.Id, 1, corrupt, gskuApprove)).ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval,
            (await revisions.GetByIdAsync(pair.Revision.Id))!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval,
            (await gskus.GetByIdAsync(pair.Gsku.Id))!.LifecycleStatus);
    }

    public async Task DisposeAsync()
    {
        await _revisions.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
        await _gskus.DeleteManyAsync(x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);
    }

    private async Task<(ProductDefinitionRevision Revision, Gsku Gsku)> SeedPairAsync(Guid tenantId)
    {
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GlobalProductId = Guid.NewGuid(),
            RevisionIdentifier = $"REV-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, Version = 0
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CreationCommandId = revision.CreationCommandId,
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, Version = 0
        };
        await _revisions.InsertManyAsync([revision, new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _otherTenantId, GlobalProductId = revision.GlobalProductId,
            RevisionIdentifier = $"REV-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}"
        }]);
        await _gskus.InsertManyAsync([gsku, new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _otherTenantId, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA"
        }]);
        return (revision, gsku);
    }

    private static FirstGskuIdentityWorkflowBinding Binding(Guid revisionId, Guid gskuId) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "gsku", GskuId = gskuId, ProductDefinitionRevisionId = revisionId,
        ObjectRef = $"GS:{gskuId:D}", SubmitterSubjectId = Guid.NewGuid(),
        StartIdempotencyKey = $"start-{Guid.NewGuid():N}", StartRequestFingerprint = $"fp-{Guid.NewGuid():N}",
        SubmittedAtUtc = DateTimeOffset.UtcNow
    };

    private static ProductIdentityWorkflowDecisionEvidence Decision(
        FirstGskuIdentityWorkflowBinding binding,
        ProductIdentityDecisionKind decision = ProductIdentityDecisionKind.Approved) => new()
    {
        Decision = decision, WorkflowInstanceId = binding.WorkflowInstanceId,
        ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
        WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = binding.ObjectType,
        ObjectId = binding.GskuId, ObjectRef = binding.ObjectRef, DecisionActorSubjectId = Guid.NewGuid(),
        DecisionAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 2,
        TaskStatus = decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        InstanceStatus = decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected"
    };

    private static FirstGskuIdentityWorkflowBinding CloneBinding(FirstGskuIdentityWorkflowBinding source) => new()
    {
        WorkflowInstanceId = source.WorkflowInstanceId,
        WorkflowTemplateId = source.WorkflowTemplateId,
        WorkflowTemplateVersionId = source.WorkflowTemplateVersionId,
        ApprovalTaskId = source.ApprovalTaskId,
        AssignmentSnapshotId = source.AssignmentSnapshotId,
        StartTransitionLogId = source.StartTransitionLogId,
        ObjectType = source.ObjectType,
        GskuId = source.GskuId,
        ProductDefinitionRevisionId = source.ProductDefinitionRevisionId,
        ObjectRef = source.ObjectRef,
        SubmitterSubjectId = source.SubmitterSubjectId,
        StartIdempotencyKey = source.StartIdempotencyKey,
        StartRequestFingerprint = source.StartRequestFingerprint,
        SubmittedAtUtc = source.SubmittedAtUtc,
        DueAtUtc = source.DueAtUtc,
        TerminalDecision = source.TerminalDecision
    };

    private static LocalAuditIntent Intent(
        EntityBase aggregate, ProductAuditOperation operation, int version, string key) => new()
    {
        IntentId = Guid.NewGuid(), TenantId = aggregate.TenantId,
        AggregateType = aggregate is Gsku ? AuditAggregateType.Gsku : AuditAggregateType.ProductDefinitionRevision,
        AggregateId = aggregate.Id, PreVersion = version, PostVersion = version + 1,
        Operation = operation, ActorId = Guid.NewGuid().ToString("D"), CorrelationId = key,
        CausationId = key, CommandId = key, Sequence = version + 1,
        TimestampUtc = DateTimeOffset.UtcNow, EvidenceHash = $"hash-{key}", IdempotencyKey = key
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
