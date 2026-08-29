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
public sealed class GlobalProductChildAdmissionMongoTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProduct> _products = null!;
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
        _products = _database.GetCollection<GlobalProduct>("mdm_global_products");
        _revisions = _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
    }

    [Fact]
    public async Task Admission_exact_replay_drift_completion_and_completion_replay_are_deterministic()
    {
        var product = await SeedProductAsync();
        var repository = Repository();
        var reservationId = Guid.NewGuid();
        var fingerprint = ProductChildCreationAdmission.ComputeRequestFingerprint(
            product.Id, "GSKU:ONE", reservationId, 1m, "EA");

        var acquired = await repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", fingerprint, Now);
        var replay = await repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", fingerprint, Now.AddMinutes(1));
        var drift = await repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", "fingerprint-B", Now);
        var premature = await repository.CompleteChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", fingerprint);
        await SeedPairAsync(
            product.Id, "GSKU:ONE", ProductIdentityLifecycleStatus.Draft, reservationId);
        var completed = await repository.CompleteChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", fingerprint);
        var completionReplay = await repository.CompleteChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", fingerprint);
        var completionDrift = await repository.CompleteChildCreationAdmissionAsync(
            product.Id, "GSKU:ONE", "wrong-fingerprint");

        Assert.True(acquired.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("PRODUCT_CHILD_ADMISSION_CONFLICT", drift.ErrorCode);
        Assert.False(premature.Succeeded);
        Assert.Equal("PRODUCT_CHILD_BINDING_NOT_DURABLE", premature.ErrorCode);
        Assert.True(completed.Succeeded);
        Assert.True(completionReplay.Succeeded);
        Assert.True(completionReplay.IsReplay);
        Assert.False(completionDrift.Succeeded);
        Assert.Equal("PRODUCT_CHILD_ADMISSION_CONFLICT", completionDrift.ErrorCode);
        Assert.Empty((await Repository().GetByIdAsync(product.Id))!.ChildCreationAdmissions);
    }

    [Fact]
    public async Task Admission_capacity_is_exactly_32_and_not_tenant_configurable()
    {
        var product = await SeedProductAsync();
        for (var index = 0; index < ProductChildCreationAdmission.MaximumActiveAdmissions; index++)
        {
            var result = await Repository().AcquireChildCreationAdmissionAsync(
                product.Id, $"GSKU:{index:D2}", $"fingerprint-{index:D2}", Now);
            Assert.True(result.Succeeded);
        }

        var overflow = await Repository().AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:OVERFLOW", "overflow-fingerprint", Now);

        Assert.False(overflow.Succeeded);
        Assert.Equal("PRODUCT_CHILD_ADMISSION_LIMIT_REACHED", overflow.ErrorCode);
        Assert.Equal(32, (await Repository().GetByIdAsync(product.Id))!.ChildCreationAdmissions.Count);
    }

    [Fact]
    public async Task Retirement_and_child_admission_race_cannot_both_succeed()
    {
        var product = await SeedProductAsync();
        var repository = Repository();
        var retire = repository.RetireIdentityAsync(
            product.Id, 0, Intent(product.Id, 0, "retire-race", "retire-fingerprint"));
        var acquire = repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:RACE", "child-fingerprint", Now);

        await Task.WhenAll(retire, acquire);
        var retireResult = await retire;
        var acquireResult = await acquire;

        Assert.NotEqual(retireResult.Succeeded, acquireResult.Succeeded);
        var stored = await Repository().GetByIdAsync(product.Id);
        Assert.NotNull(stored);
        if (retireResult.Succeeded)
        {
            Assert.Equal(ProductIdentityLifecycleStatus.Retired, stored!.LifecycleStatus);
            Assert.Empty(stored.ChildCreationAdmissions);
        }
        else
        {
            Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, stored!.LifecycleStatus);
            Assert.Single(stored.ChildCreationAdmissions);
        }
    }

    [Fact]
    public async Task Crash_left_admission_blocks_retirement_until_same_operation_completes()
    {
        var product = await SeedProductAsync();
        var repository = Repository();
        var reservationId = Guid.NewGuid();
        var fingerprint = ProductChildCreationAdmission.ComputeRequestFingerprint(
            product.Id, "GSKU:CRASH", reservationId, 1m, "EA");
        Assert.True((await repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:CRASH", fingerprint, Now)).Succeeded);

        var blocked = await repository.RetireIdentityAsync(
            product.Id, 0, Intent(product.Id, 0, "retire-blocked", "retire-blocked-fingerprint"));
        var recoveryReplay = await repository.AcquireChildCreationAdmissionAsync(
            product.Id, "GSKU:CRASH", fingerprint, Now.AddHours(1));
        Assert.False(blocked.Succeeded);
        Assert.Equal("PRODUCT_CHILD_CREATION_IN_PROGRESS", blocked.ErrorCode);
        Assert.True(recoveryReplay.Succeeded);
        Assert.True(recoveryReplay.IsReplay);
        await SeedPairAsync(
            product.Id, "GSKU:CRASH", ProductIdentityLifecycleStatus.Retired, reservationId);
        Assert.True((await repository.CompleteChildCreationAdmissionAsync(
            product.Id, "GSKU:CRASH", fingerprint)).Succeeded);

        var retired = await repository.RetireIdentityAsync(
            product.Id, 0, Intent(product.Id, 0, "retire-recovered", "retire-recovered-fingerprint"));
        Assert.True(retired.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retired.GlobalProduct!.LifecycleStatus);
        Assert.Single(retired.GlobalProduct.AuditIntents);
    }

    [Fact]
    public async Task Nonretired_children_block_without_cascade_but_retired_history_does_not()
    {
        var draftParent = await SeedProductAsync();
        var draftRevision = await SeedRevisionAsync(draftParent.Id, ProductIdentityLifecycleStatus.Draft);
        await SeedGskuAsync(draftRevision.Id, ProductIdentityLifecycleStatus.Retired);
        var draftBlocked = await Repository().RetireIdentityAsync(
            draftParent.Id, 0, Intent(draftParent.Id, 0, "draft-block", "draft-block-fingerprint"));
        Assert.False(draftBlocked.Succeeded);
        Assert.Equal("DRAFT_CHILD_CANCELLATION_REQUIRED", draftBlocked.ErrorCode);

        var approvedParent = await SeedProductAsync();
        await SeedRevisionAsync(approvedParent.Id, ProductIdentityLifecycleStatus.IdentityApproved);
        var approvedBlocked = await Repository().RetireIdentityAsync(
            approvedParent.Id, 0, Intent(approvedParent.Id, 0, "approved-block", "approved-block-fingerprint"));
        Assert.False(approvedBlocked.Succeeded);
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", approvedBlocked.ErrorCode);

        var historicalParent = await SeedProductAsync();
        var retiredRevision = await SeedRevisionAsync(historicalParent.Id, ProductIdentityLifecycleStatus.Retired);
        var retiredGsku = await SeedGskuAsync(retiredRevision.Id, ProductIdentityLifecycleStatus.Retired);
        var retired = await Repository().RetireIdentityAsync(
            historicalParent.Id, 0, Intent(historicalParent.Id, 0, "history-retire", "history-fingerprint"));
        Assert.True(retired.Succeeded);
        Assert.False((await _revisions.Find(item => item.Id == retiredRevision.Id).SingleAsync()).IsDeleted);
        Assert.False((await _gskus.Find(item => item.Id == retiredGsku.Id).SingleAsync()).IsDeleted);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired,
            (await _revisions.Find(item => item.Id == retiredRevision.Id).SingleAsync()).LifecycleStatus);
    }

    [Fact]
    public async Task Soft_deleted_revision_cannot_hide_active_gsku_from_retirement_fence()
    {
        var activeChildParent = await SeedProductAsync();
        var deletedRevision = await SeedRevisionAsync(
            activeChildParent.Id,
            ProductIdentityLifecycleStatus.Retired);
        await _revisions.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.Id == deletedRevision.Id,
            Builders<ProductDefinitionRevision>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, Now));
        await SeedGskuAsync(deletedRevision.Id, ProductIdentityLifecycleStatus.IdentityApproved);

        var blocked = await Repository().RetireIdentityAsync(
            activeChildParent.Id,
            0,
            Intent(activeChildParent.Id, 0, "soft-deleted-revision-block", "active-gsku-fingerprint"));

        Assert.False(blocked.Succeeded);
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", blocked.ErrorCode);
        Assert.Equal(
            ProductIdentityLifecycleStatus.IdentityApproved,
            (await Repository().GetByIdAsync(activeChildParent.Id))!.LifecycleStatus);

        var retiredChildParent = await SeedProductAsync();
        var secondDeletedRevision = await SeedRevisionAsync(
            retiredChildParent.Id,
            ProductIdentityLifecycleStatus.Retired);
        await _revisions.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.Id == secondDeletedRevision.Id,
            Builders<ProductDefinitionRevision>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, Now));
        await SeedGskuAsync(secondDeletedRevision.Id, ProductIdentityLifecycleStatus.Retired);

        var retired = await Repository().RetireIdentityAsync(
            retiredChildParent.Id,
            0,
            Intent(retiredChildParent.Id, 0, "soft-deleted-revision-history", "retired-gsku-fingerprint"));

        Assert.True(retired.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, retired.GlobalProduct!.LifecycleStatus);
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _products.DeleteManyAsync(item => item.TenantId == _tenantId),
            _revisions.DeleteManyAsync(item => item.TenantId == _tenantId),
            _gskus.DeleteManyAsync(item => item.TenantId == _tenantId));
    }

    private GlobalProductRepository Repository() => new(_database, new Tenant(_tenantId));

    private async Task<GlobalProduct> SeedProductAsync()
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = $"GP-{Guid.NewGuid():N}",
            GlobalProductName = "Admission product", GlobalProductNameNormalized = $"PRODUCT-{Guid.NewGuid():N}",
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = Now, UpdatedAt = Now
        };
        await _products.InsertOneAsync(product);
        return product;
    }

    private async Task<ProductDefinitionRevision> SeedRevisionAsync(
        Guid productId,
        ProductIdentityLifecycleStatus status)
    {
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GlobalProductId = productId,
            RevisionIdentifier = $"REV-{Guid.NewGuid():N}", CreationCommandId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = status, CreatedAt = Now, UpdatedAt = Now
        };
        await _revisions.InsertOneAsync(revision);
        return revision;
    }

    private async Task<Gsku> SeedGskuAsync(Guid revisionId, ProductIdentityLifecycleStatus status)
    {
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductDefinitionRevisionId = revisionId,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("D"), PackApplicabilityCode = "PACK",
            PackQuantity = 1, PackUomCode = "EA", LifecycleStatus = status, CreatedAt = Now, UpdatedAt = Now
        };
        await _gskus.InsertOneAsync(gsku);
        return gsku;
    }

    private async Task SeedPairAsync(
        Guid productId,
        string creationCommandId,
        ProductIdentityLifecycleStatus status,
        Guid? reservationId = null)
    {
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GlobalProductId = productId,
            RevisionIdentifier = $"REV-{Guid.NewGuid():N}", CreationCommandId = creationCommandId,
            LifecycleStatus = status, CreatedAt = Now, UpdatedAt = Now
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = $"GS-{Guid.NewGuid():N}", CodeReservationId = reservationId ?? Guid.NewGuid(),
            CreationCommandId = creationCommandId, PackApplicabilityCode = "PACK", PackQuantity = 1,
            PackUomCode = "EA", LifecycleStatus = status, CreatedAt = Now, UpdatedAt = Now
        };
        await _revisions.InsertOneAsync(revision);
        await _gskus.InsertOneAsync(gsku);
    }

    private LocalAuditIntent Intent(
        Guid productId,
        int expectedVersion,
        string key,
        string fingerprint) => new()
    {
        IntentId = Guid.NewGuid(), TenantId = _tenantId, AggregateType = AuditAggregateType.GlobalProduct,
        AggregateId = productId, PreVersion = expectedVersion, PostVersion = expectedVersion + 1,
        Operation = ProductAuditOperation.GlobalProductIdentityRetired, ActorId = Guid.NewGuid().ToString("D"),
        CorrelationId = Guid.NewGuid().ToString("D"), CausationId = Guid.NewGuid().ToString("D"),
        CommandId = key, Sequence = expectedVersion + 1L, TimestampUtc = Now,
        EvidenceHash = fingerprint, IdempotencyKey = key
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
