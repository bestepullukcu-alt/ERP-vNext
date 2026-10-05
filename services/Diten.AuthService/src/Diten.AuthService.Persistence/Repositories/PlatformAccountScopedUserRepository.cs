using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// BL-521 FIX6 item 13 (D') — THE ONE PLACE A TENANT ACTOR'S VIEW OF USERS LOSES THE PLATFORM ACCOUNTS. The platform system
/// tenant is also a business tenant, so a tenant administrator of it manages ordinary users next to the platform
/// administrators' own accounts (measured on FIX5: with a full <c>auth.users.*</c> token it could disable, reset the password
/// of and assign roles to another administrator). When the caller is hidden from platform accounts
/// (<see cref="IPlatformAccountVisibility"/>: since FIX8 every authenticated caller that is not a platform-door actor)
/// a platform account is ABSENT from every by-id read and every list / search the user-management endpoints use — so each of
/// their handlers (get, disable, enable, reset-password, role assignment, update, delete, resend invitation, account kind,
/// display label, lookup, reference validation) answers its own "not found" for it, indistinguishable from an id that does not
/// exist, with no per-endpoint code. A delete by id that skipped the load is a no-op for one.
///
/// <para>What is deliberately NOT hidden: the lookups by e-mail and by user name (they enforce the uniqueness of an address /
/// a user name — hiding them would let a duplicate through to the unique index), the by-reset-token lookup (the anonymous
/// set-password door holds the token), the tenant's user COUNT (quota) and every write of an object already loaded.
/// A caller that is not hidden — platform door, anonymous door, internal service call, background work — sees everything.</para>
///
/// <para>FIX7 item 2 — THE FILTER IS IN THE QUERY. A list or a search that dropped the platform accounts AFTER the database
/// had applied its limit or its page returned too few rows (a lookup with <c>limit=1</c> whose first row is a platform account
/// answered nothing although an ordinary match existed). The exclusion is now part of the Mongo filter, so the limit and the
/// page count only what the caller may see.</para>
///
/// <para>FIX7 item 4 — NO FAIL-OPEN DEFAULT. The visibility is a required constructor argument: a container that forgets to
/// register it fails to resolve this repository instead of quietly showing every platform account to every tenant actor.</para>
/// </summary>
public sealed class PlatformAccountScopedUserRepository : IUserRepository
{
    private readonly UserRepository _inner;
    private readonly IPlatformAccountVisibility _visibility;

    public PlatformAccountScopedUserRepository(UserRepository inner, IPlatformAccountVisibility visibility)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
    }

    private bool Hides => _visibility.HidesPlatformAccounts;

    public async Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var user = await _inner.GetByIdAndTenantAsync(id, tenantId, ct);
        return Hides && user is { IsPlatformAccount: true } ? null : user;
    }

    public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
        => _inner.GetAllByTenantAsync(tenantId, page, pageSize, excludePlatformAccounts: Hides, ct);

    public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct)
        => _inner.SearchActiveAsync(tenantId, term, limit, excludePlatformAccounts: Hides, ct);

    /// <summary>FIX7 item 3 — of a role's holders, the ones this caller may see: a tenant actor does not get the platform
    /// accounts among them (so a role-level action of its — ending sessions, counting holders — never reaches them).</summary>
    public async Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (!Hides || userIds.Count == 0)
        {
            return userIds;
        }

        var platformAccounts = (await _inner.PlatformAccountIdsAmongAsync(tenantId, userIds, ct)).ToHashSet();
        return userIds.Where(id => !platformAccounts.Contains(id)).ToList();
    }

    public async Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        if (Hides)
        {
            var target = await _inner.GetByIdAndTenantAsync(id, tenantId, ct);
            if (target is null || target.IsPlatformAccount)
            {
                return; // nothing a tenant actor may delete
            }
        }

        await _inner.SoftDeleteAsync(id, tenantId, ct);
    }

    public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => _inner.GetByEmailAndTenantAsync(email, tenantId, ct);
    public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => _inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
    public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => _inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
    public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => _inner.GetCountByTenantAsync(tenantId, ct);
    public Task<User> CreateAsync(User user, CancellationToken ct) => _inner.CreateAsync(user, ct);
    public Task<User> UpdateAsync(User user, CancellationToken ct) => _inner.UpdateAsync(user, ct);
    public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct) => _inner.UpdateForTenantAsync(user, tenantId, ct);
    public Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct) => _inner.SetPlatformActorTypeAsync(userId, tenantId, actorType, ct);
}
