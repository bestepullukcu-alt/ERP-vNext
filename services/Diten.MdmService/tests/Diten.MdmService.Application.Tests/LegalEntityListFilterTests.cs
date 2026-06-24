using Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.LegalEntity.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityListFilterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static GetLegalEntitiesHandler HandlerWith(params LegalEntity[] entities)
        => new(new InMemoryLegalEntityRepository(Tenant, entities));

    private static LegalEntity Entity(string code, string country, string currency, LegalEntityOperationalStatus status, int? completeness)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            Code = code,
            LegalName = code,
            LegalFormCode = "CORPORATION",
            OrganizationRoleCode = "LEGALENTITY",
            CountryCode = country,
            BaseCurrencyCode = currency,
            RegisteredAddressJson = LegalEntityTestData.ValidAddressJson,
            OperationalStatus = status,
            CompletenessScore = completeness
        };

    [Fact]
    public async Task No_filters_returns_all()
    {
        var handler = HandlerWith(
            Entity("A", "TR", "TRY", LegalEntityOperationalStatus.Active, 100),
            Entity("B", "US", "USD", LegalEntityOperationalStatus.Draft, 50));

        var response = await handler.Handle(new GetLegalEntitiesQuery(), CancellationToken.None);

        Assert.Equal(2, response.Data!.Count);
    }

    [Fact]
    public async Task Filters_by_country_and_currency()
    {
        var handler = HandlerWith(
            Entity("A", "TR", "TRY", LegalEntityOperationalStatus.Active, 100),
            Entity("B", "US", "USD", LegalEntityOperationalStatus.Active, 100));

        var byCountry = await handler.Handle(new GetLegalEntitiesQuery { Country = "tr" }, CancellationToken.None);
        Assert.Single(byCountry.Data!);
        Assert.Equal("A", byCountry.Data![0].Code);

        var byCurrency = await handler.Handle(new GetLegalEntitiesQuery { BaseCurrency = "USD" }, CancellationToken.None);
        Assert.Single(byCurrency.Data!);
        Assert.Equal("B", byCurrency.Data![0].Code);
    }

    [Fact]
    public async Task Filters_by_operational_status()
    {
        var handler = HandlerWith(
            Entity("A", "TR", "TRY", LegalEntityOperationalStatus.Active, 100),
            Entity("B", "TR", "TRY", LegalEntityOperationalStatus.Draft, 100));

        var response = await handler.Handle(new GetLegalEntitiesQuery { OperationalStatus = "Active" }, CancellationToken.None);

        Assert.Single(response.Data!);
        Assert.Equal("A", response.Data![0].Code);
    }

    [Fact]
    public async Task IncompleteOnly_returns_below_100_or_null()
    {
        var handler = HandlerWith(
            Entity("A", "TR", "TRY", LegalEntityOperationalStatus.Active, 100),
            Entity("B", "TR", "TRY", LegalEntityOperationalStatus.Draft, 60),
            Entity("C", "TR", "TRY", LegalEntityOperationalStatus.Draft, null));

        var response = await handler.Handle(new GetLegalEntitiesQuery { IncompleteOnly = true }, CancellationToken.None);

        Assert.Equal(2, response.Data!.Count);
        Assert.DoesNotContain(response.Data!, x => x.Code == "A");
    }
}
