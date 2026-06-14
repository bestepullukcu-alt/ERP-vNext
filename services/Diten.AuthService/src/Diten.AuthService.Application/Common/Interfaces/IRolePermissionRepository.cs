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
}
