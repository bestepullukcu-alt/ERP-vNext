using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
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
public sealed class GskuCorrectionMongoTests : IAsyncLifetime
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
    public async Task Operation_reservation_is_exact_replay_and_tenant_lease_fenced()
    {
        var repository = new GskuCorrectionWorkflowOperationRepository(database, new Tenant(tenantId));
        var operation = Candidate();
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Candidate(operation));
        var drifted = Candidate(operation); drifted.ProposedPackQuantity++;
        var conflict = await repository.ReserveAsync(drifted);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GskuCorrectionWorkflowCheckpoint.Prepared], "worker", now, now + TimeSpan.TicksPerMinute);
        var competing = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [GskuCorrectionWorkflowCheckpoint.Prepared], "other", now, now + TimeSpan.TicksPerMinute);

        Assert.True(first.Succeeded); Assert.True(replay.IsReplay); Assert.False(conflict.Succeeded);
        Assert.NotNull(claim); Assert.Null(competing);
        Assert.Null(await new GskuCorrectionWorkflowOperationRepository(database, new Tenant(Guid.NewGuid()))
            .GetByOperationIdAsync(operation.OperationId));
    }

    [Fact]
    public async Task Gsku_admission_and_approved_decision_are_atomic_and_exactly_replayable()
    {
        var now = DateTimeOffset.UtcNow;
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductDefinitionRevisionId = Guid.NewGuid(),
            CanonicalCode = "GS-CORRECTION", CreationCommandId = Guid.NewGuid().ToString("D"),
            PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1, PackUomCode = "C62",
            PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            PackUomSelection = Selection("uom", "C62"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = now, UpdatedAt = now, Version = 2
        };
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        var repository = new GskuRepository(database, new Tenant(tenantId));
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, operationId, 2);
        var requested = GskuCorrectionAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuCorrectionRequested, 24, "EA",
            gsku.PackApplicabilitySelection, Selection("uom", "EA"), now);
        var first = await repository.AcquireCorrectionAsync(gsku.Id, 2, binding, requested);
        var replay = await repository.AcquireCorrectionAsync(gsku.Id, 2, binding, requested);
        var admitted = first.Gsku!;
        var applied = GskuCorrectionAuditIntentFactory.Create(admitted, 3, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuCorrectionApplied, 24, "EA",
            admitted.PackApplicabilitySelection, Selection("uom", "EA"), now.AddSeconds(1));
        var applicability = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES");
        var uom = Selection("uom", "EA");
        var decision = await repository.ApplyCorrectionDecisionAsync(gsku.Id, 3, binding,
            24, "EA", applicability, uom, applied);
        var decisionReplay = await repository.ApplyCorrectionDecisionAsync(gsku.Id, 3, binding,
            24, "EA", applicability, uom, applied);

        Assert.True(first.Succeeded); Assert.True(replay.IsReplay);
        Assert.True(decision.Succeeded); Assert.True(decisionReplay.IsReplay);
        Assert.Equal(24, decision.Gsku!.PackQuantity); Assert.Equal("EA", decision.Gsku.PackUomCode);
        Assert.Null(decision.Gsku.ActiveLifecycleOperation); Assert.Equal(4, decision.Gsku.Version);
        Assert.Equal(2, decision.Gsku.AuditIntents.Count);
    }

    [Fact]
    public void Factory_rejects_child_fence_and_accepts_exact_verified_pair()
    {
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenantId,
            ProductDefinitionRevisionId = Guid.NewGuid(), CanonicalCode = "GS-FACTORY", Version = 1,
            PackQuantity = 1, PackUomCode = "C62", LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var factory = new GskuCorrectionWorkflowStartRequestFactory(new(Guid.NewGuid(), null,
            [Guid.NewGuid()], "GSKU_CORRECTION", false, false, null, Guid.NewGuid(), null), TimeProvider.System);
        var plan = factory.Create(gsku, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "EA", References("EA"));
        gsku.ChildCreationAdmissions.Add(new()
        {
            ChildKind = GskuChildIdentityKind.Lsku,
            CreationCommandId = "child",
            RequestFingerprint = new string('a', 64),
            AcquiredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(GskuCorrectionWorkflowStartRequestFactory.ObjectType, plan.Operation.ObjectType);
        Assert.Throws<InvalidOperationException>(() => factory.Create(gsku, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 2, "EA", References("EA")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Processor_starts_native_workflow_and_applies_distinct_approver_decision_once(bool approved)
    {
        var now = DateTimeOffset.UtcNow;
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenantId,
            GlobalProductId = Guid.NewGuid(), RevisionIdentifier = "REV-001", CreationCommandId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, CreatedAt = now, UpdatedAt = now };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenantId,
            ProductDefinitionRevisionId = revision.Id, CanonicalCode = "GS-PROCESSOR", CreationCommandId = Guid.NewGuid().ToString("D"),
            PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1, PackUomCode = "C62",
            PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            PackUomSelection = Selection("uom", "C62"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = now, UpdatedAt = now, Version = 2 };
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        var template = Guid.NewGuid(); var maker = Guid.NewGuid();
        var factory = new GskuCorrectionWorkflowStartRequestFactory(new(template, null, [Guid.NewGuid()],
            "GSKU_CORRECTION", false, false, null, Guid.NewGuid(), null), TimeProvider.System);
        var workflow = new Workflow(template, Guid.NewGuid(), approved);
        var operationRepository = new GskuCorrectionWorkflowOperationRepository(database, new Tenant(tenantId));
        var processor = new GskuCorrectionWorkflowProcessor(operationRepository,
            new GskuRepository(database, new Tenant(tenantId)),
            new ProductDefinitionRevisionRepository(database, new Tenant(tenantId)), workflow,
            new Resolver(), factory, TimeProvider.System);
        var operationId = Guid.NewGuid();

        var started = await processor.StartInteractiveAsync(tenantId, gsku.Id, 2, operationId, maker,
            12, "EA", "delegated", new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var durable = await operationRepository.GetByOperationIdAsync(operationId);
        var completed = await processor.RecoverAsync(durable!, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var stored = await new GskuRepository(database, new Tenant(tenantId)).GetByIdAsync(gsku.Id);

        Assert.True(started.Succeeded); Assert.Equal(202, started.StatusCode);
        Assert.True(completed.Succeeded); Assert.Equal(GskuCorrectionWorkflowCheckpoint.Completed, completed.Operation!.Checkpoint);
        Assert.Equal(approved ? 12 : 1, stored!.PackQuantity);
        Assert.Equal(approved ? "EA" : "C62", stored.PackUomCode);
        Assert.Null(stored.ActiveLifecycleOperation); Assert.Equal(4, stored.Version);
        Assert.Equal(1, workflow.StartCalls);
    }

    public async Task DisposeAsync()
    {
        await database.GetCollection<Gsku>("mdm_gskus").DeleteManyAsync(x => x.TenantId == tenantId);
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .DeleteManyAsync(x => x.TenantId == tenantId);
        await database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .DeleteManyAsync(x => x.TenantId == tenantId);
    }

    private GskuCorrectionWorkflowOperation Candidate(GskuCorrectionWorkflowOperation? source = null)
    {
        if (source is not null) return new()
        {
            OperationId = source.OperationId, GskuId = source.GskuId,
            ProductDefinitionRevisionId = source.ProductDefinitionRevisionId, GlobalProductId = source.GlobalProductId,
            BaseGskuVersion = source.BaseGskuVersion, MakerSubjectId = source.MakerSubjectId,
            ProposedPackQuantity = source.ProposedPackQuantity, ProposedPackUomCode = source.ProposedPackUomCode,
            ProposedPackApplicabilitySelection = source.ProposedPackApplicabilitySelection,
            ProposedPackUomSelection = source.ProposedPackUomSelection,
            WorkflowTemplateId = source.WorkflowTemplateId, CandidatePrincipalIds = [.. source.CandidatePrincipalIds],
            ReasonCode = source.ReasonCode, ObjectType = source.ObjectType, ObjectId = source.ObjectId,
            ObjectRef = source.ObjectRef, StartIdempotencyKey = source.StartIdempotencyKey,
            OperationFingerprint = source.OperationFingerprint, CreatedAtUtcTicksV1 = source.CreatedAtUtcTicksV1
        };
        var id = Guid.NewGuid(); return new()
        {
            OperationId = id, GskuId = Guid.NewGuid(), ProductDefinitionRevisionId = Guid.NewGuid(),
            GlobalProductId = Guid.NewGuid(), BaseGskuVersion = 2, MakerSubjectId = Guid.NewGuid(),
            ProposedPackQuantity = 24, ProposedPackUomCode = "EA", WorkflowTemplateId = Guid.NewGuid(),
            ProposedPackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            ProposedPackUomSelection = Selection("uom", "EA"),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "GSKU_CORRECTION",
            ObjectType = GskuCorrectionWorkflowStartRequestFactory.ObjectType, ObjectId = id.ToString("D"),
            ObjectRef = "GS-1", StartIdempotencyKey = $"gsku-correction:{tenantId:D}:{id:D}",
            OperationFingerprint = new string('a', 64), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }
    private static VerifiedGskuReferenceResolveResult References(string uom) => VerifiedGskuReferenceResolveResult.Success([
        new("pack-applicability", "SCALAR_QUANTITY_APPLIES", Guid.NewGuid(), 1, "LATEST", DateTimeOffset.UtcNow, false, true),
        new("uom", uom, Guid.NewGuid(), 1, "LATEST", DateTimeOffset.UtcNow, false, true)]);
    private static ReferenceCatalogSelection Selection(string set, string value) => new()
    {
        SetCode = set, ValueCode = value, CatalogVersionId = Guid.NewGuid(), CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = DateTimeOffset.UtcNow
    };
    private sealed class Tenant(Guid value) : ITenantContext
    {
        public Guid TenantId { get; private set; } = value; public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class Resolver : IWorkflowVerifiedGskuReferenceResolver
    {
        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(Guid tenantId,
            string applicability, string uom, CancellationToken cancellationToken = default) =>
            Task.FromResult(References(uom));
    }
    private sealed class Workflow(Guid templateId, Guid approver, bool approved) : IProductIdentityWorkflowClient
    {
        private readonly Guid instance = Guid.NewGuid(); private readonly Guid version = Guid.NewGuid();
        private readonly Guid task = Guid.NewGuid(); private ProductIdentityWorkflowStartRequest? request;
        public int StartCalls { get; private set; }
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string token,
            CancellationToken cancellationToken = default)
        {
            StartCalls++; this.request = request;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                new(instance, templateId, version, task, Guid.NewGuid(), Guid.NewGuid(), request.ObjectRef!,
                    "Active", "Approval", "Decision", DateTimeOffset.UtcNow, request.DueAt, false, null)));
        }
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND"));
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest input, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                new(instance, task, templateId, version, request!.ObjectType, request.ObjectId, request.ObjectRef!,
                    approved ? "Approve" : "Reject", approver.ToString("D"), approved ? "APPROVED" : "REJECTED",
                    DateTimeOffset.UtcNow, 2, approved ? "Approved" : "Rejected",
                    approved ? "Completed" : "Rejected", null)));
    }
}
