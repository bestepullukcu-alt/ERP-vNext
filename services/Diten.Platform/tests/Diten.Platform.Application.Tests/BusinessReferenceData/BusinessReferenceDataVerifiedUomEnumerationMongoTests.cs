using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedUomEnumerationMongoTests
{
    [Fact]
    public async Task UniversalEnumeration_DoesNotDependOnTenantAssignmentOrMongoState()
    {
        var context = new TenantContext();
        var handler = new EnumerateVerifiedGskuUomsHandler(context);

        context.SetTenant(Guid.NewGuid());
        var tenantA = await handler.Handle(new EnumerateVerifiedGskuUomsQuery(), CancellationToken.None);
        context.SetTenant(Guid.NewGuid());
        var tenantB = await handler.Handle(new EnumerateVerifiedGskuUomsQuery(), CancellationToken.None);

        Assert.True(tenantA.IsSuccessful);
        Assert.True(tenantB.IsSuccessful);
        Assert.Equal(tenantA.Data!.Uoms, tenantB.Data!.Uoms);
    }
}
