using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Permissions;

// A1 — a first-time permission must reach the full-catalog SuperAdmin role exactly once (idempotent),
// and a missing role must not throw (best-effort).
public sealed class FullCatalogPermissionGrantServiceTests
{
    [Fact]
    public async Task Grants_new_permission_to_superadmin_once_and_is_idempotent()
    {
        var superAdmin = new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid());
        var roles = new FakeRoleRepository(superAdmin);
        var grants = new FakeRolePermissionRepository();
        var service = new FullCatalogPermissionGrantService(roles, grants, NullLogger<FullCatalogPermissionGrantService>.Instance);
        var permission = new Permission("platform", "workflow.definitions", "view", "View Workflow", null);

        await service.GrantToFullCatalogRolesAsync(permission, CancellationToken.None);
        await service.GrantToFullCatalogRolesAsync(permission, CancellationToken.None);

        var grant = Assert.Single(grants.Assigned);
        Assert.Equal(superAdmin.Id, grant.RoleId);
        Assert.Equal(permission.Id, grant.PermissionId);
        Assert.Equal(GrantSource.System, grant.GrantSource);
    }

    [Fact]
    public async Task Missing_full_catalog_role_does_not_throw_and_grants_nothing()
    {
        var roles = new FakeRoleRepository(role: null);
        var grants = new FakeRolePermissionRepository();
        var service = new FullCatalogPermissionGrantService(roles, grants, NullLogger<FullCatalogPermissionGrantService>.Instance);

        await service.GrantToFullCatalogRolesAsync(
            new Permission("platform", "workflow.definitions", "view", "View Workflow", null),
            CancellationToken.None);

        Assert.Empty(grants.Assigned);
    }

    [Fact]
    public async Task Recovery_permission_is_never_granted_to_the_full_catalog_human_role()
    {
        var superAdmin = new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid());
        var grants = new FakeRolePermissionRepository();
        var service = new FullCatalogPermissionGrantService(
            new FakeRoleRepository(superAdmin),
            grants,
            NullLogger<FullCatalogPermissionGrantService>.Instance);
        var recovery = new Permission(
            "mdm",
            "product-identity.lifecycle-operations",
            "recover",
            "Recover Product Identity Lifecycle Operation",
            null,
            moduleOverride: "product-item-sku-master",
            scope: PermissionScope.Tenant);

        await service.GrantToFullCatalogRolesAsync(recovery, CancellationToken.None);
        await service.GrantToFullCatalogRolesAsync(recovery, CancellationToken.None);

        Assert.Empty(grants.Assigned);
    }

    private sealed class FakeRoleRepository(Role? role) : IRoleRepository
    {
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult(string.Equals(name, "SuperAdmin", StringComparison.Ordinal) ? role : null);

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public List<RolePermission> Assigned { get; } = [];

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolePermission>>(Assigned.Where(rp => rp.RoleId == roleId).ToList());

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            Assigned.Add(rolePermission);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }
}
