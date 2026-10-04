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

    public async Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        return await Collection
            .Find(u => u.TenantId == tenantId && u.IsDeleted == false)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);
    }

    // WP-INFRA-AUTH-ACCOUNT-KIND-01 — see IUserRepository. The filter IS the contract: TenantId + IsDeleted=false +
    // IsActive=true, then each whitespace-separated token of the term must match the FIRST or the LAST name
    // (case-insensitive, regex-escaped substring), so "jane sm" finds Jane Smith. Email is deliberately not a
    // match field. Ordered by last name, first name; capped at `limit`.
    public async Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<User>>
        {
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false),
            Builders<User>.Filter.Eq(u => u.IsActive, true)
        };

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

    public async Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));
        var update = Builders<User>.Update
            .Set(u => u.FailedLoginAttempts, user.FailedLoginAttempts)
            .Set(u => u.LockoutEnd, user.LockoutEnd)
            .Set(u => u.LastLoginAt, user.LastLoginAt);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<bool> TryUpdateForTenantIfPasswordHashAsync(User user, Guid tenantId, string expectedPasswordHash, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false),
            Builders<User>.Filter.Eq(u => u.PasswordHash, expectedPasswordHash));

        var result = await Collection.ReplaceOneAsync(filter, user, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public async Task<bool> TryUpdateForTenantIfResetTokenAsync(User user, Guid tenantId, string expectedResetTokenHash, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false),
            Builders<User>.Filter.Eq(u => u.PasswordResetTokenHash, expectedResetTokenHash));

        var result = await Collection.ReplaceOneAsync(filter, user, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public async Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetTokenHash, tokenHash)
            .Set(u => u.PasswordResetTokenExpiresAt, expiresAtUtc)
            .Set(u => u.PasswordResetRequestedAt, DateTime.UtcNow)
            .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public async Task<LoginFailureOutcome> RecordLoginFailureAsync(
        Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false));

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

        var lockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
        await Collection.UpdateOneAsync(filter, Builders<User>.Update.Set(u => u.LockoutEnd, lockoutEnd), cancellationToken: ct);
        return new LoginFailureOutcome(counted.FailedLoginAttempts, lockoutEnd);
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
            .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
