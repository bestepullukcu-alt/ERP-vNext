using Diten.Platform.Application.Features.Ppm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Ppm;

public sealed class PpmManifestProviderTests
{
    private static readonly string[] Resources =
        ["portfolios", "initiatives", "programs", "projects", "investment-cases", "benefit-commitments"];

    [Fact]
    public void Declares_entitlement_gated_PPM_with_six_pages_and_exact_24_permissions()
    {
        var manifest = new PpmManifestProvider().GetManifest();

        Assert.Equal("PPM", manifest.ModuleCode);
        Assert.Equal("Diten.PpmService", manifest.Service);
        Assert.True(manifest.IsTenantAssignable);
        Assert.False(manifest.IsBaseline);
        Assert.Equal(6, manifest.Pages.Count);

        var expected = Resources.SelectMany(resource => new[]
        {
            $"ppm.{resource}.read",
            $"ppm.{resource}.create",
            $"ppm.{resource}.update",
            $"ppm.{resource}.change-lifecycle"
        }).ToHashSet(StringComparer.Ordinal);
        var actual = manifest.Pages.Select(page => page.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(page => page.Actions).Select(action => action.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(24, actual.Count);
        Assert.True(actual.SetEquals(expected));
        Assert.All(manifest.Pages, page => Assert.Equal(3, page.Actions.Count));
    }

    [Fact]
    public void Routes_and_page_codes_are_unique_and_match_the_six_tenant_surfaces()
    {
        var pages = new PpmManifestProvider().GetManifest().Pages;
        Assert.Equal(pages.Count, pages.Select(page => page.PageCode).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(pages.Count, pages.Select(page => page.RoutePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            ["/ppm/portfolios", "/ppm/initiatives", "/ppm/programs", "/ppm/projects", "/ppm/investment-cases", "/ppm/benefit-commitments"],
            pages.Select(page => page.RoutePath));
    }
}
