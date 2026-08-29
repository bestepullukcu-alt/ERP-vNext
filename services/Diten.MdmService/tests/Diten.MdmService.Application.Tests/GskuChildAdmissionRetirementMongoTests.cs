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
public sealed class GskuChildAdmissionRetirementMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<Gsku> _gskus = null!;
    private IMongoCollection<ProductDefinitionRevision> _revisions = null!;
    private IMongoCollection<Lsku> _lskus = null!;
    private IMongoCollection<FinishedGood> _finishedGoods = null!;
    private IMongoCollection<CodeReservation> _reservations = null!;

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
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
        _revisions = _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _lskus = _database.GetCollection<Lsku>("mdm_lskus");
        _finishedGoods = _database.GetCollection<FinishedGood>("mdm_finished_goods");
        _reservations = _database.GetCollection<CodeReservation>("mdm_code_reservations");
    }

    [Fact]
    public async Task Referenceable_gsku_requires_both_pair_members_identity_approved()
    {
        var pair = await SeedPairAsync(ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.IdentityApproved);
        var repository = GskuRepository();
        Assert.Null(await repository.GetReferenceableByIdAsync(pair.Gsku.Id));
        await _revisions.UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == pair.Revision.Id,
            Builders<ProductDefinitionRevision>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.NotNull(await repository.GetReferenceableByIdAsync(pair.Gsku.Id));
        await _gskus.UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == pair.Gsku.Id,
            Builders<Gsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft));
        Assert.Null(await repository.GetReferenceableByIdAsync(pair.Gsku.Id));
    }

    [Fact]
    public async Task Admission_is_shared_bounded_and_retirement_fence_blocks_new_children()
    {
        var pair = await SeedPairAsync();
        var repository = GskuRepository();
        for (var i = 0; i < GskuChildCreationAdmission.MaximumActiveAdmissions; i++)
        {
            var kind = i % 2 == 0 ? GskuChildIdentityKind.Lsku : GskuChildIdentityKind.FinishedGood;
            var command = $"child-{i:D2}";
            var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
                pair.Gsku.Id, kind, command, kind == GskuChildIdentityKind.Lsku ? "TR" : null);
            Assert.True((await repository.AcquireChildCreationAdmissionAsync(
                pair.Gsku.Id, kind, command, fingerprint, DateTimeOffset.UtcNow)).Succeeded);
        }
        var overflow = await repository.AcquireChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, "overflow",
            GskuChildCreationAdmission.ComputeRequestFingerprint(
                pair.Gsku.Id, GskuChildIdentityKind.Lsku, "overflow", "TR"), DateTimeOffset.UtcNow);
        Assert.Equal("GSKU_CHILD_ADMISSION_LIMIT_REACHED", overflow.ErrorCode);
        var fenced = await repository.CloseChildAdmissionFenceAsync(
            pair.Gsku.Id, pair.Gsku.Version, Guid.NewGuid(), "retire-fingerprint");
        Assert.Equal("GSKU_CHILD_CREATION_IN_PROGRESS", fenced.ErrorCode);
    }

    [Fact]
    public async Task Completion_requires_confirmed_durable_binding_then_retirement_is_child_first_and_no_cascade()
    {
        var pair = await SeedPairAsync();
        var repository = GskuRepository();
        const string command = "lsku-create";
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, "TR");
        Assert.True((await repository.AcquireChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, fingerprint, DateTimeOffset.UtcNow)).Succeeded);
        var child = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = pair.Gsku.Id,
            CanonicalCode = $"LS-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = command, MarketCode = "TR", LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        await _lskus.InsertOneAsync(child);
        await _reservations.InsertOneAsync(new CodeReservation
        {
            Id = child.CodeReservationId, TenantId = _tenantId, EntityType = CodeBearingEntityType.Lsku,
            ReservedCode = child.CanonicalCode, ReservationState = CodeReservationState.Consumed,
            BindingState = CodeReservationBindingState.PendingIdentityWrite, ConsumedEntityId = child.Id,
            ReservationCommandId = command, ReservedAt = DateTimeOffset.UtcNow, ReservedByActorId = "test"
        });
        var premature = await repository.CompleteChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, fingerprint);
        Assert.Equal("GSKU_CHILD_BINDING_NOT_DURABLE", premature.ErrorCode);
        Assert.Single((await repository.GetByIdAsync(pair.Gsku.Id))!.ChildCreationAdmissions);
        await _reservations.UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == child.CodeReservationId,
            Builders<CodeReservation>.Update.Set(x => x.BindingState, CodeReservationBindingState.Confirmed));
        Assert.True((await repository.CompleteChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, fingerprint)).Succeeded);

        var current = (await repository.GetByIdAsync(pair.Gsku.Id))!;
        var operationId = Guid.NewGuid();
        var fence = await repository.CloseChildAdmissionFenceAsync(
            pair.Gsku.Id, current.Version, operationId, "retire-fingerprint");
        Assert.True(fence.Succeeded);
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await repository.FindRetirementBlockerAsync(pair.Gsku.Id));
        await _lskus.UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == child.Id,
            Builders<Lsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired));
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = pair.Gsku.Id,
            CanonicalCode = $"FG-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = "finished-good-existing",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            Version = 0
        };
        await _finishedGoods.InsertOneAsync(finishedGood);
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await repository.FindRetirementBlockerAsync(pair.Gsku.Id));
        var finishedGoodRepository = new FinishedGoodRepository(_database, new Tenant(_tenantId));
        var finishedGoodRetirementOperationId = Guid.NewGuid();
        var finishedGoodRetirementAudit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            finishedGood,
            finishedGood.Version,
            finishedGoodRetirementOperationId,
            Guid.NewGuid(),
            "OBSOLETE",
            null,
            DateTimeOffset.UtcNow);
        var retiredFinishedGood = await finishedGoodRepository.RetireIdentityAsync(
            finishedGood.Id, finishedGood.Version, finishedGoodRetirementAudit);
        Assert.True(retiredFinishedGood.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired,
            retiredFinishedGood.FinishedGood!.LifecycleStatus);
        Assert.Null(await repository.FindRetirementBlockerAsync(pair.Gsku.Id));
        var fencedGsku = fence.Aggregate!;
        var retiredGsku = await repository.RetireIdentityAsync(
            pair.Gsku.Id, fencedGsku.Version, operationId, "retire-fingerprint",
            Intent(fencedGsku, ProductAuditOperation.GskuIdentityRetired, fencedGsku.Version, "retire-gsku"));
        Assert.True(retiredGsku.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retiredGsku.Aggregate!.LifecycleStatus);
        var revisions = new ProductDefinitionRevisionRepository(_database, new Tenant(_tenantId));
        var retiredRevision = await revisions.RetireIdentityAsync(
            pair.Revision.Id, pair.Revision.Version, operationId, "retire-fingerprint",
            Intent(pair.Revision, ProductAuditOperation.ProductDefinitionRevisionIdentityRetired,
                pair.Revision.Version, "retire-revision"));
        Assert.True(retiredRevision.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retiredRevision.Aggregate!.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired,
            (await _lskus.Find(x => x.TenantId == _tenantId && x.Id == child.Id).SingleAsync()).LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired,
            (await _finishedGoods.Find(x => x.TenantId == _tenantId && x.Id == finishedGood.Id).SingleAsync()).LifecycleStatus);
        Assert.Equal(operationId, retiredRevision.Aggregate.RetirementOperationId);
    }

    [Fact]
    public async Task Child_repository_rejects_missing_admission_and_accepts_exact_kind_scoped_admission()
    {
        var pair = await SeedPairAsync();
        const string command = "admitted-lsku";
        var child = new Lsku
        {
            Id = Guid.NewGuid(), GskuId = pair.Gsku.Id, CanonicalCode = $"LS-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), CreationCommandId = command, MarketCode = "TR",
            MarketSelection = new ReferenceCatalogSelection
            {
                SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
                CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = DateTimeOffset.UtcNow
            },
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        child.AuditIntents.Add(new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenantId, AggregateType = AuditAggregateType.Lsku,
            AggregateId = child.Id, PreVersion = 0, PostVersion = 0,
            Operation = ProductAuditOperation.LskuDraftCreated, ActorId = "actor", CorrelationId = command,
            CausationId = command, CommandId = command, Sequence = 1, TimestampUtc = DateTimeOffset.UtcNow,
            EvidenceHash = "hash", IdempotencyKey = command
        });
        await _reservations.InsertOneAsync(new CodeReservation
        {
            Id = child.CodeReservationId, TenantId = _tenantId, EntityType = CodeBearingEntityType.Lsku,
            ReservedCode = child.CanonicalCode, ReservationState = CodeReservationState.Consumed,
            BindingState = CodeReservationBindingState.PendingIdentityWrite, ConsumedEntityId = child.Id,
            ReservationCommandId = command, ReservedAt = DateTimeOffset.UtcNow, ReservedByActorId = "test"
        });
        var children = new LskuRepository(_database, new Tenant(_tenantId));
        Assert.Equal("GSKU_NOT_REFERENCEABLE", (await children.CreateDraftAsync(child)).ErrorCode);
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, "TR");
        Assert.True((await GskuRepository().AcquireChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.Lsku, command, fingerprint, DateTimeOffset.UtcNow)).Succeeded);
        Assert.True((await children.CreateDraftWithAdmissionAsync(child, fingerprint)).Succeeded);
    }

    [Fact]
    public async Task Retirement_operation_is_exact_replayable_leased_and_recovers_each_checkpoint()
    {
        var pair = await SeedPairAsync();
        var repository = new FirstGskuIdentityRetirementOperationRepository(_database, new Tenant(_tenantId));
        var operation = new FirstGskuIdentityRetirementOperation
        {
            OperationId = Guid.NewGuid(), OperationFingerprint = "retirement-operation-fingerprint",
            ProductDefinitionRevisionId = pair.Revision.Id, GskuId = pair.Gsku.Id,
            ExpectedRevisionVersion = pair.Revision.Version, ExpectedGskuVersion = pair.Gsku.Version,
            ActorSubjectId = Guid.NewGuid(), ReasonCode = "IDENTITY_RETIREMENT",
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        Assert.True((await repository.ReserveAsync(new FirstGskuIdentityRetirementOperation
        {
            OperationId = operation.OperationId, OperationFingerprint = operation.OperationFingerprint,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId, GskuId = operation.GskuId,
            ExpectedRevisionVersion = operation.ExpectedRevisionVersion, ExpectedGskuVersion = operation.ExpectedGskuVersion,
            ActorSubjectId = operation.ActorSubjectId, ReasonCode = operation.ReasonCode,
            CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1
        })).IsReplay);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityRetirementCheckpoint.Prepared], "worker", now, now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);
        Assert.True(await repository.AdvanceAsync(claim!, new(
            FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1, ReleaseLease: true)));
        Assert.Equal(FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Single((await repository.DiscoverRecoverableAsync(now + 2, 10)).Operations,
            x => x.OperationId == operation.OperationId);
    }

    [Fact]
    public async Task Active_sibling_gsku_blocks_revision_pair_retirement_but_retired_sibling_does_not()
    {
        var pair = await SeedPairAsync();
        var sibling = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId,
            ProductDefinitionRevisionId = pair.Revision.Id,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        await _gskus.InsertOneAsync(sibling);
        var repository = GskuRepository();
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await repository.FindRetirementBlockerAsync(pair.Gsku.Id));
        await _gskus.UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == sibling.Id,
            Builders<Gsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired));
        Assert.Null(await repository.FindRetirementBlockerAsync(pair.Gsku.Id));
    }

    [Fact]
    public async Task Admission_and_retirement_operation_are_non_disclosing_across_tenants()
    {
        var pair = await SeedPairAsync();
        var command = $"cross-tenant-{Guid.NewGuid():N}";
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            pair.Gsku.Id, GskuChildIdentityKind.FinishedGood, command);
        var otherTenantGskus = new GskuRepository(_database, new Tenant(_otherTenantId));

        var admission = await otherTenantGskus.AcquireChildCreationAdmissionAsync(
            pair.Gsku.Id, GskuChildIdentityKind.FinishedGood, command, fingerprint, DateTimeOffset.UtcNow);

        Assert.False(admission.Succeeded);
        Assert.Equal("GSKU_NOT_FOUND", admission.ErrorCode);
        Assert.Null(admission.Gsku);

        var ownerOperations = new FirstGskuIdentityRetirementOperationRepository(
            _database, new Tenant(_tenantId));
        var operation = new FirstGskuIdentityRetirementOperation
        {
            OperationId = Guid.NewGuid(), OperationFingerprint = $"fp-{Guid.NewGuid():N}",
            ProductDefinitionRevisionId = pair.Revision.Id, GskuId = pair.Gsku.Id,
            ExpectedRevisionVersion = pair.Revision.Version, ExpectedGskuVersion = pair.Gsku.Version,
            ActorSubjectId = Guid.NewGuid(), ReasonCode = "IDENTITY_RETIREMENT",
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
        Assert.True((await ownerOperations.ReserveAsync(operation)).Succeeded);

        var otherTenantOperations = new FirstGskuIdentityRetirementOperationRepository(
            _database, new Tenant(_otherTenantId));
        Assert.Null(await otherTenantOperations.GetByOperationIdAsync(operation.OperationId));
        Assert.Empty((await otherTenantOperations.DiscoverRecoverableAsync(
            DateTimeOffset.UtcNow.AddHours(1).UtcTicks, 10)).Operations);
    }

    public async Task DisposeAsync()
    {
        var tenants = new[] { _tenantId, _otherTenantId };
        await _gskus.DeleteManyAsync(x => tenants.Contains(x.TenantId));
        await _revisions.DeleteManyAsync(x => tenants.Contains(x.TenantId));
        await _lskus.DeleteManyAsync(x => tenants.Contains(x.TenantId));
        await _finishedGoods.DeleteManyAsync(x => tenants.Contains(x.TenantId));
        await _reservations.DeleteManyAsync(x => tenants.Contains(x.TenantId));
        await _database.GetCollection<FirstGskuIdentityRetirementOperation>(
            FirstGskuIdentityRetirementOperationRepository.CollectionName)
            .DeleteManyAsync(x => tenants.Contains(x.TenantId));
    }

    private GskuRepository GskuRepository() => new(_database, new Tenant(_tenantId));

    private async Task<(ProductDefinitionRevision Revision, Gsku Gsku)> SeedPairAsync(
        ProductIdentityLifecycleStatus revisionStatus = ProductIdentityLifecycleStatus.IdentityApproved,
        ProductIdentityLifecycleStatus gskuStatus = ProductIdentityLifecycleStatus.IdentityApproved)
    {
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GlobalProductId = Guid.NewGuid(),
            RevisionIdentifier = $"REV-{Guid.NewGuid():N}", CreationCommandId = $"cmd-{Guid.NewGuid():N}",
            LifecycleStatus = revisionStatus, Version = 0
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = revision.CreationCommandId,
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            LifecycleStatus = gskuStatus, Version = 0
        };
        await _revisions.InsertOneAsync(revision);
        await _gskus.InsertOneAsync(gsku);
        return (revision, gsku);
    }

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
