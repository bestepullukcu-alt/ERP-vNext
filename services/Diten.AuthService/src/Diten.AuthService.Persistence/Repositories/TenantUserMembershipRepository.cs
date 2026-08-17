using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class TenantUserMembershipRepository : GlobalRepositoryBase<TenantUserMembership>, ITenantUserMembershipRepository
{
    public TenantUserMembershipRepository(IMongoDatabase database)
        : base(database, "tenant_user_memberships")
    {
    }

    public async Task<IReadOnlyList<TenantUserMembership>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var filter = Builders<TenantUserMembership>.Filter.And(
            IsDeletedFilter,
            Builders<TenantUserMembership>.Filter.Eq(x => x.UserId, userId));

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<TenantUserMembership> CreateAsync(TenantUserMembership membership, CancellationToken ct)
    {
        await InsertOneAsync(membership, ct);
        return membership;
    }
}
