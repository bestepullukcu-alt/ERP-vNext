using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct);
    Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct);
    Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
    // Cross-tenant lookup by the (hashed) set-password / reset token. Used by the ANONYMOUS
    // set-password endpoint where no tenant header/JWT is present. The token hash is a
    // cryptographically-random unique value, so it identifies exactly one user + tenant.
    Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<User> CreateAsync(User user, CancellationToken ct);
    Task<User> UpdateAsync(User user, CancellationToken ct);
    Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct);
    Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct);
}
