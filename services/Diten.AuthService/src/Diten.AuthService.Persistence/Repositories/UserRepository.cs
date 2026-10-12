using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Common.Interfaces;
using System.Text.RegularExpressions;
using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class UserRepository : RepositoryBase<User>, IUserRepository
{
    public UserRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "users")
    {
    }

    public async Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Email, email),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return null;
        }

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.NormalizedUserName, normalizedUserName.Trim().ToLowerInvariant()),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        // Intentionally NOT tenant-scoped: the anonymous set-password caller has no tenant context.
        // The token hash is a cryptographically-random unique value, so it maps to one user + tenant.
        var filter = PasswordResetTokenLookup(tokenHash);

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    // WP-USERS-ROLES-CLOSE-FIX1 FIX3 item 7 — the link lookup states the partial index's own filter ($type: string), so the
    // planner can always use ix_users_password_reset_token_hash_v2 (it serves only a query that implies its filter).
    public static FilterDefinition<User> PasswordResetTokenLookup(string tokenHash) => Builders<User>.Filter.And(
        Builders<User>.Filter.Type(u => u.PasswordResetTokenHash, MongoDB.Bson.BsonType.String),
        Builders<User>.Filter.Eq(u => u.PasswordResetTokenHash, tokenHash),
        Builders<User>.Filter.Eq(u => u.IsDeleted, false));

    public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
        => GetAllByTenantAsync(tenantId, page, pageSize, excludePlatformAccounts: false, ct);

    // BL-521 FIX7 item 2 - the platform-account exclusion is part of the QUERY, so Skip/Limit count only the rows the caller
    // may see (dropping them in memory afterwards returned too few rows).
    public async Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<User>>
        {
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        };

        if (excludePlatformAccounts)
        {
            filters.Add(NotAPlatformAccount);
        }

        return await Collection
            .Find(Builders<User>.Filter.And(filters))
            // ATT-FIX1 — Skip/Limit needs an order to be a paging: without one, a sweep past 500 users can repeat a
            // user and miss another (natural order is not guaranteed across reads).
            .SortBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    // BL-521 FIX8 item 3 - what a tenant actor is allowed to see is decided by ONE filter (PlatformAccountFilter), equal to
    // User.IsPlatformAccount: an account whose actor type is missing, null, empty or whitespace is not a platform account.
    private static readonly FilterDefinition<User> NotAPlatformAccount = PlatformAccountFilter.IsNotPlatformAccount;

    // WP-INFRA-AUTH-ACCOUNT-KIND-01 — see IUserRepository. The filter IS the contract: TenantId + IsDeleted=false +
    // IsActive=true, then each whitespace-separated token of the term must match the FIRST or the LAST name
    // (case-insensitive, regex-escaped substring), so "jane sm" finds Jane Smith. Email is deliberately not a
    // match field. Ordered by last name, first name; capped at `limit`.
    public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct)
        => SearchActiveAsync(tenantId, term, limit, excludePlatformAccounts: false, ct);

    // BL-521 FIX7 item 2 - the exclusion is in the query: Limit(n) is n rows the caller may see, not n rows of which some are dropped.
    public async Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<User>>
        {
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false),
            Builders<User>.Filter.Eq(u => u.IsActive, true)
        };

        if (excludePlatformAccounts)
        {
            filters.Add(NotAPlatformAccount);
        }

        foreach (var token in (term ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(token), "i");
            filters.Add(Builders<User>.Filter.Or(
                Builders<User>.Filter.Regex(u => u.FirstName, pattern),
                Builders<User>.Filter.Regex(u => u.LastName, pattern)));
        }

        var safeLimit = Math.Clamp(limit, 1, 50);
        return await Collection
            .Find(Builders<User>.Filter.And(filters))
            .SortBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Limit(safeLimit)
            .ToListAsync(ct);
    }

    public async Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

        return await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct)
    {
        try
        {
            await InsertOneAsync(user, ct);
        }
        catch (MongoWriteException ex) when (IsUserEmailIndexViolation(ex))
        {
            // WP-AUTH-INVITED-LIFECYCLE-01 — a live user took this address after the handler's probe. Typed, so the
            // application answers 409 USER_EMAIL_TAKEN; any OTHER duplicate key (id, user name) still propagates.
            throw new DuplicateUserEmailException(ex);
        }

        return user;
    }

    private static bool IsUserEmailIndexViolation(MongoWriteException ex)
        => ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
           && (ex.WriteError.Message ?? string.Empty).Contains(
               Configurations.MongoDbIndexConfigurations.UserEmailIndexName, StringComparison.Ordinal);

    public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct)
        => RecordLoginOutcomeAsync(user, tenantId, excludePlatformAccounts: false, ct);

    // Merge WP-CT-LANE-BL521-MERGE-01 FIX1 (small 2) — the by-id writes take the platform-account exclusion IN the Mongo filter
    // (as the list and search do): the decorator's hidden caller writes nothing to a STORED platform account.
    private static FilterDefinition<User> LiveAccount(Guid userId, Guid tenantId, bool excludePlatformAccounts)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));
        return excludePlatformAccounts ? filter & NotAPlatformAccount : filter;
    }

    public async Task RecordLoginOutcomeAsync(User user, Guid tenantId, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filter = LiveAccount(user.Id, tenantId, excludePlatformAccounts);
        var update = Builders<User>.Update
            .Set(u => u.FailedLoginAttempts, user.FailedLoginAttempts)
            .Set(u => u.LockoutEnd, user.LockoutEnd)
            .Set(u => u.LastLoginAt, user.LastLoginAt);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public object CaptureState(User user) => user.ToBsonDocument();

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 item 6 — test seam (see RecordLoginFailureAsync).</summary>
    internal Func<Task>? AfterFailureCounted { get; set; }

    public async Task<bool> TryWriteChangesAsync(User user, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (capturedState is not BsonDocument before)
        {
            throw new ArgumentException("The captured state does not come from this repository.", nameof(capturedState));
        }

        var filters = new List<FilterDefinition<User>>
        {
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        };
        if (condition.PasswordHash is { } hash) filters.Add(Builders<User>.Filter.Eq(u => u.PasswordHash, hash));
        if (condition.IsActive is { } active) filters.Add(Builders<User>.Filter.Eq(u => u.IsActive, active));
        // BL-529 FIX7 — a field BL-529 ADDED is missing from every account written before it, and Eq(false) does not match a
        // missing field: every such account (admin@diten.com included) failed this condition for good. "Not marked" is
        // therefore "not true" — Ne(true) matches false, a missing field and null alike, which is exactly what the
        // serializer reads them as (the property's default, false). One predicate, no Or/Exists pair, no backfill.
        if (condition.DeactivatedByAdministrator is { } marked)
        {
            filters.Add(marked
                ? Builders<User>.Filter.Eq(u => u.DeactivatedByAdministrator, true)
                : Builders<User>.Filter.Ne(u => u.DeactivatedByAdministrator, true));
        }
        if (condition.PasswordResetTokenHash is { } link) filters.Add(Builders<User>.Filter.Eq(u => u.PasswordResetTokenHash, link));
        if (condition.ExcludePlatformAccounts) filters.Add(NotAPlatformAccount);
        var filter = Builders<User>.Filter.And(filters);

        // Only the elements whose value differs from the state read — never the whole document.
        var after = user.ToBsonDocument();
        var changed = new BsonDocument(after.Elements
            .Where(element => element.Name != "_id" && (!before.TryGetValue(element.Name, out var old) || !old.Equals(element.Value))));
        if (changed.ElementCount == 0)
        {
            return await Collection.Find(filter).AnyAsync(ct);
        }

        // A raw $set of the serialized values: the field serializers already ran in ToBsonDocument (a null stays a BSON null).
        UpdateDefinition<User> update = new BsonDocument("$set", changed);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct)
        => SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, excludePlatformAccounts: false, ct);

    public async Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filter = LiveAccount(userId, tenantId, excludePlatformAccounts);
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetTokenHash, tokenHash)
            .Set(u => u.PasswordResetTokenExpiresAt, expiresAtUtc)
            .Set(u => u.PasswordResetRequestedAt, DateTime.UtcNow)
            .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public Task<LoginFailureOutcome> RecordLoginFailureAsync(
        Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct)
        => RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, excludePlatformAccounts: false, ct);

    public async Task<LoginFailureOutcome> RecordLoginFailureAsync(
        Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filter = LiveAccount(userId, tenantId, excludePlatformAccounts);

        var counted = await Collection.FindOneAndUpdateAsync(
            filter,
            Builders<User>.Update.Inc(u => u.FailedLoginAttempts, 1),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After },
            ct);
        if (counted is null)
        {
            return new LoginFailureOutcome(0, null);
        }

        if (counted.FailedLoginAttempts < maxFailedAttempts)
        {
            return new LoginFailureOutcome(counted.FailedLoginAttempts, counted.LockoutEnd);
        }

        // WP-USERS-ROLES-CLOSE-FIX1 item 6 — test seam: between the count and the lockout write (the lockout write carries the
        // same filter, the platform-account exclusion included).
        if (AfterFailureCounted is { } hook) await hook();

        // WP-USERS-ROLES-CLOSE-FIX1 FIX2 — the failures of a LOCKED account are counted now (the password is checked before
        // the lock is said), but they do not extend the lockout already running: it ends when it was set to end.
        var now = DateTime.UtcNow;
        var lockoutEnd = now.AddMinutes(lockoutDurationMinutes);
        var notLockedNow = Builders<User>.Filter.Eq(u => u.LockoutEnd, null) | Builders<User>.Filter.Lte(u => u.LockoutEnd, now);
        var locked = await Collection.UpdateOneAsync(filter & notLockedNow, Builders<User>.Update.Set(u => u.LockoutEnd, lockoutEnd), cancellationToken: ct);
        return locked.MatchedCount == 1
            ? new LoginFailureOutcome(counted.FailedLoginAttempts, lockoutEnd, LockoutStarted: true)
            : new LoginFailureOutcome(counted.FailedLoginAttempts, counted.LockoutEnd);
    }

    public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
        => SoftDeleteAsync(id, tenantId, excludePlatformAccounts: false, ct);

    // WP-USERS-ROLES-CLOSE-FIX1 item 6 — the delete takes the platform-account exclusion IN the filter too (the decorator's
    // hidden caller): an account Platform made a platform account after the load is not deleted.
    public async Task SoftDeleteAsync(Guid id, Guid tenantId, bool excludePlatformAccounts, CancellationToken ct)
    {
        var filter = LiveAccount(id, tenantId, excludePlatformAccounts);
        var update = Builders<User>.Update
            .Set(u => u.IsDeleted, true)
            .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    // BL-521 FIX7 item 1 - a targeted $set, never a replace (see IUserRepository).
    public async Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));
        var update = Builders<User>.Update
            .Set(u => u.PlatformActorType, actorType.Trim())
            .Set(u => u.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount > 0; // a $set of the value the document already holds still MATCHES; only a missing account does not
    }

    // BL-521 FIX7 item 3 - this repository applies no caller scope (see IUserRepository); the decorator does.
    public Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        => Task.FromResult(userIds);

    /// <summary>The ids among <paramref name="userIds"/> (of this tenant) that belong to a platform account.</summary>
    public async Task<IReadOnlyCollection<Guid>> PlatformAccountIdsAmongAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.In(u => u.Id, userIds),
            PlatformAccountFilter.IsPlatformAccount);

        return await Collection.Find(filter).Project(u => u.Id).ToListAsync(ct);
    }
}
