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

        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // BL-458 / BL-452 package 3 — the page actions the catalog (and so the catalog→Auth permission sync) knows about.
    // Written out by hand, per page, both directions; the key-for-key parity with AuthService's seed is held on the
    // AuthService side (AccessGovernanceManifestSeedParityTests reads this provider's source).
    private static readonly Dictionary<string, Dictionary<string, string>> ExpectedActions = new(StringComparer.Ordinal)
    {
        ["USERS"] = new(StringComparer.Ordinal)
        {
            ["CREATE"] = "auth.users.create",
            ["EXPORT"] = "auth.users.export",
            ["UPDATE"] = "auth.users.update",
            ["ACCOUNT_KIND"] = "auth.users.account-kind.manage",
            ["DELETE"] = "auth.users.delete"
        },
        ["ROLES"] = new(StringComparer.Ordinal)
        {
            ["CREATE"] = "auth.roles.create",
            ["EXPORT"] = "auth.roles.export", // WP-ROLES-CLOSE-01 (BL-452): the file is a right of its own
            ["UPDATE"] = "auth.roles.update",
            ["DELETE"] = "auth.roles.delete"
        },
        ["PERMISSIONS"] = new(StringComparer.Ordinal),
        ["ROLE_PERMISSIONS"] = new(StringComparer.Ordinal) { ["ASSIGN_PERMISSION"] = "auth.roles.assign-permission" },
        ["USER_ROLES"] = new(StringComparer.Ordinal) { ["ASSIGN_ROLE"] = "auth.users.assign-role" }
    };

    [Fact]
    public void Page_actions_declare_the_auth_keys_exactly_both_directions()
    {
        foreach (var page in Manifest.Pages)
        {
            var expected = ExpectedActions[page.PageCode];
            var declared = page.Actions.ToDictionary(a => a.ActionCode, a => a.PermissionKey, StringComparer.Ordinal);

            Assert.Equal(page.Actions.Count, declared.Count); // unique action codes within the page
            Assert.True(expected.OrderBy(x => x.Key).SequenceEqual(declared.OrderBy(x => x.Key)),
                $"{page.PageCode}: declared [{string.Join(", ", declared.Select(x => x.Key + "=" + x.Value))}] "
                + $"expected [{string.Join(", ", expected.Select(x => x.Key + "=" + x.Value))}]");
        }

        // Deleting is dangerous; nothing else is.
        Assert.All(Manifest.Pages.SelectMany(p => p.Actions), a => Assert.Equal(a.ActionCode == "DELETE", a.IsDangerous));
    }
}
