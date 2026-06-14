using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S1 / AG-INFRA-COMPLETION — the shared baseline template is the single source of truth for both
// the DataSeeder (default tenant) and RoleProvisioningService (every tenant). These tests pin the
// selection so the two cannot drift, and assert the platform-escalation boundary for tenant roles.
public sealed class DefaultRolePermissionTemplateTests
{
    private static List<Permission> Catalog() =>
    [
        new("auth", "users", "read", "Read User", null),
        new("auth", "users", "create", "Create User", null),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public void SuperAdmin_gets_the_full_catalog()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(5, keys.Count);
        Assert.Contains("platform.tenants.read", keys);
    }

    [Fact]
    public void Admin_gets_auth_and_mdm_only_never_platform()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(
            new[] { "auth.users.read", "auth.users.create", "mdm.legal-entities.read", "mdm.legal-entities.delete" }.OrderBy(k => k),
            keys.OrderBy(k => k));
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void Viewer_gets_read_actions_only_never_platform()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("Viewer", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(
            new[] { "auth.users.read", "mdm.legal-entities.read" }.OrderBy(k => k),
            keys.OrderBy(k => k));
        // platform.tenants.read is a read action but platform-scoped → still excluded for tenant roles.
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void Unknown_role_gets_nothing()
    {
        Assert.Empty(DefaultRolePermissionTemplate.SelectFor("Nope", Catalog()));
    }

    [Fact]
    public void Deleted_permissions_are_excluded()
    {
        var catalog = Catalog();
        catalog[0].MarkAsDeletedForTest(); // auth.users.read

        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("auth.users.read", keys);
        Assert.Contains("auth.users.create", keys);
    }
}

internal static class PermissionTestExtensions
{
    // Permission has no public delete mutator; IsDeleted is a settable base property.
    public static void MarkAsDeletedForTest(this Permission permission) => permission.IsDeleted = true;
}
