using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S3 / AG-INFRA-COMPLETION — entitlement → role-permission sync. Implements the S2 revoke spec over
// the S1 grant-source fields. Hand-written fakes (codebase convention).
public sealed class EntitlementPermissionSyncServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Actor = "entitlement-sync";

    // Catalog spanning two modules that SHARE one permission key shape (different modules, so distinct
    // Permission rows) is not needed; sharing across modules is modelled via two ModuleGrant rows for
    // the SAME permission id below.
    private static List<Permission> Catalog() =>
    [
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "create", "Create Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public async Task Grant_adds_module_permissions_admin_full_viewer_read_with_source_tag()
    {
        var (svc, roles, rolePerms, catalog) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var adminId = roles.IdOf(TenantA, "Admin");
        var viewerId = roles.IdOf(TenantA, "Viewer");

        // Admin: all 3 mdm permissions; Viewer: only the read.
        Assert.Equal(
            new[] { "mdm.legal-entities.read", "mdm.legal-entities.create", "mdm.legal-entities.delete" }.OrderBy(k => k),
            rolePerms.KeysFor(adminId, catalog).OrderBy(k => k));
        Assert.Equal(new[] { "mdm.legal-entities.read" }, rolePerms.KeysFor(viewerId, catalog));

        // All written as Module grants tagged with the normalized module code; platform.* never added.
        Assert.All(rolePerms.Rows, rp => Assert.Equal(GrantSource.Module, rp.GrantSource));
        Assert.All(rolePerms.Rows, rp => Assert.Equal("mdm", rp.SourceModuleCode));
        Assert.DoesNotContain(rolePerms.Rows, rp => catalog.Single(p => p.Id == rp.PermissionId).Key.StartsWith("platform."));
    }

    [Fact]
    public async Task Disable_removes_only_that_modules_grants_and_preserves_system_and_manual()
    {
        var (svc, roles, rolePerms, _) = Build();
        var adminId = roles.IdOf(TenantA, "Admin");
        var mdmRead = CatalogPermissionId("mdm.legal-entities.read");

        // Pre-existing baseline (System) + operator (Manual) grants on the SAME permission key the module also grants.
        rolePerms.Seed(RolePermission.SystemGrant(adminId, mdmRead, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminId, mdmRead, TenantA, "operator"));

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var remaining = rolePerms.Rows.Where(rp => rp.RoleId == adminId).ToList();
        Assert.Contains(remaining, rp => rp.GrantSource == GrantSource.System);
        Assert.Contains(remaining, rp => rp.GrantSource == GrantSource.Manual);
        Assert.DoesNotContain(remaining, rp => rp.GrantSource == GrantSource.Module);
    }

    [Fact]
    public async Task Shared_permission_survives_until_the_last_entitlement_is_removed()
    {
        // Two modules ("mdm" and "bank") both granting the same permission row to Admin is simulated by
        // granting "mdm" then seeding a second Module grant from "bank" on one of mdm's permissions.
        var (svc, roles, rolePerms, _) = Build();
        var adminId = roles.IdOf(TenantA, "Admin");
        var mdmRead = CatalogPermissionId("mdm.legal-entities.read");

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, mdmRead, TenantA, Actor, "bank")); // shared via another module

        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var rowsForRead = rolePerms.Rows.Where(rp => rp.RoleId == adminId && rp.PermissionId == mdmRead).ToList();
        Assert.Single(rowsForRead);                              // the mdm row is gone…
        Assert.Equal("bank", rowsForRead[0].SourceModuleCode);   // …the bank row keeps the permission effective
    }

    [Fact]
    public async Task Grant_is_idempotent_no_duplicate_module_grants_on_redelivery()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        var afterFirst = rolePerms.Rows.Count;
        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        Assert.Equal(afterFirst, rolePerms.Rows.Count);
    }

    [Fact]
    public async Task Disable_is_idempotent_when_nothing_to_remove()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None); // never granted

        Assert.Empty(rolePerms.Rows);
    }

    [Fact]
    public async Task Only_the_event_tenant_is_affected()
    {
        var (svc, roles, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var tenantBAdmin = roles.IdOf(TenantB, "Admin");
        Assert.All(rolePerms.Rows, rp => Assert.Equal(TenantA, rp.TenantId));
        Assert.DoesNotContain(rolePerms.Rows, rp => rp.RoleId == tenantBAdmin);
    }

    [Fact]
    public async Task Unmatched_module_code_is_a_no_op()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "DOES-NOT-EXIST", Actor, CancellationToken.None);

        Assert.Empty(rolePerms.Rows);
    }

    [Fact]
    public async Task Platform_module_code_is_a_no_op()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "platform", Actor, CancellationToken.None);

        Assert.Empty(rolePerms.Rows);
    }

    // ── harness ──

    private static readonly List<Permission> SharedCatalog = Catalog();
    private static Guid CatalogPermissionId(string key) => SharedCatalog.Single(p => p.Key == key).Id;

    private static (EntitlementPermissionSyncService svc, FakeRoleRepository roles, FakeRolePermissionRepository rolePerms, List<Permission> catalog) Build()
    {
        var catalog = SharedCatalog;
        var roles = new FakeRoleRepository(TenantA, TenantB);
        var rolePerms = new FakeRolePermissionRepository();
        var svc = new EntitlementPermissionSyncService(new FakePermissionRepository(catalog), roles, rolePerms);
        return (svc, roles, rolePerms, catalog);
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        private readonly Dictionary<(Guid, string), Role> _roles = new();

        public FakeRoleRepository(params Guid[] tenants)
        {
            foreach (var t in tenants)
            {
                foreach (var name in new[] { "Admin", "Viewer" })
                {
                    var role = new Role(name, name, null, t);
                    role.MarkAsSystem();
                    _roles[(t, name)] = role;
                }
            }
        }

        public Guid IdOf(Guid tenantId, string name) => _roles[(tenantId, name)].Id;

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult(_roles.TryGetValue((tenantId, name), out var r) ? r : null);

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository(List<Permission> catalog) : IPermissionRepository
    {
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(catalog);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public List<RolePermission> Rows { get; } = [];

        public void Seed(RolePermission rp) => Rows.Add(rp);

        public IEnumerable<string> KeysFor(Guid roleId, List<Permission> catalog) =>
            Rows.Where(rp => rp.RoleId == roleId).Select(rp => catalog.Single(p => p.Id == rp.PermissionId).Key);

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            Rows.Add(rolePermission);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolePermission>>(
                Rows.Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId && !rp.IsDeleted).ToList());

        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            Rows.RemoveAll(rp => rp.Id == id && rp.TenantId == tenantId);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }
}
