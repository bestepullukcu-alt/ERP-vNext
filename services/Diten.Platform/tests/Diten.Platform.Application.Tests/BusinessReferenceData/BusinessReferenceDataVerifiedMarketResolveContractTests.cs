using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketResolveContractTests
{
    [Theory]
    [InlineData("tr")]
    [InlineData(" TR")]
    [InlineData("TUR")]
    public async Task Resolve_RejectsAnythingOtherThanExactIsoAlpha2WithoutNormalization(string value)
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var handler = new ResolveVerifiedMarketReferenceDataHandler(repository.Object, tenant, TimeProvider.System);

        var result = await handler.Handle(new ResolveVerifiedMarketReferenceDataQuery(value), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("REFERENCE_MARKET_NOT_FOUND", result.ReasonCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Resolve_CanonicalButAbsentCodeReturns404WithoutAliasFallback()
    {
        var consumerTenantId = Guid.NewGuid();
        var referenceTenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(x => x.GetVerifiedPublicationAsync("market", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Publication(referenceTenantId, Guid.NewGuid(), [Active("TR", "Turkiye", 10)]));

        var result = await new ResolveVerifiedMarketReferenceDataHandler(repository.Object, tenant, TimeProvider.System)
            .Handle(new ResolveVerifiedMarketReferenceDataQuery("EU"), CancellationToken.None);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("REFERENCE_MARKET_NOT_FOUND", result.ReasonCode);
        Assert.Equal(consumerTenantId, tenant.TenantId);
    }

    [Fact]
    public async Task Resolve_WhenDurableVerifiedPublicationIsAbsent_ReturnsProviderUnavailable()
    {
        var tenant = new TenantContext();
        var consumerTenantId = Guid.NewGuid();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        var referenceTenantId = Guid.NewGuid();
        repository.Setup(x => x.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(x => x.GetVerifiedPublicationAsync(
                VerifiedMarketCatalogContract.SetCode,
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(referenceTenantId, tenant.TenantId))
            .ReturnsAsync((BusinessReferenceDataVerifiedPublication?)null);
        var result = await new ResolveVerifiedMarketReferenceDataHandler(repository.Object, tenant, TimeProvider.System)
            .Handle(new ResolveVerifiedMarketReferenceDataQuery("TR"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.ReasonCode);
        Assert.Equal(consumerTenantId, tenant.TenantId);
    }

    [Fact]
    public async Task Resolve_UsesReferenceTenantAndReturnsExactSixProviderFields()
    {
        var consumerTenantId = Guid.NewGuid();
        var referenceTenantId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(x => x.GetVerifiedPublicationAsync(
                VerifiedMarketCatalogContract.SetCode,
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(referenceTenantId, tenant.TenantId))
            .ReturnsAsync(Publication(referenceTenantId, versionId, [Active("TR", "Turkiye", 10)]));

        var result = await new ResolveVerifiedMarketReferenceDataHandler(repository.Object, tenant, TimeProvider.System)
            .Handle(new ResolveVerifiedMarketReferenceDataQuery("TR"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var market = result.Data!.Market;
        Assert.Equal("market", market.SetCode);
        Assert.Equal("TR", market.ValueCode);
        Assert.Equal(versionId, market.CatalogVersionId);
        Assert.Equal(7, market.CatalogVersionNumber);
        Assert.Equal("LATEST", market.ResolutionMode);
        Assert.NotEqual(default, market.ResolvedAtUtc);
        Assert.Equal(consumerTenantId, tenant.TenantId);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(market));
        Assert.Equal(
            ["set_code", "value_code", "catalog_version_id", "catalog_version_number", "resolution_mode", "resolved_at_utc"],
            json.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Resolve_InvalidProviderConfigurationReturns503AndPreservesConsumerScope()
    {
        var consumerTenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetRequiredReferenceTenantId())
            .Throws(new InvalidOperationException("REFERENCE_PROVIDER_CONFIGURATION_INVALID"));

        var result = await new ResolveVerifiedMarketReferenceDataHandler(repository.Object, tenant, TimeProvider.System)
            .Handle(new ResolveVerifiedMarketReferenceDataQuery("TR"), CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.ReasonCode);
        Assert.Equal(consumerTenantId, tenant.TenantId);
    }

    [Fact]
    public async Task Pipeline_MapsUnexpectedProviderFailureToExact503()
    {
        var behavior = new BusinessReferenceDataExceptionBehavior<
            ResolveVerifiedMarketReferenceDataQuery,
            Response<BusinessReferenceDataVerifiedMarketResolveResult>>(
                NullLogger<BusinessReferenceDataExceptionBehavior<
                    ResolveVerifiedMarketReferenceDataQuery,
                    Response<BusinessReferenceDataVerifiedMarketResolveResult>>>.Instance);

        var result = await behavior.Handle(
            new ResolveVerifiedMarketReferenceDataQuery("TR"),
            () => Task.FromException<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(
                new IOException("provider read failed")),
            CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.ReasonCode);
    }

    [Fact]
    public async Task Pipeline_MapsProviderBudgetCancellationToExact504()
    {
        var behavior = new BusinessReferenceDataExceptionBehavior<
            ResolveVerifiedMarketReferenceDataQuery,
            Response<BusinessReferenceDataVerifiedMarketResolveResult>>(
                NullLogger<BusinessReferenceDataExceptionBehavior<
                    ResolveVerifiedMarketReferenceDataQuery,
                    Response<BusinessReferenceDataVerifiedMarketResolveResult>>>.Instance);

        var result = await behavior.Handle(
            new ResolveVerifiedMarketReferenceDataQuery("TR"),
            () => Task.FromException<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(
                new OperationCanceledException("provider budget")),
            CancellationToken.None);

        Assert.Equal(504, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_TIMEOUT", result.ReasonCode);
    }

    internal static BusinessReferenceDataVerifiedPublication Publication(
        Guid tenantId,
        Guid versionId,
        IReadOnlyList<BusinessReferenceDataValue> values)
    {
        var setId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        return new(
            new BusinessReferenceDataSet
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                SetCode = "market",
                Name = "Market",
                ScopeType = "global",
                Status = BusinessReferenceDataSetStatus.Active,
                PublishedVersionId = versionId
            },
            new BusinessReferenceDataVersion
            {
                TenantId = tenantId,
                BusinessReferenceDataVersionId = versionId,
                BusinessReferenceDataSetId = setId,
                VersionNumber = 7,
                Status = BusinessReferenceDataVersionStatus.Published,
                IsImmutable = true,
                LastPublishIdempotencyKey = "market-v7",
                Values = values.ToList()
            },
            new BusinessReferenceDataPublishOperation
            {
                TenantId = tenantId,
                PublishOperationId = operationId,
                BusinessReferenceDataSetId = setId,
                BusinessReferenceDataVersionId = versionId,
                IdempotencyKey = "market-v7",
                CatalogVersion = "market-test-v7",
                CatalogFingerprint = new string('a', 64),
                ExpectedSetVersion = 1,
                ExpectedTargetVersionToken = "market-v7-token",
                OperationState = BusinessReferenceDataPublishOperationState.COMPLETED,
                PublishCheckpoint = BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
                CompletedAt = DateTimeOffset.UtcNow
            });
    }

    internal static BusinessReferenceDataValue Active(string code, string display, int sortOrder) => new()
    {
        ValueCode = code,
        DisplayName = display,
        SortOrder = sortOrder,
        IsDeprecated = false
    };
}
