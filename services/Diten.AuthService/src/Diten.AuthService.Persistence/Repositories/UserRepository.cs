using Diten.AuthService.Application.Common;
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
        await InsertOneAsync(user, ct);
        return user;
    }

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
}
