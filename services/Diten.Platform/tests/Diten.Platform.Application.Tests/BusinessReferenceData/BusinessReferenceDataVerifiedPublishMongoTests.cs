using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedPublishMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BusinessReferenceDataTestHarness.CreateAsync();
    }

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Theory]
    [InlineData(BusinessReferenceDataPublishCheckpoint.INITIALIZED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED)]
    public async Task CrashAtEachCheckpoint_ReplaysSameOperationToVerifiedCompletion(
        BusinessReferenceDataPublishCheckpoint checkpoint)
    {
        var observer = new ThrowOnceAtCheckpointObserver(checkpoint);
        await Assert.ThrowsAsync<InjectedPublishCrashException>(() => _harness.CreateLoader(observer).LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "test-publisher",
            ["pack-applicability", "uom"]));

        var interrupted = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && x.OperationState == BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED)
            .FirstOrDefaultAsync();
        Assert.NotNull(interrupted);
        Assert.Equal(checkpoint, interrupted.PublishCheckpoint);
        var operationId = interrupted.PublishOperationId;

        var replay = await _harness.CreateLoader().LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "test-publisher",
            ["pack-applicability", "uom"]);
        var completed = await _harness.Repository.GetPublishOperationByIdAsync(operationId);

        Assert.Empty(replay.BlockedConflicts);
        Assert.NotNull(completed);
        Assert.Equal(operationId, completed.PublishOperationId);
        Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, completed.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, completed.PublishCheckpoint);
        Assert.True(await _harness.Repository.IsPublishOperationVerifiedAsync(operationId));
    }

    [Fact]
    public Task StalePublishedPointer_IsRejectedAndOtherTenantCannotSatisfyFence()
        => AssertStalePreMutationContextAsync(StaleFence.Pointer);

    [Fact]
    public Task StaleSetRowVersion_IsRejectedAndOtherTenantCannotSatisfyFence()
        => AssertStalePreMutationContextAsync(StaleFence.SetRowVersion);

    [Fact]
    public Task StaleTargetConcurrencyToken_IsRejectedAndOtherTenantCannotSatisfyFence()
        => AssertStalePreMutationContextAsync(StaleFence.TargetToken);

    [Fact]
    public async Task PostPointerMismatch_RemainsRecoveryRequiredAndNeverClaimsCompleted()
    {
        var observer = new ThrowOnceAtCheckpointObserver(BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED);
        await Assert.ThrowsAsync<InjectedPublishCrashException>(() => _harness.CreateLoader(observer).LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "test-publisher",
            ["pack-applicability", "uom"]));

        var operation = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && x.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED)
            .FirstAsync();
        await _harness.Database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions").UpdateOneAsync(
            x => x.TenantId == _harness.ReferenceTenantId
                 && x.BusinessReferenceDataVersionId == operation.BusinessReferenceDataVersionId,
            Builders<BusinessReferenceDataVersion>.Update.Set(x => x.LastPublishIdempotencyKey, "tampered-operation"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.CreateLoader().LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "test-publisher",
            ["pack-applicability", "uom"]));
        var persisted = await _harness.Repository.GetPublishOperationByIdAsync(operation.PublishOperationId);

        Assert.NotNull(persisted);
        Assert.Equal(BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED, persisted.OperationState);
        Assert.Null(persisted.CompletedAt);
        Assert.False(await _harness.Repository.IsPublishOperationVerifiedAsync(operation.PublishOperationId));
    }

    private async Task AssertStalePreMutationContextAsync(StaleFence staleFence)
    {
        var tenantId = _harness.ReferenceTenantId;
        var otherTenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var expectedPointer = Guid.NewGuid();
        const long expectedSetVersion = 7;
        const string expectedToken = "expected-target-token";
        var operation = new BusinessReferenceDataPublishOperation
        {
            TenantId = tenantId,
            BusinessReferenceDataSetId = setId,
            BusinessReferenceDataVersionId = versionId,
            IdempotencyKey = $"stale-{staleFence}",
            ExpectedPublishedVersionId = expectedPointer,
            ExpectedSetVersion = expectedSetVersion,
            ExpectedTargetVersionToken = expectedToken,
            CreatedBy = "test"
        };

        await SeedContextAsync(
            tenantId,
            setId,
            versionId,
            staleFence == StaleFence.Pointer ? Guid.NewGuid() : expectedPointer,
            staleFence == StaleFence.SetRowVersion ? expectedSetVersion + 1 : expectedSetVersion,
            staleFence == StaleFence.TargetToken ? "newer-target-token" : expectedToken);
        await SeedContextAsync(otherTenantId, setId, versionId, expectedPointer, expectedSetVersion, expectedToken);
        await _harness.Repository.CreateOrGetPublishOperationAsync(operation);

        var transitioned = await _harness.Repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            1,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.INITIALIZED,
            "test-publisher");
        var persisted = await _harness.Repository.GetPublishOperationByIdAsync(operation.PublishOperationId);

        Assert.False(transitioned);
        Assert.NotNull(persisted);
        Assert.Equal(BusinessReferenceDataPublishOperationState.FAILED_TERMINAL, persisted.OperationState);
        Assert.Equal("REFERENCE_PUBLISH_OPERATION_STALE", persisted.LastErrorCode);
        Assert.Null(persisted.CompletedAt);
    }

    private async Task SeedContextAsync(
        Guid tenantId,
        Guid setId,
        Guid versionId,
        Guid? pointer,
        long rowVersion,
        string targetToken)
    {
        await _harness.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets").InsertOneAsync(
            new BusinessReferenceDataSet
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                SetCode = $"test-{tenantId:N}",
                Name = "Test",
                ScopeType = "global",
                PublishedVersionId = pointer,
                RowVersion = rowVersion,
                CreatedBy = "test"
            });
        await _harness.Database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions").InsertOneAsync(
            new BusinessReferenceDataVersion
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                BusinessReferenceDataVersionId = versionId,
                VersionNumber = 1,
                ConcurrencyToken = targetToken,
                CreatedBy = "test"
            });
    }

    private enum StaleFence
    {
        Pointer,
        SetRowVersion,
        TargetToken
    }
}

internal sealed class ThrowOnceAtCheckpointObserver : IBusinessReferenceDataPublishCheckpointObserver
{
    private readonly BusinessReferenceDataPublishCheckpoint _checkpoint;
    private bool _thrown;

    public ThrowOnceAtCheckpointObserver(BusinessReferenceDataPublishCheckpoint checkpoint)
    {
        _checkpoint = checkpoint;
    }

    public Task OnCheckpointPersistedAsync(BusinessReferenceDataPublishOperation operation, CancellationToken ct = default)
    {
        if (!_thrown && operation.PublishCheckpoint == _checkpoint)
        {
            _thrown = true;
            throw new InjectedPublishCrashException();
        }

        return Task.CompletedTask;
    }
}

internal sealed class InjectedPublishCrashException : Exception;
