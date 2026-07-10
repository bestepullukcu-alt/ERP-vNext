using Diten.Platform.API.Controllers;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Persistence.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

// MOD-0220 cross-tenant read bug — BRD sets are per-tenant; the seed loads them under the REFERENCE tenant
// (BusinessReferenceData:CatalogLoad:TenantId), but the consumer read scoped to the AMBIENT (caller's) tenant, so
// the LE wizard came up EMPTY for every tenant except the reference one. This is the test that would have caught
// it: with caller tenant ≠ reference tenant, the read MUST execute in the reference tenant's context (sourced from
// the same config the seed uses). The prior guards only checked auth-scoping + seed⇔allow-list — never the tenant
// the query actually runs under.
public sealed class TenantReferenceDataCrossTenantReadTests
{
    private static readonly Guid CallerTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReferenceTenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Fact]
    public void Reads_against_the_config_reference_tenant_not_the_caller_tenant()
    {
        // The seed and the read read the SAME options type/key → the config key is the single source of truth.
        Assert.Equal("BusinessReferenceData:CatalogLoad", BusinessReferenceDataCatalogLoadOptions.SectionName);

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(CallerTenant); // simulate the request middleware resolving the caller's tenant

        var capturedTenantAtSend = Guid.Empty;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetBusinessReferenceDataPublishedValuesQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetBusinessReferenceDataPublishedValuesQuery q, CancellationToken _) =>
            {
                // Capture the tenant the repository would resolve against AT THE MOMENT the query runs.
                capturedTenantAtSend = tenantContext.TenantId;
                var model = new BusinessReferenceDataPublishedValuesModel(q.SetCode, 1, null,
                    new List<BusinessReferenceDataPublishedValueItemModel>
                    {
                        new("CORPORATION", "Corporation", null, true, 10, null)
                    });
                return Task.FromResult(Response<BusinessReferenceDataPublishedValuesModel>.Success(model));
            });

        var controller = new TenantReferenceDataController(mediator.Object, tenantContext, Options(ReferenceTenant.ToString()));

        var result = controller.GetPublishedValues("legal-form", CancellationToken.None).GetAwaiter().GetResult();

        // The read executed under the REFERENCE tenant (from config), NOT the caller's tenant → non-empty result.
        Assert.Equal(ReferenceTenant, capturedTenantAtSend);
        Assert.NotEqual(CallerTenant, capturedTenantAtSend);
        // Ambient tenant is restored to the caller after the scoped read.
        Assert.Equal(CallerTenant, tenantContext.TenantId);
        Assert.IsType<OkObjectResult>(result);
        mediator.Verify(m => m.Send(It.IsAny<GetBusinessReferenceDataPublishedValuesQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Non_allowlisted_set_is_rejected_before_any_read()
    {
        var mediator = new Mock<IMediator>();
        var controller = new TenantReferenceDataController(mediator.Object, new TenantContext(), Options(ReferenceTenant.ToString()));

        var result = controller.GetPublishedValues("qms-document-class", CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(404, (result as ObjectResult)?.StatusCode);
        mediator.Verify(m => m.Send(It.IsAny<GetBusinessReferenceDataPublishedValuesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Misconfigured_reference_tenant_fails_without_reading()
    {
        var mediator = new Mock<IMediator>();
        var controller = new TenantReferenceDataController(mediator.Object, new TenantContext(), Options("not-a-guid"));

        var result = controller.GetPublishedValues("legal-form", CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(500, (result as ObjectResult)?.StatusCode);
        mediator.Verify(m => m.Send(It.IsAny<GetBusinessReferenceDataPublishedValuesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IOptions<BusinessReferenceDataCatalogLoadOptions> Options(string tenantId) =>
        Microsoft.Extensions.Options.Options.Create(new BusinessReferenceDataCatalogLoadOptions { TenantId = tenantId });
}
