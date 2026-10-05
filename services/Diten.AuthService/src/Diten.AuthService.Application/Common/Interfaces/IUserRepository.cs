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

    // BL-521 FIX7 item 1 - the platform door's maintenance write: sets ONLY the account's platform actor type (and the update
    // time), by id and tenant. Never the whole document - a replace of an object loaded earlier would undo a concurrent change.
    // FIX8 item 1 - returns whether the write MATCHED a live account of that id and tenant (false: the account is gone or
    // was moved since it was read, nothing was written). The caller must treat false as a failed mint.
    Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct);

    // BL-521 FIX7 item 3 - of these user ids (a role's holders), the ones the CURRENT CALLER may see and act on: for a tenant
    // actor the platform accounts are not among them, so a role-level action (ending sessions, counting holders) never reaches
    // them. A caller that is not a tenant actor gets every id back. The concrete Mongo repository applies no caller scope.
    Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
