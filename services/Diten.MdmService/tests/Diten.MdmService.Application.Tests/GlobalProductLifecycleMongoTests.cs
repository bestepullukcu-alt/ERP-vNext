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
public sealed class GlobalProductLifecycleMongoTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProduct> _products = null!;

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
        _products = _database.GetCollection<GlobalProduct>("mdm_global_products");
    }

    [Fact]
    public async Task Submit_replay_and_fingerprint_drift_are_atomic_and_deterministic()
    {
        var product = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository();
        var binding = Binding(product.Id, "submit-1", "fingerprint-A");
        var intent = Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
            "submit-1", "fingerprint-A");

        var first = await repository.SubmitIdentityAsync(product.Id, 0, binding, intent);
        var replay = await repository.SubmitIdentityAsync(product.Id, 0, binding, intent);
        var bindingDrift = Binding(product.Id, "submit-1", "fingerprint-A");
        var replayWithBindingDrift = await repository.SubmitIdentityAsync(
            product.Id, 0, bindingDrift, intent);
        var drift = await repository.SubmitIdentityAsync(product.Id, 0, binding,
            Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "submit-1", "fingerprint-B"));

        Assert.True(first.Succeeded);
        Assert.False(first.IsReplay);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(replayWithBindingDrift.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", replayWithBindingDrift.ErrorCode);
        Assert.False(drift.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        var stored = await _products.Find(item => item.TenantId == _tenantId && item.Id == product.Id).SingleAsync();
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, stored.LifecycleStatus);
        Assert.Equal(1, stored.Version);
        Assert.Equal(binding.WorkflowInstanceId, stored.WorkflowBinding!.WorkflowInstanceId);
        Assert.Single(stored.AuditIntents);
    }

    [Fact]
    public async Task Approved_terminal_evidence_is_bound_and_replay_does_not_append_again()
    {
        var product = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository();
        var binding = Binding(product.Id, "submit-approve", "submit-fingerprint");
        Assert.True((await repository.SubmitIdentityAsync(product.Id, 0, binding,
            Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "submit-approve", "submit-fingerprint"))).Succeeded);
        var evidence = Evidence(product.Id, binding, ProductIdentityDecisionKind.Approved);
        var intent = Intent(product.Id, 1, ProductAuditOperation.GlobalProductIdentityApproved,
            "decision-approve", "decision-fingerprint");

        var first = await repository.ReconcileIdentityDecisionAsync(product.Id, 1, evidence, intent);
        var replay = await repository.ReconcileIdentityDecisionAsync(product.Id, 1, evidence, intent);
        var evidenceDrift = Evidence(product.Id, binding, ProductIdentityDecisionKind.Approved);
        evidenceDrift.TransitionSequence = evidence.TransitionSequence + 1;
        var replayWithEvidenceDrift = await repository.ReconcileIdentityDecisionAsync(
            product.Id, 1, evidenceDrift, intent);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(replayWithEvidenceDrift.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", replayWithEvidenceDrift.ErrorCode);
        var stored = await _products.Find(item => item.TenantId == _tenantId && item.Id == product.Id).SingleAsync();
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, stored.LifecycleStatus);
        Assert.Equal(2, stored.Version);
        Assert.Equal(evidence.TransitionSequence, stored.WorkflowBinding!.TerminalDecision!.TransitionSequence);
        Assert.Equal(2, stored.AuditIntents.Count);
    }

    [Fact]
    public async Task Rejection_returns_to_draft_and_binding_mismatch_is_fail_closed()
    {
        var product = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var repository = Repository();
        var binding = Binding(product.Id, "submit-reject", "submit-fingerprint");
        Assert.True((await repository.SubmitIdentityAsync(product.Id, 0, binding,
            Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "submit-reject", "submit-fingerprint"))).Succeeded);
        var wrong = Evidence(product.Id, binding, ProductIdentityDecisionKind.Rejected);
        wrong.WorkflowInstanceId = Guid.NewGuid();

        var conflict = await repository.ReconcileIdentityDecisionAsync(product.Id, 1, wrong,
            Intent(product.Id, 1, ProductAuditOperation.GlobalProductIdentityRejected,
                "wrong-decision", "wrong-fingerprint"));
        var evidence = Evidence(product.Id, binding, ProductIdentityDecisionKind.Rejected);
        var rejected = await repository.ReconcileIdentityDecisionAsync(product.Id, 1, evidence,
            Intent(product.Id, 1, ProductAuditOperation.GlobalProductIdentityRejected,
                "reject-decision", "reject-fingerprint"));

        Assert.False(conflict.Succeeded);
        Assert.Equal("WORKFLOW_BINDING_CONFLICT", conflict.ErrorCode);
        Assert.True(rejected.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, rejected.GlobalProduct!.LifecycleStatus);
        Assert.Equal(ProductIdentityDecisionKind.Rejected,
            rejected.GlobalProduct.WorkflowBinding!.TerminalDecision!.Decision);
    }

    [Fact]
    public async Task Tenant_soft_delete_state_and_expected_version_fences_are_non_disclosing()
    {
        var product = await SeedAsync(ProductIdentityLifecycleStatus.Draft);
        var tenantBId = Guid.NewGuid();
        var tenantB = new GlobalProductRepository(_database, new Tenant(tenantBId));
        var missing = await tenantB.SubmitIdentityAsync(product.Id, 0,
            Binding(product.Id, "cross", "cross-fingerprint"),
            Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "cross", "cross-fingerprint", tenantBId));
        Assert.False(missing.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_NOT_FOUND", missing.ErrorCode);

        var stale = await Repository().SubmitIdentityAsync(product.Id, 3,
            Binding(product.Id, "stale", "stale-fingerprint"),
            Intent(product.Id, 3, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "stale", "stale-fingerprint"));
        Assert.False(stale.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_CONCURRENCY_CONFLICT", stale.ErrorCode);

        await _products.UpdateOneAsync(item => item.TenantId == _tenantId && item.Id == product.Id,
            Builders<GlobalProduct>.Update.Set(item => item.IsDeleted, true));
        var deleted = await Repository().SubmitIdentityAsync(product.Id, 0,
            Binding(product.Id, "deleted", "deleted-fingerprint"),
            Intent(product.Id, 0, ProductAuditOperation.GlobalProductIdentitySubmitted,
                "deleted", "deleted-fingerprint"));
        Assert.False(deleted.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_NOT_FOUND", deleted.ErrorCode);
    }

    [Fact]
    public async Task Same_operation_id_with_delimiter_collision_facts_is_idempotency_conflict()
    {
        var product = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var operationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = Repository();
        var firstIntent = ProductIdentityLifecycleAuditIntentFactory.CreateRetire(
            product, 0, operationId, actorId, "A|B", "C", Now);
        var collidingTupleIntent = ProductIdentityLifecycleAuditIntentFactory.CreateRetire(
            product, 0, operationId, actorId, "A", "B|C", Now);

        var first = await repository.RetireIdentityAsync(product.Id, 0, firstIntent);
        var conflict = await repository.RetireIdentityAsync(product.Id, 0, collidingTupleIntent);

        Assert.True(first.Succeeded);
        Assert.False(conflict.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", conflict.ErrorCode);
        var stored = await _products.Find(item => item.TenantId == _tenantId && item.Id == product.Id).SingleAsync();
        Assert.Equal(1, stored.Version);
        Assert.Single(stored.AuditIntents);
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _products.DeleteManyAsync(item => item.TenantId == _tenantId),
            _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
                .DeleteManyAsync(item => item.TenantId == _tenantId),
            _database.GetCollection<Gsku>("mdm_gskus").DeleteManyAsync(item => item.TenantId == _tenantId));
    }

    private GlobalProductRepository Repository() => new(_database, new Tenant(_tenantId));

    private async Task<GlobalProduct> SeedAsync(ProductIdentityLifecycleStatus status)
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = $"GP-{Guid.NewGuid():N}",
            GlobalProductName = "Lifecycle product", GlobalProductNameNormalized = $"PRODUCT-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = status, CreatedAt = Now, UpdatedAt = Now
        };
        await _products.InsertOneAsync(product);
        return product;
    }

    private static ProductIdentityWorkflowBinding Binding(Guid productId, string key, string fingerprint) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "GlobalProduct", ObjectId = productId, ObjectRef = $"GP:{productId:D}",
        SubmitterSubjectId = Guid.NewGuid(), StartIdempotencyKey = key,
        StartRequestFingerprint = fingerprint, SubmittedAtUtc = Now
    };

    private static ProductIdentityWorkflowDecisionEvidence Evidence(
        Guid productId,
        ProductIdentityWorkflowBinding binding,
        ProductIdentityDecisionKind decision) => new()
    {
        Decision = decision, WorkflowInstanceId = binding.WorkflowInstanceId,
        ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
        WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = "GlobalProduct",
        ObjectId = productId, ObjectRef = binding.ObjectRef, DecisionActorSubjectId = Guid.NewGuid(),
        ReasonCode = decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null,
        DecisionAtUtc = Now.AddMinutes(1), TransitionSequence = 2,
        TaskStatus = decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        InstanceStatus = decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected"
    };

    private LocalAuditIntent Intent(
        Guid productId,
        int expectedVersion,
        ProductAuditOperation operation,
        string key,
        string fingerprint,
        Guid? tenantOverride = null) => new()
    {
        IntentId = Guid.NewGuid(), TenantId = tenantOverride ?? _tenantId,
        AggregateType = AuditAggregateType.GlobalProduct, AggregateId = productId,
        PreVersion = expectedVersion, PostVersion = expectedVersion + 1, Operation = operation,
        ActorId = Guid.NewGuid().ToString("D"), CorrelationId = Guid.NewGuid().ToString("D"),
        CausationId = Guid.NewGuid().ToString("D"), CommandId = key, Sequence = expectedVersion + 1L,
        TimestampUtc = Now, EvidenceHash = fingerprint, IdempotencyKey = key
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
