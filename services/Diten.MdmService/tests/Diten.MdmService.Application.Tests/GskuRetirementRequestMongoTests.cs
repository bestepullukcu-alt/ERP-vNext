using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GskuRetirementRequestMongoTests : IAsyncLifetime
{
    private readonly Guid tenantId = Guid.NewGuid();
    private IMongoDatabase database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    }

    [Fact]
    public async Task Reserve_replay_is_exact_and_cross_tenant_hidden()
    {
        var repository = new GskuRetirementRequestOperationRepository(database, new Tenant(tenantId));
        var operation = Candidate();
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Candidate(operation));
        var drift = Candidate(operation);
        drift.RequestReason = "different";
        var conflict = await repository.ReserveAsync(drift);

        Assert.True(first.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(conflict.Succeeded);
        Assert.Null(await new GskuRetirementRequestOperationRepository(database, new Tenant(Guid.NewGuid()))
            .GetByOperationIdAsync(operation.OperationId));
    }

    [Fact]
    public async Task Admission_and_rejection_are_atomic_exact_replay_and_release_fence()
    {
        var gsku = Gsku(2);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        var repository = new GskuRepository(database, new Tenant(tenantId));
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Retirement, operationId, 2);
        var now = DateTimeOffset.UtcNow;
        var requested = GskuRetirementRequestAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuRetirementRequested, "retire obsolete presentation", now);
        var admitted = await repository.AcquireRetirementRequestAsync(gsku.Id, 2, binding, requested);
        var replay = await repository.AcquireRetirementRequestAsync(gsku.Id, 2, binding, requested);
        var rejectedAudit = GskuRetirementRequestAuditIntentFactory.Create(admitted.Gsku!, 3, operationId,
            Guid.NewGuid(), ProductAuditOperation.GskuRetirementRejected,
            "retire obsolete presentation", now.AddSeconds(1));
        var rejected = await repository.RejectRetirementRequestAsync(gsku.Id, 3, binding, rejectedAudit);
        var rejectionReplay = await repository.RejectRetirementRequestAsync(gsku.Id, 3, binding, rejectedAudit);

        Assert.True(admitted.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.True(rejected.Succeeded);
        Assert.True(rejectionReplay.IsReplay);
        Assert.Null(rejected.Gsku!.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, rejected.Gsku.LifecycleStatus);
        Assert.Equal(4, rejected.Gsku.Version);
    }

    [Fact]
    public async Task Approved_request_handoff_atomically_becomes_existing_pair_retirement_fence()
    {
        var gsku = Gsku(2);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        var repository = new GskuRepository(database, new Tenant(tenantId));
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Retirement, operationId, 2);
        var requested = GskuRetirementRequestAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuRetirementRequested, "retire obsolete presentation", DateTimeOffset.UtcNow);
        var admitted = await repository.AcquireRetirementRequestAsync(gsku.Id, 2, binding, requested);
        var fingerprint = new string('a', 64);
        var fenced = await repository.CloseChildAdmissionFenceAsync(gsku.Id, 3, operationId, fingerprint);
        var fenceReplay = await repository.CloseChildAdmissionFenceAsync(gsku.Id, 3, operationId, fingerprint);

        Assert.True(admitted.Succeeded);
        Assert.True(fenced.Succeeded);
        Assert.True(fenceReplay.IsReplay);
        Assert.Null(fenced.Aggregate!.ActiveLifecycleOperation);
        Assert.Equal(operationId, fenced.Aggregate.RetirementOperationId);
        Assert.Equal(fingerprint, fenced.Aggregate.RetirementOperationFingerprint);
        Assert.Equal(4, fenced.Aggregate.Version);
    }

    [Fact]
    public async Task Manual_reconciliation_audit_is_atomic_and_exact_replay_after_pair_handoff()
    {
        var gsku = Gsku(2);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        var repository = new GskuRepository(database, new Tenant(tenantId));
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Retirement, operationId, 2);
        var requested = GskuRetirementRequestAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuRetirementRequested, "retire obsolete presentation", DateTimeOffset.UtcNow);
        await repository.AcquireRetirementRequestAsync(gsku.Id, 2, binding, requested);
        var fenced = await repository.CloseChildAdmissionFenceAsync(gsku.Id, 3, operationId, new string('a', 64));
        var manualAudit = GskuRetirementRequestAuditIntentFactory.Create(fenced.Aggregate!, 4, operationId,
            Guid.NewGuid(), ProductAuditOperation.GskuRetirementManualReconciliationRequired,
            "retire obsolete presentation", DateTimeOffset.UtcNow.AddSeconds(1));

        var recorded = await repository.RecordRetirementRequestConflictAsync(gsku.Id, 4, binding, manualAudit);
        var replay = await repository.RecordRetirementRequestConflictAsync(gsku.Id, 4, binding, manualAudit);

        Assert.True(recorded.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(5, recorded.Gsku!.Version);
        Assert.Contains(recorded.Gsku.AuditIntents,
            x => x.Operation == ProductAuditOperation.GskuRetirementManualReconciliationRequired);
    }

    [Fact]
    public void Factory_uses_exact_pair_and_rejects_child_admission()
    {
        var gsku = Gsku(2);
        var revision = Revision(gsku);
        var template = Guid.NewGuid();
        var factory = new GskuRetirementRequestWorkflowStartRequestFactory(new(template, null,
            [Guid.NewGuid()], "GSKU_RETIREMENT", false, false, null, Guid.NewGuid(), null,
            Guid.NewGuid(), null), TimeProvider.System);
        var plan = factory.Create(gsku, revision, Guid.NewGuid(), Guid.NewGuid(), "obsolete");
        gsku.ChildCreationAdmissions.Add(new()
        {
            ChildKind = GskuChildIdentityKind.Lsku, CreationCommandId = "child",
            RequestFingerprint = new string('b', 64), AcquiredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType, plan.Operation.ObjectType);
        Assert.Equal(revision.Version, plan.Operation.BaseRevisionVersion);
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(gsku, revision, Guid.NewGuid(), Guid.NewGuid(), "obsolete"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Native_workflow_distinct_decision_retires_pair_or_preserves_it(bool approved)
    {
        var gsku = Gsku(2);
        var revision = Revision(gsku);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .InsertOneAsync(revision);
        var tenant = new Tenant(tenantId);
        var gskuRepository = new GskuRepository(database, tenant);
        var revisionRepository = new ProductDefinitionRevisionRepository(database, tenant);
        var pair = new FirstGskuIdentityRetirementProcessor(
            new FirstGskuIdentityRetirementOperationRepository(database, tenant),
            gskuRepository, revisionRepository, TimeProvider.System);
        var template = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var workflow = new Workflow(template, Guid.NewGuid(), approved);
        var factory = new GskuRetirementRequestWorkflowStartRequestFactory(new(template, null,
            [Guid.NewGuid()], "GSKU_RETIREMENT", false, false, null, Guid.NewGuid(), null,
            Guid.NewGuid(), null), TimeProvider.System);
        var operations = new GskuRetirementRequestOperationRepository(database, tenant);
        var processor = new GskuRetirementRequestWorkflowProcessor(
            operations, gskuRepository, revisionRepository, workflow, pair, factory, TimeProvider.System);
        var operationId = Guid.NewGuid();

        var started = await processor.StartInteractiveAsync(tenantId, gsku.Id, 2, operationId, maker,
            "obsolete", "delegated", new(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(1)), default);
        var durable = await operations.GetByOperationIdAsync(operationId);
        var completed = await processor.RecoverAsync(durable!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(1)), default);
        var storedGsku = await gskuRepository.GetByIdAsync(gsku.Id);
        var storedRevision = await revisionRepository.GetByIdAsync(revision.Id);

        Assert.True(started.Succeeded);
        Assert.Equal(202, started.StatusCode);
        Assert.True(completed.Succeeded);
        Assert.Equal(GskuRetirementRequestCheckpoint.Completed, completed.Operation!.Checkpoint);
        Assert.Equal(approved ? ProductIdentityLifecycleStatus.Retired : ProductIdentityLifecycleStatus.IdentityApproved,
            storedGsku!.LifecycleStatus);
        Assert.Equal(approved ? ProductIdentityLifecycleStatus.Retired : ProductIdentityLifecycleStatus.IdentityApproved,
            storedRevision!.LifecycleStatus);
        Assert.Null(storedGsku.ActiveLifecycleOperation);
        Assert.Equal(1, workflow.StartCalls);
    }

    public async Task DisposeAsync()
    {
        await database.GetCollection<Gsku>("mdm_gskus").DeleteManyAsync(x => x.TenantId == tenantId);
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .DeleteManyAsync(x => x.TenantId == tenantId);
        await database.GetCollection<GskuRetirementRequestOperation>(
            GskuRetirementRequestOperationRepository.CollectionName).DeleteManyAsync(x => x.TenantId == tenantId);
        await database.GetCollection<FirstGskuIdentityRetirementOperation>(
            FirstGskuIdentityRetirementOperationRepository.CollectionName).DeleteManyAsync(x => x.TenantId == tenantId);
    }

    private Gsku Gsku(int version)
    {
        var id = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        return new()
        {
            Id = id, TenantId = tenantId, ProductDefinitionRevisionId = revisionId,
            CanonicalCode = "GS-RETIREMENT", CreationCommandId = Guid.NewGuid().ToString("D"),
            PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1, PackUomCode = "C62",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            IdentityWorkflowBinding = ApprovedBinding(id, revisionId),
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Version = version
        };
    }

    private ProductDefinitionRevision Revision(Gsku gsku) => new()
    {
        Id = gsku.ProductDefinitionRevisionId, TenantId = tenantId, GlobalProductId = Guid.NewGuid(),
        RevisionIdentifier = "REV-001", CreationCommandId = Guid.NewGuid().ToString("D"),
        LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
        IdentityWorkflowBinding = gsku.IdentityWorkflowBinding,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Version = 2
    };

    private GskuRetirementRequestOperation Candidate(GskuRetirementRequestOperation? source = null)
    {
        if (source is not null) return new()
        {
            OperationId = source.OperationId, GskuId = source.GskuId,
            ProductDefinitionRevisionId = source.ProductDefinitionRevisionId, GlobalProductId = source.GlobalProductId,
            BaseGskuVersion = source.BaseGskuVersion, BaseRevisionVersion = source.BaseRevisionVersion,
            MakerSubjectId = source.MakerSubjectId, RequestReason = source.RequestReason,
            WorkflowTemplateId = source.WorkflowTemplateId, CandidatePrincipalIds = [.. source.CandidatePrincipalIds],
            ReasonCode = source.ReasonCode, ObjectType = source.ObjectType, ObjectId = source.ObjectId,
            ObjectRef = source.ObjectRef, StartIdempotencyKey = source.StartIdempotencyKey,
            OperationFingerprint = source.OperationFingerprint, CreatedAtUtcTicksV1 = source.CreatedAtUtcTicksV1
        };
        var id = Guid.NewGuid();
        return new()
        {
            OperationId = id, GskuId = Guid.NewGuid(), ProductDefinitionRevisionId = Guid.NewGuid(),
            GlobalProductId = Guid.NewGuid(), BaseGskuVersion = 2, BaseRevisionVersion = 2,
            MakerSubjectId = Guid.NewGuid(), RequestReason = "obsolete",
            WorkflowTemplateId = Guid.NewGuid(), CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "GSKU_RETIREMENT", ObjectType = GskuRetirementRequestWorkflowStartRequestFactory.ObjectType,
            ObjectId = id.ToString("D"), ObjectRef = "GS-1",
            StartIdempotencyKey = $"gsku-retirement-request:{tenantId:D}:{id:D}",
            OperationFingerprint = new string('a', 64), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private sealed class Tenant(Guid value) : ITenantContext
    {
        public Guid TenantId { get; private set; } = value;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private static FirstGskuIdentityWorkflowBinding ApprovedBinding(Guid gskuId, Guid revisionId)
    {
        var workflow = Guid.NewGuid();
        var template = Guid.NewGuid();
        var version = Guid.NewGuid();
        var task = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            WorkflowInstanceId = workflow, WorkflowTemplateId = template,
            WorkflowTemplateVersionId = version, ApprovalTaskId = task,
            AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "gsku", GskuId = gskuId, ProductDefinitionRevisionId = revisionId,
            ObjectRef = "GS-RETIREMENT", SubmitterSubjectId = Guid.NewGuid(),
            StartIdempotencyKey = "first-gsku", StartRequestFingerprint = new string('c', 64),
            SubmittedAtUtc = now,
            TerminalDecision = new()
            {
                Decision = ProductIdentityDecisionKind.Approved, WorkflowInstanceId = workflow,
                ApprovalTaskId = task, WorkflowTemplateId = template, WorkflowTemplateVersionId = version,
                ObjectType = "gsku", ObjectId = gskuId, ObjectRef = "GS-RETIREMENT",
                DecisionActorSubjectId = Guid.NewGuid(), DecisionAtUtc = now, TransitionSequence = 2,
                TaskStatus = "Approved", InstanceStatus = "Completed"
            }
        };
    }

    private sealed class Workflow(Guid templateId, Guid approver, bool approved) : IProductIdentityWorkflowClient
    {
        private readonly Guid instance = Guid.NewGuid();
        private readonly Guid version = Guid.NewGuid();
        private readonly Guid task = Guid.NewGuid();
        private ProductIdentityWorkflowStartRequest? request;
        public int StartCalls { get; private set; }
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest value, string token,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            request = value;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                new(instance, templateId, version, task, Guid.NewGuid(), Guid.NewGuid(), value.ObjectRef!,
                    "Active", "Approval", "Decision", DateTimeOffset.UtcNow, value.DueAt, false, null)));
        }
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest value,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND"));
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest value,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                new(instance, task, templateId, version, request!.ObjectType, request.ObjectId, request.ObjectRef!,
                    approved ? "Approve" : "Reject", approver.ToString("D"), approved ? "APPROVED" : "REJECTED",
                    DateTimeOffset.UtcNow, 2, approved ? "Approved" : "Rejected",
                    approved ? "Completed" : "Rejected", null)));
    }
}
