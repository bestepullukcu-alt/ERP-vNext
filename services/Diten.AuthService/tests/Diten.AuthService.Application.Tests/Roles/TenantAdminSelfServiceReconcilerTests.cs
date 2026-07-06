using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// FEAT-ROLE-GRANT-RECONCILE — the startup backfill that gives EXISTING tenant Admin roles the self-service
// permissions added to the template after those tenants were provisioned. These pin the pure planner: additive
// only, Admin-only, self-service-subset-only, idempotent, per-tenant.
public sealed class TenantAdminSelfServiceReconcilerTests
{
    private const string Admin = DefaultRolePermissionTemplate.AdminRole;

    // A catalog with the three curated self-service keys plus an unrelated Admin-template permission that must
    // never be backfilled by this reconcile (it belongs to the full template, not the self-service subset).
    private static List<Permission> Catalog() =>
    [
        new("platform", "tenant-security", "read", "Read Tenant Security", null, moduleOverride: "tenant-settings"),
        new("platform", "tenant-security", "manage", "Manage Tenant Security", null, moduleOverride: "tenant-settings"),
        new("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", null, moduleOverride: "tenant-settings"),
        new("auth", "users", "read", "Read User", null) // NOT self-service → never planned
    ];

    private static ISet<(Guid, Guid)> NoGrants() => new HashSet<(Guid, Guid)>();

    [Fact]
    public void Admin_missing_all_self_service_keys_gets_them_all()
    {
        var catalog = Catalog();
        var tenant = Guid.NewGuid();
        var role = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, tenant);

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, NoGrants());

        var plannedPermIds = planned.Select(g => g.PermissionId).ToHashSet();
        var selfServiceIds = catalog
            .Where(p => DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(p.Key))
            .Select(p => p.Id)
            .ToHashSet();

        Assert.Equal(selfServiceIds, plannedPermIds);
        Assert.All(planned, g => Assert.Equal(role.RoleId, g.RoleId));
        Assert.All(planned, g => Assert.Equal(tenant, g.TenantId));
    }

    [Fact]
    public void Non_self_service_catalog_permissions_are_never_planned()
    {
        var catalog = Catalog();
        var authUsersRead = catalog.Single(p => p.Key == "auth.users.read");
        var role = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, Guid.NewGuid());

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, NoGrants());

        Assert.DoesNotContain(planned, g => g.PermissionId == authUsersRead.Id);
    }

    [Fact]
    public void Already_granted_self_service_key_is_a_no_op()
    {
        var catalog = Catalog();
        var manageNav = catalog.Single(p => p.Key == "platform.tenant-navigation.manage");
        var roleId = Guid.NewGuid();
        var role = new TenantAdminSelfServiceReconciler.RoleRef(roleId, Admin, Guid.NewGuid());
        var existing = new HashSet<(Guid, Guid)> { (roleId, manageNav.Id) };

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, existing);

        Assert.DoesNotContain(planned, g => g.PermissionId == manageNav.Id);
        // The other two self-service keys are still missing and planned.
        Assert.Equal(2, planned.Count);
    }

    [Fact]
    public void SuperAdmin_and_Viewer_and_custom_roles_are_untouched()
    {
        var catalog = Catalog();
        var tenant = Guid.NewGuid();
        var roles = new[]
        {
            new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), "SuperAdmin", tenant),
            new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), "Viewer", tenant),
            new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), "TenantCustomRole", tenant)
        };

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(roles, catalog, NoGrants());

        Assert.Empty(planned);
    }

    [Fact]
    public void Only_admin_role_is_reconciled_when_mixed_with_others()
    {
        var catalog = Catalog();
        var tenant = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var roles = new[]
        {
            new TenantAdminSelfServiceReconciler.RoleRef(adminId, Admin, tenant),
            new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), "Viewer", tenant),
            new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), "SuperAdmin", tenant)
        };

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(roles, catalog, NoGrants());

        Assert.NotEmpty(planned);
        Assert.All(planned, g => Assert.Equal(adminId, g.RoleId));
    }

    [Fact]
    public void Each_tenant_admin_gets_its_own_grants_with_its_own_tenant_id()
    {
        var catalog = Catalog();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var roleA = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, tenantA);
        var roleB = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, tenantB);

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { roleA, roleB }, catalog, NoGrants());

        var affectedTenants = planned.Select(g => g.TenantId).ToHashSet();
        Assert.Equal(new HashSet<Guid> { tenantA, tenantB }, affectedTenants);
        Assert.All(planned.Where(g => g.RoleId == roleA.RoleId), g => Assert.Equal(tenantA, g.TenantId));
        Assert.All(planned.Where(g => g.RoleId == roleB.RoleId), g => Assert.Equal(tenantB, g.TenantId));
    }

    [Fact]
    public void Second_run_after_applying_grants_plans_nothing_idempotent()
    {
        var catalog = Catalog();
        var role = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, Guid.NewGuid());

        var firstRun = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, NoGrants());
        Assert.NotEmpty(firstRun);

        // Apply the first run's grants, then reconcile again — nothing new.
        var existing = firstRun.Select(g => (g.RoleId, g.PermissionId)).ToHashSet();
        var secondRun = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, existing);

        Assert.Empty(secondRun);
    }

    [Fact]
    public void Deleted_self_service_permission_is_excluded()
    {
        var catalog = Catalog();
        catalog.Single(p => p.Key == "platform.tenant-navigation.manage").IsDeleted = true;
        var role = new TenantAdminSelfServiceReconciler.RoleRef(Guid.NewGuid(), Admin, Guid.NewGuid());

        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(new[] { role }, catalog, NoGrants());

        var keys = planned
            .Select(g => catalog.Single(p => p.Id == g.PermissionId).Key)
            .ToHashSet();
        Assert.DoesNotContain("platform.tenant-navigation.manage", keys);
        Assert.Equal(2, planned.Count); // the two non-deleted self-service keys remain
    }
}
