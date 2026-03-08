using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
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
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );

        var users = await Collection.Find(filter).ToListAsync(ct);
        return users.FirstOrDefault(u => u.TenantId == tenantId);
    }

    public async Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var users = await Collection.Find(u => u.IsDeleted == false).ToListAsync(ct);
        return users.FirstOrDefault(u => u.Id == id && u.TenantId == tenantId);
    }

    public async Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var users = await Collection.Find(u => u.IsDeleted == false).ToListAsync(ct);
        return users
            .Where(u => u.TenantId == tenantId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }

    public async Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var users = await Collection.Find(u => u.IsDeleted == false).ToListAsync(ct);
        return users.Count(u => u.TenantId == tenantId);
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

    public async Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );

        var users = await Collection.Find(filter).ToListAsync(ct);
        var target = users.FirstOrDefault(u => u.Id == id && u.TenantId == tenantId);
        
        if (target != null)
        {
            var update = Builders<User>.Update
                .Set(u => u.IsDeleted, true)
                .Set(u => u.UpdatedAt, DateTimeOffset.UtcNow);

            await Collection.UpdateOneAsync(u => u.Id == target.Id, update, cancellationToken: ct);
        }
    }
}
