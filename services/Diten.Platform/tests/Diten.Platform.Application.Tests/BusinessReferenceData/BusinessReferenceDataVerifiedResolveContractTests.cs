using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedResolveContractTests
{
    [Theory]
    [InlineData("uom", "EA", "LATEST")]
    [InlineData("uom", "KGM", "PINNED")]
    [InlineData("other", "KGM", "LATEST")]
    [InlineData("pack-applicability", "OTHER", "LATEST")]
    public async Task UnsupportedSelection_ReturnsStableConflict(
        string setCode,
        string valueCode,
        string mode)
    {
        var context = ResolvedTenant();
        var handler = new ResolveVerifiedGskuReferenceDataHandler(context, TimeProvider.System);

        var response = await handler.Handle(
            new ResolveVerifiedGskuReferenceDataQuery([new(setCode, valueCode, mode)]),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal("REFERENCE_RESOLUTION_CONTRACT_INVALID", response.ReasonCode);
    }

    [Fact]
    public async Task LockedSelections_ReturnDeterministicVersionedEvidence()
    {
        var handler = new ResolveVerifiedGskuReferenceDataHandler(ResolvedTenant(), TimeProvider.System);

        var response = await handler.Handle(
            new ResolveVerifiedGskuReferenceDataQuery(
            [
                new("pack-applicability", "SCALAR_QUANTITY_APPLIES", "LATEST"),
                new("uom", "KGM", "LATEST")
            ]),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data!.Selections.Count);
        Assert.Contains(response.Data.Selections, x =>
            x.SetCode == "pack-applicability"
            && x.CatalogVersionId == VerifiedGskuUniversalCatalog.PackApplicabilityCatalogVersionId);
        Assert.Contains(response.Data.Selections, x =>
            x.SetCode == "uom"
            && x.CatalogVersionId == VerifiedGskuUniversalCatalog.UomCatalogVersionId);
        Assert.All(response.Data.Selections, x =>
        {
            Assert.Equal(VerifiedGskuUniversalCatalog.CatalogVersionNumber, x.CatalogVersionNumber);
            Assert.Equal("LATEST", x.ResolutionMode);
            Assert.False(x.IsRetired);
            Assert.True(x.SelectableForNew);
        });
    }

    [Fact]
    public async Task MissingTenantContext_IsForbidden()
    {
        var handler = new ResolveVerifiedGskuReferenceDataHandler(new TenantContext(), TimeProvider.System);

        var response = await handler.Handle(
            new ResolveVerifiedGskuReferenceDataQuery([new("uom", "KGM", "LATEST")]),
            CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal("REFERENCE_FORBIDDEN", response.ReasonCode);
    }

    private static TenantContext ResolvedTenant()
    {
        var context = new TenantContext();
        context.SetTenant(Guid.NewGuid());
        return context;
    }
}
