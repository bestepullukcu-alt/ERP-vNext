using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Authorization;
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
        var service = CreateService(roles, grants, new FakePermissionRepository(Permission()));
        var permissionId = Guid.NewGuid();

        await service.GrantToFullCatalogRolesAsync(permissionId, CancellationToken.None);
        await service.GrantToFullCatalogRolesAsync(permissionId, CancellationToken.None);

        var grant = Assert.Single(grants.Assigned);
        Assert.Equal(superAdmin.Id, grant.RoleId);
        Assert.Equal(permissionId, grant.PermissionId);
        Assert.Equal(GrantSource.System, grant.GrantSource);
    }

    [Fact]
    public async Task Missing_full_catalog_role_does_not_throw_and_grants_nothing()
    {
        var roles = new FakeRoleRepository(role: null);
        var grants = new FakeRolePermissionRepository();
        var permissionId = Guid.NewGuid();
        var service = CreateService(roles, grants, new FakePermissionRepository(Permission()));

        await service.GrantToFullCatalogRolesAsync(permissionId, CancellationToken.None);

        Assert.Empty(grants.Assigned);
    }

    [Fact]
    public async Task Provisioning_only_recovery_permission_is_not_auto_granted_to_superadmin()
    {
        var permission = new Permission(
            "mdm",
            "product-identity.lifecycle-operations",
            "recover",
            "Recover",
            null,
            moduleOverride: "product-item-sku-master",
            scope: PermissionScope.Tenant);
        var superAdmin = new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid());
        var grants = new FakeRolePermissionRepository();
        var service = CreateService(
            new FakeRoleRepository(superAdmin),
            grants,
            new FakePermissionRepository(permission));

        await service.GrantToFullCatalogRolesAsync(permission.Id, CancellationToken.None);

        Assert.Empty(grants.Assigned);
    }

    [Fact]
    public async Task Missing_permission_is_non_throwing_and_never_reads_roles_or_grants()
    {
        var roles = new FakeRoleRepository(new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid()));
        var grants = new FakeRolePermissionRepository();
        var service = CreateService(roles, grants, new FakePermissionRepository(permission: null));

        await service.GrantToFullCatalogRolesAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, roles.LookupCount);
        Assert.Equal(0, grants.ReadCount);
        Assert.Empty(grants.Assigned);
    }

    [Fact]
    public async Task Permission_lookup_cancellation_propagates_without_grant()
    {
        var grants = new FakeRolePermissionRepository();
        var service = CreateService(
            new FakeRoleRepository(new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid())),
            grants,
            new FakePermissionRepository(permission: null, cancel: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GrantToFullCatalogRolesAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Empty(grants.Assigned);
    }

    [Fact]
    public async Task Role_lookup_cancellation_propagates_without_grant()
    {
        var grants = new FakeRolePermissionRepository();
        var service = CreateService(
            new FakeRoleRepository(role: null, cancelLookup: true),
            grants,
            new FakePermissionRepository(Permission()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GrantToFullCatalogRolesAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Empty(grants.Assigned);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Grant_repository_cancellation_propagates(bool cancelRead, bool cancelAssign)
    {
        var grants = new FakeRolePermissionRepository(cancelRead, cancelAssign);
        var service = CreateService(
            new FakeRoleRepository(new Role("SuperAdmin", "Super Administrator", null, Guid.NewGuid())),
            grants,
            new FakePermissionRepository(Permission()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GrantToFullCatalogRolesAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Empty(grants.Assigned);
    }

    private static FullCatalogPermissionGrantService CreateService(
        FakeRoleRepository roles,
        FakeRolePermissionRepository grants,
        FakePermissionRepository permissions)
        => new(roles, grants, permissions, NullLogger<FullCatalogPermissionGrantService>.Instance);

    private static Permission Permission()
        => new("auth", "users", "read", "Read Users", null);

    private sealed class FakeRoleRepository(Role? role, bool cancelLookup = false) : IRoleRepository
    {
        public int LookupCount { get; private set; }

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
        {
            LookupCount++;
            return cancelLookup
                ? Task.FromCanceled<Role?>(new CancellationToken(true))
                : Task.FromResult(string.Equals(name, "SuperAdmin", StringComparison.Ordinal) ? role : null);
        }

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository(bool cancelRead = false, bool cancelAssign = false) : IRolePermissionRepository
    {
        public List<RolePermission> Assigned { get; } = [];
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
        {
            ReadCount++;
            return cancelRead
                ? Task.FromCanceled<IReadOnlyList<RolePermission>>(new CancellationToken(true))
                : Task.FromResult<IReadOnlyList<RolePermission>>(Assigned.Where(rp => rp.RoleId == roleId).ToList());
        }

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            if (cancelAssign)
            {
                return Task.FromCanceled(new CancellationToken(true));
            }

            Assigned.Add(rolePermission);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class FakePermissionRepository(Permission? permission, bool cancel = false) : IPermissionRepository
    {
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct)
            => cancel
                ? Task.FromCanceled<Permission?>(new CancellationToken(true))
                : Task.FromResult(permission);
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
