using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class UsageFormOptionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_UsesEffectiveTenantEntitlementsForConsumerModules()
    {
        var tenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
        var setId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(x => x.Send(
                It.Is<GetTenantModuleEntitlementsQuery>(q => q.TenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(
            [
                new TenantModuleEntitlementRowDto(
                    tenantId,
                    "BUSINESSREFERENCEDATA",
                    "Business Reference Data",
                    "Plan",
                    null,
                    true,
                    null,
                    "Active",
                    null,
                    true,
                    false,
                    null,
                    null),
                new TenantModuleEntitlementRowDto(
                    tenantId,
                    "CRM",
                    "CRM",
                    "Plan",
                    null,
                    true,
                    null,
                    "Active",
                    null,
                    true,
                    false,
                    null,
                    null),
                new TenantModuleEntitlementRowDto(
                    tenantId,
                    "DISABLED",
                    "Disabled",
                    "ManualOverride",
                    Guid.NewGuid(),
                    false,
                    null,
                    "BlockedByOverride",
                    null,
                    false,
                    true,
                    null,
                    null)
            ]));

        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository
            .Setup(x => x.GetSetByCodeAsync("COUNTRY_CODES", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataSet
            {
                TenantId = tenantId,
                BusinessReferenceDataSetId = setId,
                SetCode = "COUNTRY_CODES",
                Name = "Country Codes",
                ScopeType = "Tenant"
            });
        repository
            .Setup(x => x.GetVersionsBySetIdAsync(setId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new BusinessReferenceDataVersion
                {
                    TenantId = tenantId,
                    BusinessReferenceDataSetId = setId,
                    ScopeKey = "TR",
                    VersionNumber = 1
                }
            ]);

        var handler = new GetBusinessReferenceDataUsageFormOptionsQueryHandler(
            mediator.Object,
            tenantContext,
            repository.Object);

        var response = await handler.Handle(new GetBusinessReferenceDataUsageFormOptionsQuery("COUNTRY_CODES"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(["BUSINESSREFERENCEDATA", "CRM"], response.Data!.ConsumerModules.Select(x => x.Value));
        Assert.Equal(["TR"], response.Data.ScopeKeys);
        Assert.Equal("tenant", response.Data.SetScopeType, ignoreCase: true);
        Assert.True(response.Data.ScopeKeysByScopeType!.TryGetValue("tenant", out var tenantScopeKeys));
        Assert.Equal(["TR"], tenantScopeKeys);
        mediator.VerifyAll();
        repository.VerifyAll();
    }
}
