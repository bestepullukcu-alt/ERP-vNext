using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedUomEnumerationContractTests
{
    [Fact]
    public async Task ResolvedTenant_ReceivesExactLockedUomCatalog()
    {
        var context = new TenantContext();
        context.SetTenant(Guid.NewGuid());
        var handler = new EnumerateVerifiedGskuUomsHandler(context);

        var response = await handler.Handle(new EnumerateVerifiedGskuUomsQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(["C62", "GRM", "KGM", "MLT", "LTR"], response.Data!.Uoms.Select(x => x.Code));
        Assert.Equal(["One", "Gram", "Kilogram", "Millilitre", "Litre"],
            response.Data.Uoms.Select(x => x.DisplayText));
        Assert.Equal([10, 20, 30, 40, 50], response.Data.Uoms.Select(x => x.SortOrder));
        Assert.Equal([0, 3, 3, 3, 3], response.Data.Uoms.Select(x => x.MaximumDecimalPrecision));
    }

    [Fact]
    public async Task MissingTenantContext_IsForbidden()
    {
        var handler = new EnumerateVerifiedGskuUomsHandler(new TenantContext());

        var response = await handler.Handle(new EnumerateVerifiedGskuUomsQuery(), CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal("REFERENCE_FORBIDDEN", response.ReasonCode);
    }
}
