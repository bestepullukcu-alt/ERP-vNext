using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRolePermissionRepository
{
    Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct);
    Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct);
    Task AssignAsync(RolePermission rolePermission, CancellationToken ct);
    Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);
}
