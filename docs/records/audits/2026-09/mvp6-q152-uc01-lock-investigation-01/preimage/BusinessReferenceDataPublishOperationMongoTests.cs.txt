using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataPublishOperationMongoTests : IAsyncLifetime
{
    private string _databaseName = null!;
    private MongoClient _client = null!;
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
        _databaseName = await BusinessReferenceDataMongoResidueSweeper.CreateDatabaseAsync(_client, "pub");
        _database = _client.GetDatabase(_databaseName);
        await _database.RunCommandAsync<object>("{ ping: 1 }");
        await PlatformSchemaManifest.ApplyAsync(
            _database,
            new[] { SchemaProfile.BusinessReferenceData });
    }

    public Task DisposeAsync()
    {
        return _client.DropDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Idempotency_SameFingerprintReplaysAndDifferentTargetConflicts()
    {
        var tenantId = Guid.NewGuid();
        var (repository, _) = CreateRepository(tenantId);
        var operation = CreateOperation(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "publish-001",
            Guid.NewGuid(),
            expectedSetVersion: 7,
            expectedTargetVersionToken: "captured-target-token");

        var created = await repository.CreateOrGetPublishOperationAsync(operation);
        var replayed = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(
                tenantId,
                operation.BusinessReferenceDataSetId,
                operation.BusinessReferenceDataVersionId,
                "publish-001",
                operation.ExpectedPublishedVersionId,
                operation.ExpectedSetVersion,
                operation.ExpectedTargetVersionToken));
        var conflict = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(
                tenantId,
                operation.BusinessReferenceDataSetId,
                Guid.NewGuid(),
                "publish-001",
                operation.ExpectedPublishedVersionId,
                operation.ExpectedSetVersion,
                operation.ExpectedTargetVersionToken));

        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Created, created.Outcome);
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Replayed, replayed.Outcome);
        Assert.Equal(operation.PublishOperationId, replayed.Operation.PublishOperationId);
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Conflict, conflict.Outcome);

        var collection = _database.GetCollection<BusinessReferenceDataPublishOperation>(
            "business_reference_data_publish_operations");
        await collection.UpdateOneAsync(
            Builders<BusinessReferenceDataPublishOperation>.Filter.Eq(x => x.PublishOperationId, operation.PublishOperationId),
            Builders<BusinessReferenceDataPublishOperation>.Update.Set(x => x.IsDeleted, true));
        var replacement = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(tenantId, operation.BusinessReferenceDataSetId, Guid.NewGuid(), "publish-001"));
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Created, replacement.Outcome);
    }

    [Fact]
    public async Task Idempotency_DifferentExpectedPublishedVersionConflicts()
    {
        var tenantId = Guid.NewGuid();
        var (repository, _) = CreateRepository(tenantId);
        var operation = CreateOperation(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "publish-pointer-fence",
            Guid.NewGuid());

        var created = await repository.CreateOrGetPublishOperationAsync(operation);
        var conflict = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(
                tenantId,
                operation.BusinessReferenceDataSetId,
                operation.BusinessReferenceDataVersionId,
                operation.IdempotencyKey,
                Guid.NewGuid(),
                operation.ExpectedSetVersion,
                operation.ExpectedTargetVersionToken));

        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Created, created.Outcome);
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Conflict, conflict.Outcome);
        Assert.Equal(operation.PublishOperationId, conflict.Operation.PublishOperationId);
    }

    [Fact]
    public async Task Idempotency_DifferentExpectedSetVersionConflicts()
    {
        var tenantId = Guid.NewGuid();
        var (repository, _) = CreateRepository(tenantId);
        var operation = CreateOperation(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "publish-set-fence",
            Guid.NewGuid(),
            expectedSetVersion: 7);

        var created = await repository.CreateOrGetPublishOperationAsync(operation);
        var conflict = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(
                tenantId,
                operation.BusinessReferenceDataSetId,
                operation.BusinessReferenceDataVersionId,
                operation.IdempotencyKey,
                operation.ExpectedPublishedVersionId,
                operation.ExpectedSetVersion + 1,
                operation.ExpectedTargetVersionToken));

        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Created, created.Outcome);
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Conflict, conflict.Outcome);
        Assert.Equal(operation.PublishOperationId, conflict.Operation.PublishOperationId);
    }

    [Fact]
    public async Task Idempotency_DifferentExpectedTargetVersionTokenConflicts()
    {
        var tenantId = Guid.NewGuid();
        var (repository, _) = CreateRepository(tenantId);
        var operation = CreateOperation(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "publish-target-fence",
            Guid.NewGuid(),
            expectedSetVersion: 7,
            expectedTargetVersionToken: "captured-target-token");

        var created = await repository.CreateOrGetPublishOperationAsync(operation);
        var conflict = await repository.CreateOrGetPublishOperationAsync(
            CreateOperation(
                tenantId,
                operation.BusinessReferenceDataSetId,
                operation.BusinessReferenceDataVersionId,
                operation.IdempotencyKey,
                operation.ExpectedPublishedVersionId,
                operation.ExpectedSetVersion,
                "different-target-token"));

        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Created, created.Outcome);
        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Conflict, conflict.Outcome);
        Assert.Equal(operation.PublishOperationId, conflict.Operation.PublishOperationId);
    }

    [Fact]
    public async Task Operation_IsTenantIsolatedAndExpectedVersionFencesConcurrentRetry()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (repositoryA, _) = CreateRepository(tenantA);
        var (repositoryB, _) = CreateRepository(tenantB);
        var operationA = CreateOperation(tenantA, Guid.NewGuid(), Guid.NewGuid(), "publish-shared");
        var operationB = CreateOperation(tenantB, operationA.BusinessReferenceDataSetId, operationA.BusinessReferenceDataVersionId, "publish-shared");
        await SeedPreMutationContextAsync(
            tenantA,
            operationA.BusinessReferenceDataSetId,
            operationA.BusinessReferenceDataVersionId,
            operationA.ExpectedPublishedVersionId,
            operationA.ExpectedSetVersion,
            operationA.ExpectedTargetVersionToken);
        await SeedPreMutationContextAsync(
            tenantB,
            operationB.BusinessReferenceDataSetId,
            operationB.BusinessReferenceDataVersionId,
            operationB.ExpectedPublishedVersionId,
            operationB.ExpectedSetVersion,
            operationB.ExpectedTargetVersionToken);
        await repositoryA.CreateOrGetPublishOperationAsync(operationA);
        await repositoryB.CreateOrGetPublishOperationAsync(operationB);

        Assert.Null(await repositoryB.GetPublishOperationByIdAsync(operationA.PublishOperationId));
        Assert.Equal(operationB.PublishOperationId, (await repositoryB.GetPublishOperationByIdempotencyKeyAsync("publish-shared"))?.PublishOperationId);

        Assert.True(await repositoryA.TransitionPublishOperationAsync(
            operationA.PublishOperationId,
            1,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.INITIALIZED,
            "publisher"));

        var attempts = await Task.WhenAll(
            repositoryA.TransitionPublishOperationAsync(
                operationA.PublishOperationId,
                2,
                BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
                BusinessReferenceDataPublishCheckpoint.INITIALIZED,
                "publisher-a",
                "INTERRUPTED"),
            repositoryA.TransitionPublishOperationAsync(
                operationA.PublishOperationId,
                2,
                BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
                BusinessReferenceDataPublishCheckpoint.INITIALIZED,
                "publisher-b",
                "INTERRUPTED"));

        Assert.Single(attempts, value => value);
        var recovery = await repositoryA.GetPublishOperationByIdAsync(operationA.PublishOperationId);
        Assert.NotNull(recovery);
        Assert.Equal(BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED, recovery.OperationState);
        Assert.Equal("INTERRUPTED", recovery.LastErrorCode);

        Assert.True(await repositoryA.TransitionPublishOperationAsync(
            operationA.PublishOperationId,
            recovery.Version,
            BusinessReferenceDataPublishOperationState.RUNNING,
            recovery.PublishCheckpoint,
            "recovery-worker"));
        var resumed = await repositoryA.GetPublishOperationByIdAsync(operationA.PublishOperationId);
        Assert.Equal(1, resumed?.RetryCount);
    }

    [Fact]
    public async Task Completion_IsRejectedUntilPointerTargetAndCheckpointAgree()
    {
        var tenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var (repository, _) = CreateRepository(tenantId);
        var priorPublishedVersionId = Guid.NewGuid();
        var operation = CreateOperation(
            tenantId,
            setId,
            versionId,
            "publish-verified",
            priorPublishedVersionId,
            expectedSetVersion: 7,
            expectedTargetVersionToken: "pre-target-token");
        await SeedPreMutationContextAsync(
            tenantId,
            setId,
            versionId,
            priorPublishedVersionId,
            operation.ExpectedSetVersion,
            operation.ExpectedTargetVersionToken);
        await repository.CreateOrGetPublishOperationAsync(operation);

        var version = 1;
        version = await AdvanceAsync(repository, operation.PublishOperationId, version, BusinessReferenceDataPublishCheckpoint.INITIALIZED);
        var started = await repository.GetPublishOperationByIdAsync(operation.PublishOperationId);
        Assert.NotNull(started?.PreMutationContextVerifiedAt);

        const string postTargetVersionToken = "post-target-token";
        await _database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions").UpdateOneAsync(
            Builders<BusinessReferenceDataVersion>.Filter.And(
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, tenantId),
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId)),
            Builders<BusinessReferenceDataVersion>.Update
                .Set(x => x.Status, BusinessReferenceDataVersionStatus.Published)
                .Set(x => x.IsImmutable, true)
                .Set(x => x.ConcurrencyToken, postTargetVersionToken)
                .Set(x => x.LastPublishIdempotencyKey, "different-operation"));
        version = await AdvanceAsync(repository, operation.PublishOperationId, version, BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN);
        version = await AdvanceAsync(repository, operation.PublishOperationId, version, BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED);
        version = await AdvanceAsync(repository, operation.PublishOperationId, version, BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED);
        await _database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets").UpdateOneAsync(
            Builders<BusinessReferenceDataSet>.Filter.And(
                Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, tenantId),
                Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
                Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.RowVersion, operation.ExpectedSetVersion)),
            Builders<BusinessReferenceDataSet>.Update
                .Set(x => x.PublishedVersionId, versionId)
                .Set(x => x.RowVersion, operation.ExpectedSetVersion + 1));
        version = await AdvanceAsync(repository, operation.PublishOperationId, version, BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED);

        Assert.False(await repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            version,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
            "publisher"));

        await _database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions").UpdateOneAsync(
            Builders<BusinessReferenceDataVersion>.Filter.And(
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, tenantId),
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId)),
            Builders<BusinessReferenceDataVersion>.Update.Set(x => x.LastPublishIdempotencyKey, operation.IdempotencyKey));
        Assert.True(await repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            version,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
            "publisher"));
        version++;

        Assert.True(await repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            version,
            BusinessReferenceDataPublishOperationState.COMPLETED,
            BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
            "publisher"));
        var completed = await repository.GetPublishOperationByIdAsync(operation.PublishOperationId);

        Assert.NotNull(completed);
        Assert.NotNull(completed.CompletedAt);
        Assert.True(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            completed,
            versionId,
            operation.ExpectedSetVersion + 1,
            postTargetVersionToken,
            true));
        Assert.False(await repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            completed.Version,
            BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
            BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
            "publisher",
            "LATE_FAILURE"));
    }

    [Fact]
    public async Task StalePublishedPointer_IsRejectedWithoutUsingAnotherTenantMatch()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var expectedPointer = Guid.NewGuid();
        var operation = CreateOperation(tenantId, setId, versionId, "stale-pointer", expectedPointer, 11, "target-token");
        var (repository, _) = CreateRepository(tenantId);

        await SeedPreMutationContextAsync(tenantId, setId, versionId, Guid.NewGuid(), 11, "target-token");
        await SeedPreMutationContextAsync(otherTenantId, setId, versionId, expectedPointer, 11, "target-token");
        await repository.CreateOrGetPublishOperationAsync(operation);

        await AssertStaleContextRejectedAsync(repository, operation);
    }

    [Fact]
    public async Task StaleSetRowVersion_IsRejectedWithoutUsingAnotherTenantMatch()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var expectedPointer = Guid.NewGuid();
        var operation = CreateOperation(tenantId, setId, versionId, "stale-set-version", expectedPointer, 17, "target-token");
        var (repository, _) = CreateRepository(tenantId);

        await SeedPreMutationContextAsync(tenantId, setId, versionId, expectedPointer, 18, "target-token");
        await SeedPreMutationContextAsync(otherTenantId, setId, versionId, expectedPointer, 17, "target-token");
        await repository.CreateOrGetPublishOperationAsync(operation);

        await AssertStaleContextRejectedAsync(repository, operation);
    }

    [Fact]
    public async Task StaleTargetConcurrencyToken_IsRejectedWithoutUsingAnotherTenantMatch()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var expectedPointer = Guid.NewGuid();
        var operation = CreateOperation(tenantId, setId, versionId, "stale-target-token", expectedPointer, 23, "expected-target-token");
        var (repository, _) = CreateRepository(tenantId);

        await SeedPreMutationContextAsync(tenantId, setId, versionId, expectedPointer, 23, "newer-target-token");
        await SeedPreMutationContextAsync(otherTenantId, setId, versionId, expectedPointer, 23, "expected-target-token");
        await repository.CreateOrGetPublishOperationAsync(operation);

        await AssertStaleContextRejectedAsync(repository, operation);
    }

    [Fact]
    public async Task PublishOperationIndex_IsPartialUniqueOnTenantAndIdempotencyKey()
    {
        var collection = _database.GetCollection<BusinessReferenceDataPublishOperation>(
            "business_reference_data_publish_operations");
        using var cursor = await collection.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var index = Assert.Single(indexes, x => x["name"] == "ux_business_reference_data_publish_operations_idempotency");

        Assert.True(index["unique"].AsBoolean);
        Assert.Equal(1, index["key"][nameof(BusinessReferenceDataPublishOperation.TenantId)].AsInt32);
        Assert.Equal(1, index["key"][nameof(BusinessReferenceDataPublishOperation.IdempotencyKey)].AsInt32);
        Assert.False(index["partialFilterExpression"][nameof(BusinessReferenceDataPublishOperation.IsDeleted)].AsBoolean);
    }

    private (BusinessReferenceDataStewardshipRepository Repository, TenantContext Context) CreateRepository(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        var dbContext = new PlatformDbContext(_client, _database);
        return (new BusinessReferenceDataStewardshipRepository(dbContext, context), context);
    }

    private static BusinessReferenceDataPublishOperation CreateOperation(
        Guid tenantId,
        Guid setId,
        Guid versionId,
        string idempotencyKey,
        Guid? expectedPublishedVersionId = null,
        long expectedSetVersion = 1,
        string expectedTargetVersionToken = "target-token")
    {
        return new BusinessReferenceDataPublishOperation
        {
            TenantId = tenantId,
            BusinessReferenceDataSetId = setId,
            BusinessReferenceDataVersionId = versionId,
            IdempotencyKey = idempotencyKey,
            ExpectedPublishedVersionId = expectedPublishedVersionId,
            ExpectedSetVersion = expectedSetVersion,
            ExpectedTargetVersionToken = expectedTargetVersionToken,
            CreatedBy = "test"
        };
    }

    private async Task SeedPreMutationContextAsync(
        Guid tenantId,
        Guid setId,
        Guid versionId,
        Guid? publishedVersionId,
        long setVersion,
        string targetVersionToken)
    {
        await _database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets").InsertOneAsync(
            new BusinessReferenceDataSet
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                SetCode = $"test-{setId:N}",
                Name = "Test Set",
                ScopeType = "EnterpriseGlobal",
                PublishedVersionId = publishedVersionId,
                RowVersion = setVersion,
                CreatedBy = "test"
            });
        await _database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions").InsertOneAsync(
            new BusinessReferenceDataVersion
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                BusinessReferenceDataVersionId = versionId,
                VersionNumber = 1,
                ConcurrencyToken = targetVersionToken,
                CreatedBy = "test"
            });
    }

    private static async Task AssertStaleContextRejectedAsync(
        BusinessReferenceDataStewardshipRepository repository,
        BusinessReferenceDataPublishOperation operation)
    {
        Assert.False(await repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            expectedVersion: 1,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.INITIALIZED,
            "publisher"));

        var persisted = await repository.GetPublishOperationByIdAsync(operation.PublishOperationId);
        Assert.NotNull(persisted);
        Assert.Equal(BusinessReferenceDataPublishOperationState.FAILED_TERMINAL, persisted.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.INITIALIZED, persisted.PublishCheckpoint);
        Assert.Equal("REFERENCE_PUBLISH_OPERATION_STALE", persisted.LastErrorCode);
        Assert.Null(persisted.PreMutationContextVerifiedAt);
        Assert.Null(persisted.CompletedAt);
    }

    private static async Task<int> AdvanceAsync(
        BusinessReferenceDataStewardshipRepository repository,
        Guid operationId,
        int expectedVersion,
        BusinessReferenceDataPublishCheckpoint nextCheckpoint)
    {
        Assert.True(await repository.TransitionPublishOperationAsync(
            operationId,
            expectedVersion,
            BusinessReferenceDataPublishOperationState.RUNNING,
            nextCheckpoint,
            "publisher"));
        return expectedVersion + 1;
    }
}
