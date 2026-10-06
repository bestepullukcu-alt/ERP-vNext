using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct);
    Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<Role> CreateAsync(Role role, CancellationToken ct);
    Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct);
    Task<Role> UpdateAsync(Role role, CancellationToken ct);
    /// <summary>Soft delete (IsDeleted = true). BL-412 F1 — <paramref name="deletedBy"/> is stamped as UpdatedBy in the same update.</summary>
    Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct);
}
