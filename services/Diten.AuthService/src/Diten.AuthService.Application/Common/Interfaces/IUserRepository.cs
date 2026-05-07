using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct);
    Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<User> CreateAsync(User user, CancellationToken ct);
    Task<User> UpdateAsync(User user, CancellationToken ct);
    Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct);
    Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct);
}
