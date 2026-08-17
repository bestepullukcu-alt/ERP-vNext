using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IUserRoleRepository
{
    Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct);
    Task AssignAsync(UserRole userRole, CancellationToken ct);
    Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);

    // AG-STEP-010 / MOD-0018-FU13 Group C — distinct, tenant-scoped user ids holding a role (active rows only).
    // Used to revoke refresh tokens for every holder when a role-permission is removed.
    Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct);
}
