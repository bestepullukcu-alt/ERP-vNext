using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketEnumerationContractTests
{
    [Fact]
    public async Task Enumeration_ReturnsOnlyThreeFieldsInDeterministicOrderAndExcludesRetired()
    {
        var values = new List<BusinessReferenceDataValue>
        {
            Active("US", "United States", 20),
            Active("TR", "Turkiye", 10),
            new()
            {
                ValueCode = "DE",
                DisplayName = "Germany",
                SortOrder = 5,
                IsDeprecated = true
            }
        };
        var (handler, tenant, consumerTenantId) = Handler(values);

        var result = await handler.Handle(new EnumerateVerifiedMarketsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(["TR", "US"], result.Data!.Markets.Select(value => value.Code));
        Assert.Equal(consumerTenantId, tenant.TenantId);
        var json = JsonSerializer.Serialize(result.Data.Markets[0]);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["code", "display_text", "sort_order"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task EmptyDuplicateMalformedOrOverBoundActiveCatalogFailsAsWholeWith503()
    {
        var invalidCatalogs = new IReadOnlyList<BusinessReferenceDataValue>[]
        {
            [],
            [Active("TR", "Turkiye", 10), Active("TR", "Duplicate", 20)],
            [Active("tr", "Lowercase", 10), Active("US", "United States", 20)],
            Enumerable.Range(0, VerifiedMarketCatalogContract.MaximumActiveMarketCount + 1)
                .Select(index => Active(
                    $"{(char)('A' + index / 26)}{(char)('A' + index % 26)}",
                    $"Market {index}",
                    index))
                .ToList()
        };

        foreach (var values in invalidCatalogs)
        {
            var (handler, tenant, consumerTenantId) = Handler(values);
            var result = await handler.Handle(new EnumerateVerifiedMarketsQuery(), CancellationToken.None);

            Assert.False(result.IsSuccessful);
            Assert.Equal(503, result.StatusCode);
            Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.ReasonCode);
            Assert.Empty(result.Data?.Markets ?? []);
            Assert.Equal(consumerTenantId, tenant.TenantId);
        }
    }

    [Fact]
    public async Task CancellationInsideReferenceScopeRestoresConsumerTenant()
    {
        var consumerTenantId = Guid.NewGuid();
        var referenceTenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                VerifiedMarketCatalogContract.SetCode,
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(referenceTenantId, tenant.TenantId))
            .Returns((string _, CancellationToken token) => Task.FromCanceled<BusinessReferenceDataVerifiedPublication?>(
                token.IsCancellationRequested ? token : new CancellationToken(canceled: true)));
        var handler = new EnumerateVerifiedMarketsHandler(repository.Object, tenant);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.Handle(new EnumerateVerifiedMarketsQuery(), cancellation.Token));

        Assert.Equal(consumerTenantId, tenant.TenantId);
    }

    private static (EnumerateVerifiedMarketsHandler Handler, TenantContext Tenant, Guid ConsumerTenantId) Handler(
        IReadOnlyList<BusinessReferenceDataValue> values)
    {
        var consumerTenantId = Guid.NewGuid();
        var referenceTenantId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                VerifiedMarketCatalogContract.SetCode,
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(referenceTenantId, tenant.TenantId))
            .ReturnsAsync(BusinessReferenceDataVerifiedMarketResolveContractTests.Publication(
                referenceTenantId,
                versionId,
                values));
        return (new EnumerateVerifiedMarketsHandler(repository.Object, tenant), tenant, consumerTenantId);
    }

    private static BusinessReferenceDataValue Active(string code, string display, int sortOrder) =>
        BusinessReferenceDataVerifiedMarketResolveContractTests.Active(code, display, sortOrder);
}
