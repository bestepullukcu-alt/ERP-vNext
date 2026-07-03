using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.TenantSettingsModule.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantSettingsModule;

// FEAT-BASELINE-MODULES-S2 — the Tenant Settings manifest must mirror the real tenant self-service routes
// (/TenantSecurity, /TenantNavigation) + the verbatim platform.tenant-security.manage key the previous hardcoded
// _LayoutTenantShell block gated on, and must declare itself BASELINE under the "Settings" domain. Asserted both
// directions so a missing/extra page or a changed permission breaks the build.
public sealed class TenantSettingsManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new TenantSettingsManifestProvider().GetManifest();

    private static readonly Dictionary<string, string> ExpectedRoutePermissions = new(StringComparer.Ordinal)
    {
        ["/TenantSecurity"] = "platform.tenant-security.manage",
        ["/TenantNavigation"] = "platform.tenant-security.manage"
    };

    [Fact]
    public void Declares_a_clean_slug_baseline_module_identity_under_settings()
    {
        Assert.Equal("tenant-settings", Manifest.ModuleCode);
        Assert.Equal("Administration", Manifest.Domain); // FEAT-ADMIN-DOMAIN — grouped under Administration
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.True(Manifest.IsBaseline);              // FEAT-BASELINE-MODULES — entitlement-free
        Assert.Equal("bx-cog", Manifest.Icon);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Pages_mirror_the_tenant_self_service_routes_and_permissions_exactly_both_directions()
    {
        Assert.Equal(ExpectedRoutePermissions.Count, Manifest.Pages.Count);
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.True(manifestRoutes.SetEquals(ExpectedRoutePermissions.Keys),
            "Manifest pages must mirror the tenant self-service routes exactly (both directions).");

        foreach (var page in Manifest.Pages)
        {
            Assert.Equal(ExpectedRoutePermissions[page.RoutePath], page.RequiredPermission);
        }
    }

    [Fact]
    public void All_pages_are_co_equal_top_level_nav_entries_with_unique_codes()
    {
        Assert.All(Manifest.Pages, p => Assert.True(p.IsNavigationVisible));
        Assert.All(Manifest.Pages, p => Assert.Null(p.ParentPageCode));
        Assert.All(Manifest.Pages, p => Assert.Empty(p.Actions));

        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
