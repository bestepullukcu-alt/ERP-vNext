using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S1 / AG-INFRA-COMPLETION Part 1 — EnsureDefaultRolesAsync must now attach baseline grants (the
// provisioning bridge), not just create empty roles. Covers new-tenant baseline, idempotency,
// tenant isolation and the platform-escalation boundary. Hand-written fakes (codebase convention).
public sealed class RoleProvisioningServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly string[] ExpectedAdmin =
        ["auth.users.read", "auth.users.create", "mdm.legal-entities.read", "mdm.legal-entities.delete"];
    private static readonly string[] ExpectedViewer =
        ["auth.users.read", "mdm.legal-entities.read"];

    private static List<Permission> Catalog() =>
    [
        new("auth", "users", "read", "Read User", null),
        new("auth", "users", "create", "Create User", null),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public async Task New_tenant_provisioning_attaches_baseline_grants_with_system_source()
    {
        var catalog = Catalog();
        var roles = new FakeRoleRepository();
        var rolePerms = new FakeRolePermissionRepository(catalog);
        var service = new RoleProvisioningService(roles, new FakePermissionRepository(catalog), rolePerms);

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);

        var adminId = roles.IdOf(TenantA, "Admin");
        var viewerId = roles.IdOf(TenantA, "Viewer");

        Assert.Equal(ExpectedAdmin.OrderBy(k => k), rolePerms.KeysFor(adminId, TenantA, catalog).OrderBy(k => k));
        Assert.Equal(ExpectedViewer.OrderBy(k => k), rolePerms.KeysFor(viewerId, TenantA, catalog).OrderBy(k => k));
        Assert.All(rolePerms.Assigned, rp => Assert.Equal(GrantSource.System, rp.GrantSource));
        Assert.All(rolePerms.Assigned, rp => Assert.Null(rp.SourceModuleCode));
    }

    [Fact]
    public async Task Platform_permissions_are_never_granted_to_tenant_roles()
    {
        var catalog = Catalog();
        var roles = new FakeRoleRepository();
        var rolePerms = new FakeRolePermissionRepository(catalog);
        var service = new RoleProvisioningService(roles, new FakePermissionRepository(catalog), rolePerms);

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);

        var allKeys = rolePerms.Assigned.Select(rp => catalog.Single(p => p.Id == rp.PermissionId).Key);
        Assert.DoesNotContain("platform.tenants.read", allKeys);
    }

    [Fact]
    public async Task Provisioning_is_idempotent_no_duplicate_grants_on_retry()
    {
        var catalog = Catalog();
        var roles = new FakeRoleRepository();
        var rolePerms = new FakeRolePermissionRepository(catalog);
        var service = new RoleProvisioningService(roles, new FakePermissionRepository(catalog), rolePerms);

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);
        var afterFirst = rolePerms.Assigned.Count;

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);

        Assert.Equal(afterFirst, rolePerms.Assigned.Count);
        Assert.Equal(ExpectedAdmin.Length + ExpectedViewer.Length, afterFirst);
    }

    [Fact]
    public async Task Grants_are_tenant_scoped_and_do_not_leak_across_tenants()
    {
        var catalog = Catalog();
        var roles = new FakeRoleRepository();
        var rolePerms = new FakeRolePermissionRepository(catalog);
        var service = new RoleProvisioningService(roles, new FakePermissionRepository(catalog), rolePerms);

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);
        await service.EnsureDefaultRolesAsync(TenantB, CancellationToken.None);

        Assert.All(rolePerms.Assigned.Where(rp => rp.TenantId == TenantA),
            rp => Assert.Equal(TenantA, rp.TenantId));

        var adminA = roles.IdOf(TenantA, "Admin");
        var adminB = roles.IdOf(TenantB, "Admin");
        Assert.NotEqual(adminA, adminB);

        // TenantB's admin grants carry only TenantB; none reference TenantA's role.
        Assert.Equal(ExpectedAdmin.OrderBy(k => k), rolePerms.KeysFor(adminB, TenantB, catalog).OrderBy(k => k));
        Assert.DoesNotContain(rolePerms.Assigned, rp => rp.RoleId == adminA && rp.TenantId == TenantB);
        Assert.DoesNotContain(rolePerms.Assigned, rp => rp.RoleId == adminB && rp.TenantId == TenantA);
    }

    [Fact]
    public async Task Empty_catalog_creates_roles_without_grants_and_does_not_throw()
    {
        var empty = new List<Permission>();
        var roles = new FakeRoleRepository();
        var rolePerms = new FakeRolePermissionRepository(empty);
        var service = new RoleProvisioningService(roles, new FakePermissionRepository(empty), rolePerms);

        await service.EnsureDefaultRolesAsync(TenantA, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, roles.IdOf(TenantA, "Admin"));
        Assert.NotEqual(Guid.Empty, roles.IdOf(TenantA, "Viewer"));
        Assert.Empty(rolePerms.Assigned);
    }

    // ── Hand-written fakes ──

    private sealed class FakeRoleRepository : IRoleRepository
    {
        private readonly Dictionary<(Guid tenantId, string name), Role> _roles = new();

        public Guid IdOf(Guid tenantId, string name) => _roles[(tenantId, name)].Id;

        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct)
        {
            if (!_roles.TryGetValue((tenantId, name), out var role))
            {
                role = new Role(name, displayName, description, tenantId);
                role.MarkAsSystem();
                _roles[(tenantId, name)] = role;
            }
            return Task.FromResult(role);
        }

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
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

    private sealed class FakeRolePermissionRepository(List<Permission> catalog) : IRolePermissionRepository
    {
        public List<RolePermission> Assigned { get; } = [];

        public IEnumerable<string> KeysFor(Guid roleId, Guid tenantId, List<Permission> cat) =>
            Assigned.Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId)
                    .Select(rp => cat.Single(p => p.Id == rp.PermissionId).Key);

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            Assigned.Add(rolePermission);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
        {
            var keys = Assigned
                .Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId && !rp.IsDeleted)
                .Select(rp => catalog.Single(p => p.Id == rp.PermissionId).Key);
            return Task.FromResult(keys);
        }

        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }
}
