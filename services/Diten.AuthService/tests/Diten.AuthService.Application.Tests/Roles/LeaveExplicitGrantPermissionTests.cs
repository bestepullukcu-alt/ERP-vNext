using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// MOD-0280-FU02 DL-0 (AC-58, AuthService half; owner decision 2026-10-31) — the three HR leave read keys read other people's
/// data across the tenant, so no default role receives them, on the production <see cref="DefaultRolePermissionTemplate"/>.
/// The keys are written out here on purpose: the test holds the exact names, not the constants that could drift with them.
/// The keys sit under a module the Admin breadth clause DOES reach, beside an ordinary read key that reaches both Admin and
/// Viewer, so the exclusion is not vacuous on either branch.
/// </summary>
public sealed class LeaveExplicitGrantPermissionTests
{
    private const string AdminReachableModule = "access-governance";

    private static Permission LeaveRead(string resource) => new(
        "leave",
        resource,
        "read",
        "Read " + resource,
        null,
        moduleOverride: AdminReachableModule,
        scope: PermissionScope.Tenant);

    private static List<string> KeysFor(string role, IEnumerable<Permission> catalog)
        => DefaultRolePermissionTemplate.SelectFor(role, catalog).Select(p => p.Key).ToList();

    [Theory]
    [InlineData("admin-requests", "leave.admin-requests.read")]
    [InlineData("entitlements", "leave.entitlements.read")]
    [InlineData("sync-runs", "leave.sync-runs.read")]
    public void Hr_read_keys_enter_no_default_role_even_under_the_Admin_module(string resource, string expectedKey)
    {
        var hrRead = LeaveRead(resource);
        var ordinary = LeaveRead("types");
        Assert.Equal(expectedKey, hrRead.Key);
        Assert.Equal("leave.types.read", ordinary.Key);
        var catalog = new List<Permission> { hrRead, ordinary };

        var superAdminKeys = KeysFor(DefaultRolePermissionTemplate.SuperAdminRole, catalog);
        var adminKeys = KeysFor(DefaultRolePermissionTemplate.AdminRole, catalog);
        var viewerKeys = KeysFor(DefaultRolePermissionTemplate.ViewerRole, catalog);

        Assert.DoesNotContain(expectedKey, superAdminKeys);
        Assert.DoesNotContain(expectedKey, adminKeys);
        Assert.DoesNotContain(expectedKey, viewerKeys);
        Assert.Contains("leave.types.read", adminKeys);  // ordinary key: Admin breadth clause
        Assert.Contains("leave.types.read", viewerKeys); // ordinary read: Viewer read clause
    }

    [Fact]
    public void An_ordinary_leave_read_key_still_reaches_Viewer()
    {
        var ordinary = LeaveRead("requests");
        Assert.Equal("leave.requests.read", ordinary.Key);

        var viewerKeys = KeysFor(DefaultRolePermissionTemplate.ViewerRole, [ordinary]);

        Assert.Contains("leave.requests.read", viewerKeys);
    }

    [Fact]
    public void The_three_keys_are_in_the_explicit_grant_set_by_their_exact_names()
    {
        Assert.Equal("leave.admin-requests.read", ExplicitGrantOnlyPermissions.LeaveAdminRequestsRead);
        Assert.Equal("leave.entitlements.read", ExplicitGrantOnlyPermissions.LeaveEntitlementsRead);
        Assert.Equal("leave.sync-runs.read", ExplicitGrantOnlyPermissions.LeaveSyncRunsRead);

        Assert.Contains("leave.admin-requests.read", ExplicitGrantOnlyPermissions.Keys);
        Assert.Contains("leave.entitlements.read", ExplicitGrantOnlyPermissions.Keys);
        Assert.Contains("leave.sync-runs.read", ExplicitGrantOnlyPermissions.Keys);
    }
}
