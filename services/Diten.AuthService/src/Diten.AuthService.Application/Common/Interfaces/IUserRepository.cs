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
    // WP-INFRA-AUTH-ACCOUNT-KIND-01 — name search for the reference-picker lookup. The contract is the filter:
    // ONLY the given tenant's ACTIVE (IsActive) and non-deleted users; a blank term lists the first `limit`
    // ordered by last name, first name. Matching is on first/last name — never on email, which the lookup
    // must not let a caller probe for. Exercised against real Mongo by AccountKindEndpointTests.
    Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct);
    Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<User> CreateAsync(User user, CancellationToken ct);
    Task<User> UpdateAsync(User user, CancellationToken ct);
    Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct);
    Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// BL-529 — what a sign-in attempt writes, and ONLY that: the failed-attempt count, the lockout end and the last
    /// sign-in time, as a targeted update of the user's own row. A sign-in never rewrites the whole account: a reset that
    /// lands between the sign-in's read and its write would otherwise be undone (old hash, link and forced change back).
    /// </summary>
    Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// BL-529 — writes the account only while its password hash is still <paramref name="expectedPasswordHash"/> (the one
    /// the caller read). False when the password changed in between (nothing is written): the caller reads again.
    /// </summary>
    Task<bool> TryUpdateForTenantIfPasswordHashAsync(User user, Guid tenantId, string expectedPasswordHash, CancellationToken ct);
}
