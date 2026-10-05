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
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.PasswordResetTokenHash, tokenHash),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

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

    public async Task<User> UpdateAsync(User user, CancellationToken ct)
    {
        return await ReplaceOneAsync(user, ct);
    }

    public async Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

        await Collection.ReplaceOneAsync(filter, user, cancellationToken: ct);
        return user;
    }

    public async Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );
        var update = Builders<User>.Update
            .Set(u => u.IsDeleted, true)
            .Set(u => u.UpdatedAt, DateTimeOffset.UtcNow);

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
