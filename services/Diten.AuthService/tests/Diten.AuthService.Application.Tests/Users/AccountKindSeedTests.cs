using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the two keys are in the canonical seed catalog with the nature each was decided to
/// have: <c>auth.users.lookup</c> an ordinary tenant key, <c>auth.users.account-kind.manage</c> explicit-grant-only.
/// Read from the REAL seed list (DataSeeder.BuildCanonicalPermissions), not a copy.
/// </summary>
public sealed class AccountKindSeedTests
{
    [Fact]
    public void Lookup_key_is_seeded_as_an_ordinary_tenant_key_under_access_governance()
    {
        var permission = Assert.Single(DataSeeder.BuildCanonicalPermissions(), p => p.Key == "auth.users.lookup");

        Assert.Equal("access-governance", permission.Module);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.Equal("users", permission.Resource);
        Assert.Equal("lookup", permission.Action);
        Assert.DoesNotContain(permission.Key, ExplicitGrantOnlyPermissions.Keys);
    }

    [Fact]
    public void Account_kind_manage_key_is_seeded_tenant_scoped_and_is_explicit_grant_only()
    {
        var permission = Assert.Single(DataSeeder.BuildCanonicalPermissions(), p => p.Key == "auth.users.account-kind.manage");

        Assert.Equal("access-governance", permission.Module);
        Assert.Equal(PermissionScope.Tenant, permission.Scope); // assignable to a tenant role by a person — never automatically
        Assert.Equal("users.account-kind", permission.Resource);
        Assert.Equal("manage", permission.Action);
        Assert.Contains(permission.Key, ExplicitGrantOnlyPermissions.Keys);
        Assert.Equal(ExplicitGrantOnlyPermissions.UsersAccountKindManage, permission.Key);
    }

    [Fact]
    public void Manage_key_is_tenant_assignable_by_hand_but_reaches_no_default_role()
    {
        var catalog = DataSeeder.BuildCanonicalPermissions();
        var manage = catalog.Single(p => p.Key == "auth.users.account-kind.manage");

        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(manage)); // the manual path stays open …
        foreach (var role in new[] { "SuperAdmin", "Admin", "Viewer" })
        {
            Assert.DoesNotContain("auth.users.account-kind.manage", DefaultRolePermissionTemplate.SelectFor(role, catalog).Select(p => p.Key));
        }

        // … while lookup, an ordinary key, follows its siblings into the Admin baseline (not Viewer: it is not a read).
        Assert.Contains("auth.users.lookup", DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key));
        Assert.DoesNotContain("auth.users.lookup", DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key));
    }

    [Fact]
    public void Both_keys_are_literally_in_the_seeder_source()
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains("\"auth\", \"users\", \"lookup\"", seederSource);
        Assert.Contains("\"auth\", \"users.account-kind\", \"manage\"", seederSource);
    }

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
