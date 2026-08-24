using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketResolveMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BusinessReferenceDataTestHarness.CreateAsync();

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Fact]
    public async Task ConsumerAAndB_SeeSameReferenceTenantCatalog_AndConsumerDecoyCannotChangeIt()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            await _harness.CreateLoader().LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test");
            var consumerA = Guid.NewGuid();
            var consumerB = Guid.NewGuid();
            using (TenantScope.Begin(_harness.TenantContext, consumerA))
            {
                await _harness.Repository.CreateSetAsync(new BusinessReferenceDataSet
                {
                    TenantId = consumerA,
                    SetCode = "market",
                    Name = "Consumer Decoy Market",
                    ScopeType = "tenant",
                    Status = BusinessReferenceDataSetStatus.Active
                });
            }

            var enumerate = new EnumerateVerifiedMarketsHandler(_harness.Repository, _harness.TenantContext);
            var resolve = new ResolveVerifiedMarketReferenceDataHandler(
                _harness.Repository,
                _harness.TenantContext,
                TimeProvider.System);

            _harness.TenantContext.SetTenant(consumerA);
            var marketsA = await enumerate.Handle(new EnumerateVerifiedMarketsQuery(), CancellationToken.None);
            var resolvedA = await resolve.Handle(new ResolveVerifiedMarketReferenceDataQuery("TR"), CancellationToken.None);
            Assert.Equal(consumerA, _harness.TenantContext.TenantId);

            _harness.TenantContext.SetTenant(consumerB);
            var marketsB = await enumerate.Handle(new EnumerateVerifiedMarketsQuery(), CancellationToken.None);
            var resolvedB = await resolve.Handle(new ResolveVerifiedMarketReferenceDataQuery("TR"), CancellationToken.None);
            Assert.Equal(consumerB, _harness.TenantContext.TenantId);

            Assert.True(marketsA.IsSuccessful);
            Assert.True(marketsB.IsSuccessful);
            Assert.Equal(marketsA.Data!.Markets, marketsB.Data!.Markets);
            Assert.Equal(["TR", "US"], marketsA.Data.Markets.Select(value => value.Code));
            Assert.True(resolvedA.IsSuccessful);
            Assert.True(resolvedB.IsSuccessful);
            Assert.Equal(resolvedA.Data!.Market.CatalogVersionId, resolvedB.Data!.Market.CatalogVersionId);
            Assert.Equal("TR", resolvedA.Data.Market.ValueCode);
            Assert.Equal("market", resolvedA.Data.Market.SetCode);

            var sets = _harness.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets");
            Assert.Equal(1, await sets.CountDocumentsAsync(x =>
                x.TenantId == _harness.ReferenceTenantId && x.SetCode == "market" && x.PublishedVersionId != null));
            Assert.Equal(1, await sets.CountDocumentsAsync(x =>
                x.TenantId == consumerA && x.SetCode == "market" && x.PublishedVersionId == null));
            Assert.Equal(0, await sets.CountDocumentsAsync(x => x.TenantId == consumerB && x.SetCode == "market"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
