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
/// set-password door holds the token) and the tenant's user COUNT (quota).</para>
///
/// <para>EVERY WRITE IS GUARDED (BL-521 / BL-529 merge, FIX1): for a hidden caller no write reaches a platform account —
/// BL-529's targeted writes (the conditional write, the sign-in record, the failed-attempt count, the link) carry "not a
/// platform account" IN THE MONGO FILTER, on the stored row (an account Platform made a platform account after the caller
/// loaded it is not written either); the platform marker write answers false; a create of a platform-marked account is
/// refused; a delete by id is a no-op. An object a hidden caller loaded through an unhidden lookup is not written.
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

            if (AfterPlatformPreCheck is { } hook) await hook();
        }

        // WP-USERS-ROLES-CLOSE-FIX1 item 6 — and on the STORED row: an account Platform marked after the check is not deleted.
        await _inner.SoftDeleteAsync(id, tenantId, excludePlatformAccounts: Hides, ct);
    }

    public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => _inner.GetByEmailAndTenantAsync(email, tenantId, ct);
    public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => _inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
    public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => _inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
    public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => _inner.GetCountByTenantAsync(tenantId, ct);
    /// <summary>FIX1 (small 1): a hidden caller (a tenant actor) cannot CREATE a platform account — only the platform door and the
    /// provisioning call (not hidden) do.</summary>
    public Task<User> CreateAsync(User user, CancellationToken ct)
        => Hides && user.IsPlatformAccount
            ? throw new InvalidOperationException(PlatformAccountNotCreatable)
            : _inner.CreateAsync(user, ct);

    public const string PlatformAccountNotCreatable = "PLATFORM_ACCOUNT_NOT_CREATABLE_BY_THIS_CALLER";
    // Merge WP-CT-LANE-BL521-MERGE-01 — BL-529 FIX4 removed the whole-document UpdateAsync / UpdateForTenantAsync; BL-529's
    // TARGETED writes take their place, and each of them keeps this class's rule (CT decision A, condition 1): a hidden caller's
    // write never reaches a platform account — it writes nothing and answers as for an account that is not there. The writes of
    // a loaded object are checked on the object (it may have been loaded through a lookup that is deliberately NOT hidden — by
    // e-mail or user name); the writes by id load first, as SoftDeleteAsync does.
    // FIX4 item 3 — the claim carries the hidden caller's stored-row exclusion like every targeted write.
    public Task<bool> TryClaimSignInAsync(Guid userId, Guid tenantId, CancellationToken ct)
        => _inner.TryClaimSignInAsync(userId, tenantId, excludePlatformAccounts: Hides, ct);

    public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct)
        => Hides && user.IsPlatformAccount
            ? Task.CompletedTask
            : _inner.RecordLoginOutcomeAsync(user, tenantId, excludePlatformAccounts: Hides, ct);

    public object CaptureState(User user) => _inner.CaptureState(user);

    /// <summary>False — "no longer meets the condition, nothing written" — for a platform account and a hidden caller.</summary>
    public Task<bool> TryWriteChangesAsync(User user, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct)
        => Hides && user.IsPlatformAccount
            ? Task.FromResult(false)
            : _inner.TryWriteChangesAsync(user, capturedState, tenantId,
                Hides ? condition with { ExcludePlatformAccounts = true } : condition, ct); // FIX1: the STORED row, not the copy

    public async Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct)
    {
        if (Hides && await IsPlatformAccountOrAbsentAsync(userId, tenantId, ct))
        {
            return new LoginFailureOutcome(0, null); // nothing counted on an account this caller may not reach
        }

        if (Hides && AfterPlatformPreCheck is { } hook) await hook();

        return await _inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes,
            excludePlatformAccounts: Hides, ct);
    }

    /// <summary>BL-529's link write goes by id WITHOUT a load, so — the rule <see cref="SoftDeleteAsync"/> follows — a hidden
    /// caller's write never reaches a platform account: it matches nothing (false), exactly as for an id that does not exist.</summary>
    public async Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct)
    {
        if (Hides && await IsPlatformAccountOrAbsentAsync(userId, tenantId, ct))
        {
            return false;
        }

        if (Hides && AfterPlatformPreCheck is { } hook) await hook();
        return await _inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, excludePlatformAccounts: Hides, ct);
    }

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 item 6 — test seam: between the in-memory pre-check and the write, for a hidden caller
    /// (where Platform can mark the account meanwhile); proves the write itself carries the stored-row exclusion.</summary>
    internal Func<Task>? AfterPlatformPreCheck { get; set; }

    private async Task<bool> IsPlatformAccountOrAbsentAsync(Guid userId, Guid tenantId, CancellationToken ct)
        => await _inner.GetByIdAndTenantAsync(userId, tenantId, ct) is not { IsPlatformAccount: false };

    /// <summary>FIX1 (small 1): the platform marker is written only by the platform door's mint and the provisioning / sync
    /// calls (not hidden); a hidden caller's marker write matches nothing (false), as for an account that is not there.</summary>
    public Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct)
        => Hides ? Task.FromResult(false) : _inner.SetPlatformActorTypeAsync(userId, tenantId, actorType, ct);
}
