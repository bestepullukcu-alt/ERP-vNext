using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IUserRoleRepository
{
    Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct);
    Task AssignAsync(UserRole userRole, CancellationToken ct);
    Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
}
