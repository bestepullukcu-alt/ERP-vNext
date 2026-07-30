using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

internal static class PpmEntitlementTestHarness
{
    internal static (EntitlementPermissionSyncService Sync, GrantRepository Grants, RoleRepository Roles) Create(
        Guid tenantId,
        IReadOnlyList<Permission> catalog)
    {
        var grants = new GrantRepository();
        var roles = new RoleRepository(tenantId);
        return (new EntitlementPermissionSyncService(
            new PermissionRepository(catalog), roles, grants, new EntitlementPermissionPolicy(),
            NullLogger<EntitlementPermissionSyncService>.Instance), grants, roles);
    }

    internal sealed class GrantRepository : IRolePermissionRepository
    {
        internal List<RolePermission> Rows { get; } = [];
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            Rows.Add(rolePermission);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolePermission>>(Rows.Where(x => x.RoleId == roleId && x.TenantId == tenantId).ToList());
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            Rows.RemoveAll(x => x.Id == id && x.TenantId == tenantId);
            return Task.CompletedTask;
        }
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class RoleRepository : IRoleRepository
    {
        private readonly Dictionary<string, Role> _roles;
        internal RoleRepository(Guid tenantId) => _roles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = new("Admin", "Admin", null, tenantId),
            ["Viewer"] = new("Viewer", "Viewer", null, tenantId)
        };
        internal Guid IdOf(string name) => _roles[name].Id;
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult(_roles.TryGetValue(name, out var role) && role.TenantId == tenantId ? role : null);
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class PermissionRepository(IReadOnlyList<Permission> catalog) : IPermissionRepository
    {
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(catalog);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
