using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class PlatformUserRepository : GlobalRepositoryBase<PlatformUser>, IPlatformUserRepository
{
    public PlatformUserRepository(IMongoDatabase database)
        : base(database, "platform_users")
    {
    }

    public async Task<PlatformUser?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var filter = Builders<PlatformUser>.Filter.And(
            IsDeletedFilter,
            Builders<PlatformUser>.Filter.Eq(x => x.Email, email));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<PlatformUser> CreateAsync(PlatformUser user, CancellationToken ct)
    {
        await InsertOneAsync(user, ct);
        return user;
    }
}
