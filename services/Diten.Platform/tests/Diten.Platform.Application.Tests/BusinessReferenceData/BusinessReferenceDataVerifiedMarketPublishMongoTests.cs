using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketPublishMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BusinessReferenceDataTestHarness.CreateAsync();

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Fact]
    public async Task VerifiedPublish_ReplayKeepsOneImmutableTargetAndOneCompletedOperation()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            var loader = _harness.CreateLoader();
            var first = await loader.LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test");
            var replay = await loader.LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test");

            Assert.Equal(1, first.SetsLoaded);
            Assert.Equal(1, replay.SetsAlreadyLoaded);
            Assert.Equal(first.CatalogFingerprint, replay.CatalogFingerprint);
            var set = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
                .Find(x => x.TenantId == _harness.ReferenceTenantId && x.SetCode == "market")
                .ToListAsync());
            var version = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions")
                .Find(x => x.TenantId == _harness.ReferenceTenantId
                           && x.BusinessReferenceDataSetId == set.BusinessReferenceDataSetId)
                .ToListAsync());
            var operation = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
                .Find(x => x.TenantId == _harness.ReferenceTenantId
                           && x.BusinessReferenceDataSetId == set.BusinessReferenceDataSetId)
                .ToListAsync());

            Assert.Equal(version.BusinessReferenceDataVersionId, set.PublishedVersionId);
            Assert.True(version.IsImmutable);
            Assert.Equal(BusinessReferenceDataVersionStatus.Published, version.Status);
            Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, operation.OperationState);
            Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, operation.PublishCheckpoint);
            Assert.Equal("market-test-fixture-v1", operation.CatalogVersion);
            Assert.Equal(first.CatalogFingerprint, operation.CatalogFingerprint);
            Assert.NotNull(await _harness.Repository.GetVerifiedPublicationAsync("market"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NonEligiblePublish_LeavesNoVerifiedPublication()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            var loader = _harness.CreateLoader(
                eligibility: new RuntimeBusinessReferenceDataPublicationEligibility());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                loader.LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test"));

            Assert.Null(await _harness.Repository.GetVerifiedPublicationAsync("market"));
            var operation = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
                .Find(x => x.TenantId == _harness.ReferenceTenantId)
                .ToListAsync());
            Assert.NotEqual(BusinessReferenceDataPublishOperationState.COMPLETED, operation.OperationState);
            Assert.NotEqual(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, operation.PublishCheckpoint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StalePointer_InvalidatesOtherwiseCompletedPublication()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            await _harness.CreateLoader().LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test");
            var set = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
                .Find(x => x.TenantId == _harness.ReferenceTenantId && x.SetCode == "market")
                .ToListAsync());
            var expectedRowVersion = set.RowVersion;
            set.PublishedVersionId = Guid.NewGuid();

            Assert.True(await _harness.Repository.UpdateSetAsync(set, expectedRowVersion));
            Assert.Null(await _harness.Repository.GetVerifiedPublicationAsync("market"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NewVersion_PreservesImmutableHistoryAndRetiredCodeCannotBeReactivated()
    {
        var firstPath = await VerifiedMarketTestFixture.CreateAsync();
        var retiredPath = await VerifiedMarketTestFixture.CreateAsync("retire-tr", "market-test-fixture-v2");
        var reactivatedPath = await VerifiedMarketTestFixture.CreateAsync(null, "market-test-fixture-v3");
        try
        {
            var loader = _harness.CreateLoader();
            await loader.LoadVerifiedMarketCatalogFromFileAsync(firstPath, "verified-market-test");
            var second = await loader.LoadVerifiedMarketCatalogFromFileAsync(retiredPath, "verified-market-test");

            Assert.Empty(second.BlockedConflicts);
            var versions = await _harness.Database
                .GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions")
                .Find(x => x.TenantId == _harness.ReferenceTenantId)
                .SortBy(x => x.VersionNumber)
                .ToListAsync();
            Assert.Equal(2, versions.Count);
            Assert.All(versions, version => Assert.True(version.IsImmutable));
            Assert.Equal(BusinessReferenceDataVersionStatus.Deprecated, versions[0].Status);
            Assert.Equal(BusinessReferenceDataVersionStatus.Published, versions[1].Status);
            Assert.True(Assert.Single(versions[1].Values, value => value.ValueCode == "TR").IsDeprecated);

            var reactivation = await loader.LoadVerifiedMarketCatalogFromFileAsync(
                reactivatedPath,
                "verified-market-test");
            Assert.Contains(reactivation.BlockedConflicts, conflict =>
                conflict.Contains("cannot be reactivated", StringComparison.Ordinal));
            Assert.Equal(2, await _harness.Database
                .GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions")
                .CountDocumentsAsync(x => x.TenantId == _harness.ReferenceTenantId));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(retiredPath);
            File.Delete(reactivatedPath);
        }
    }

    [Fact]
    public async Task PublishedCode_CannotBeReusedForAnotherMeaning()
    {
        var firstPath = await VerifiedMarketTestFixture.CreateAsync();
        var renamedPath = await VerifiedMarketTestFixture.CreateAsync("rename-tr", "market-test-fixture-v2");
        try
        {
            var loader = _harness.CreateLoader();
            await loader.LoadVerifiedMarketCatalogFromFileAsync(firstPath, "verified-market-test");

            var renamed = await loader.LoadVerifiedMarketCatalogFromFileAsync(renamedPath, "verified-market-test");

            Assert.Contains(renamed.BlockedConflicts, conflict =>
                conflict.Contains("cannot be reused for another meaning", StringComparison.Ordinal));
            Assert.Equal(1, await _harness.Database
                .GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions")
                .CountDocumentsAsync(x => x.TenantId == _harness.ReferenceTenantId));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(renamedPath);
        }
    }
}
