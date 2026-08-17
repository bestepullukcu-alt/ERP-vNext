using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Permission?> GetByKeyAsync(string key, CancellationToken ct);
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct);
    Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct);
    Task<Permission> CreateAsync(Permission permission, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
