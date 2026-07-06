using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.AccessGovernance.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

// FEAT-BASELINE-MODULES-S1 — the Access Governance manifest must mirror the real tenant-shell Access Governance
// routes + the verbatim auth.* permission keys (the same keys the previous hardcoded _LayoutTenantShell block
// gated on), and must declare itself as a BASELINE module. Asserted in both directions so a missing/extra page
// or a changed permission breaks the build. (Routes/permissions are cross-service — enforced by AuthService +
// Diten.Web — so the expected sets are declared here rather than reflected off controllers.)
public sealed class AccessGovernanceManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new AccessGovernanceManifestProvider().GetManifest();

    // The real tenant-shell routes the block linked to, with the exact auth.* permission each one gated on.
    private static readonly Dictionary<string, string> ExpectedRoutePermissions = new(StringComparer.Ordinal)
    {
        ["/Users"] = "auth.users.read",
        ["/Roles"] = "auth.roles.read",
        ["/Permissions"] = "auth.roles.read",
        ["/RoleAssignments"] = "auth.roles.assign-permission",
        ["/UserRoleAssignments"] = "auth.users.assign-role"
    };

    [Fact]
    public void Declares_a_clean_slug_baseline_module_identity()
    {
        Assert.Equal("access-governance", Manifest.ModuleCode);
        Assert.Equal("Administration", Manifest.Domain); // FEAT-ADMIN-DOMAIN — grouped under Administration
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.True(Manifest.IsBaseline);              // FEAT-BASELINE-MODULES — entitlement-free
        Assert.Equal("bx-shield-quarter", Manifest.Icon);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Pages_mirror_the_tenant_shell_routes_and_permissions_exactly_both_directions()
    {
        Assert.Equal(ExpectedRoutePermissions.Count, Manifest.Pages.Count);
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.True(manifestRoutes.SetEquals(ExpectedRoutePermissions.Keys),
            "Manifest pages must mirror the tenant-shell routes exactly (both directions).");

        foreach (var page in Manifest.Pages)
        {
            Assert.Equal(ExpectedRoutePermissions[page.RoutePath], page.RequiredPermission);
        }
    }

    [Fact]
    public void All_pages_are_co_equal_top_level_nav_entries_with_unique_codes()
    {
        // PERMISSIONS is a read-only catalog view, deliberately hidden from the tenant nav (IsNavigationVisible=false);
        // the other four (USERS/ROLES/ROLE_PERMISSIONS/USER_ROLES) are visible top-level entries.
        Assert.All(Manifest.Pages, p => Assert.Equal(
            !string.Equals(p.PageCode, "PERMISSIONS", StringComparison.OrdinalIgnoreCase), p.IsNavigationVisible));
        Assert.All(Manifest.Pages, p => Assert.Null(p.ParentPageCode));
        Assert.All(Manifest.Pages, p => Assert.Empty(p.Actions)); // read-only nav entries, no toolbar/row actions

        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
