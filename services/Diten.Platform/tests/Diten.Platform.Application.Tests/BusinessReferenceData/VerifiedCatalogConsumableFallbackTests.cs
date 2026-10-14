using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W4 · W4-FIX-d (G-2) — the consumable read's verified-provider fallback, on the handler itself (the HTTP Mongo tests
/// run it end to end; here the consumer service would serve ANY scope, so only the handler's own guard keeps a tenant-scoped
/// set of another tenant out): a VERIFIED catalog set (market, uom, pack-applicability) that neither the caller nor the
/// catalog reference tenant holds is read from the provider tenant — only when it is GLOBAL; any other set never is.
/// </summary>
public sealed class VerifiedCatalogConsumableFallbackTests
{
    private static readonly Guid Caller = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid Provider = Guid.Parse("0b7d0048-0290-4000-8000-000000000001");

    [Theory]
    [InlineData("market")]
    [InlineData("uom")]
    [InlineData("pack-applicability")]
    public async Task A_global_verified_catalog_of_the_provider_tenant_is_served(string setCode)
    {
        var (handler, reads) = Arrange((Provider, setCode, "global"));
        var response = await handler.Handle(Query(setCode), default);
        Assert.True(response.IsSuccessful);
        Assert.Equal([(Provider, (string?)null)], reads);           // read in the provider tenant, keyless (global)
    }

    [Fact]
    public async Task A_tenant_scoped_set_of_the_provider_tenant_is_never_served_even_when_the_consumer_would()
    {
        var (handler, reads) = Arrange((Provider, "market", "tenant"));
        var response = await handler.Handle(Query("market"), default);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains(GetConsumableBusinessReferenceDataPublishedValuesQueryHandler.NotPublished, response.Errors!);
        Assert.Empty(reads);                                          // nothing was read in another tenant
    }

    [Fact]
    public async Task A_non_verified_global_set_is_never_read_from_the_provider_tenant()
    {
        var (handler, reads) = Arrange((Provider, "country", "global"));
        var response = await handler.Handle(Query("country"), default);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(reads);
    }

    [Fact]
    public async Task Without_a_provider_tenant_the_old_answer_stands()
    {
        var (handler, reads) = Arrange((Provider, "market", "global"));
        var response = await handler.Handle(new GetConsumableBusinessReferenceDataPublishedValuesQuery("market", Caller, Caller, null), default);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(reads);
        Assert.Contains("market", GetConsumableBusinessReferenceDataPublishedValuesQueryHandler.VerifiedCatalogSets);
    }

    // The local shape: CatalogLoad:TenantId is the caller (97c5), the verified catalogs live in the reserved provider tenant.
    private static GetConsumableBusinessReferenceDataPublishedValuesQuery Query(string setCode)
        => new(setCode, Caller, Caller, Provider);

    private static (GetConsumableBusinessReferenceDataPublishedValuesQueryHandler Handler, List<(Guid Tenant, string? ScopeKey)> Reads) Arrange(
        params (Guid Tenant, string SetCode, string Scope)[] sets)
    {
        var tenant = new TenantContext();
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(r => r.GetSetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => sets
                .Where(s => s.Tenant == tenant.TenantId && s.SetCode == code)
                .Select(s => new BusinessReferenceDataSet { TenantId = s.Tenant, SetCode = s.SetCode, Name = s.SetCode, ScopeType = s.Scope })
                .FirstOrDefault());
        var reads = new List<(Guid, string?)>();
        var consumer = new Mock<IBusinessReferenceDataConsumerQueryService>();
        consumer.Setup(c => c.GetPublishedValuesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, string? scopeKey, CancellationToken _) =>
            {
                reads.Add((tenant.TenantId, scopeKey));   // serves ANY scope — the handler's guard is what is tested
                return new BusinessReferenceDataPublishedValuesModel(code, 1, DateTimeOffset.UtcNow, Array.Empty<BusinessReferenceDataPublishedValueItemModel>());
            });
        var handler = new GetConsumableBusinessReferenceDataPublishedValuesQueryHandler(repository.Object, consumer.Object, tenant,
            Options.Create(new BusinessReferenceDataConsumableSetsOptions { ConsumableSets = ["market", "uom", "pack-applicability", "country"] }));
        return (handler, reads);
    }
}
