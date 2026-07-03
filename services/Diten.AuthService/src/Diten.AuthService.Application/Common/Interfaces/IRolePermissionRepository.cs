using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRolePermissionRepository
{
    Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct);
    Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct);
    Task AssignAsync(RolePermission rolePermission, CancellationToken ct);
    Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Returns the role's grant rows (not just permission keys) so callers can inspect
    /// <see cref="RolePermission.GrantSource"/> / <see cref="RolePermission.SourceModuleCode"/> —
    /// required by the entitlement bridge for source-scoped idempotency and revoke.
    /// </summary>
    Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct);

    /// <summary>Hard-deletes a single grant row by id within the tenant scope.</summary>
    Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// FEAT-CATALOG-PERM-DELETE-SYNC — hard-deletes EVERY grant row for the given permission across ALL roles and
    /// tenants. Used when a catalog-sourced permission is removed globally (deleting the permission would otherwise
    /// leave orphan grants). Returns the number of grant rows removed.
    /// </summary>
    Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct);
}
