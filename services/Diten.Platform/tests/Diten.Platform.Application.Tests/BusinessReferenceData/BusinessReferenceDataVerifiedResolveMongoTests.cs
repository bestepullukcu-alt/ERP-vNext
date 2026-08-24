using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedResolveMongoTests
{
    [Fact]
    public async Task UniversalCatalog_IsIdenticalForDifferentTenantsWithoutAssignmentOrPersistence()
    {
        var context = new TenantContext();
        var handler = new ResolveVerifiedGskuReferenceDataHandler(context, TimeProvider.System);
        var query = new ResolveVerifiedGskuReferenceDataQuery(
        [
            new("pack-applicability", "SCALAR_QUANTITY_APPLIES", "LATEST"),
            new("uom", "LTR", "LATEST")
        ]);

        context.SetTenant(Guid.NewGuid());
        var first = await handler.Handle(query, CancellationToken.None);
        context.SetTenant(Guid.NewGuid());
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(
            first.Data!.Selections.Select(x => (x.SetCode, x.ValueCode, x.CatalogVersionId, x.CatalogVersionNumber)),
            second.Data!.Selections.Select(x => (x.SetCode, x.ValueCode, x.CatalogVersionId, x.CatalogVersionNumber)));
    }
}
