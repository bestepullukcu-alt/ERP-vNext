using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public RefreshTokenRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        IRefreshTokenHasher refreshTokenHasher)
        : base(database, tenantContext, "refreshTokens")
    {
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct)
    {
        var tokenHash = _refreshTokenHasher.Hash(token);
        var filter = Builders<RefreshToken>.Filter.And(
            Builders<RefreshToken>.Filter.Eq(t => t.Token, tokenHash),
            Builders<RefreshToken>.Filter.Eq(t => t.IsDeleted, false)
        );

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        await InsertOneAsync(refreshToken, ct);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        await ReplaceOneAsync(refreshToken, ct);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var tokenHash = _refreshTokenHasher.Hash(token);
        var filter = Builders<RefreshToken>.Filter.Eq(t => t.Token, tokenHash);
        var update = Builders<RefreshToken>.Update
            .Set(t => t.RevokedAt, DateTime.UtcNow)
            .Set(t => t.RevokedReason, "manual_revoke");
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<RefreshToken>.Filter.And(
            Builders<RefreshToken>.Filter.Eq(t => t.UserId, userId),
            Builders<RefreshToken>.Filter.Eq(t => t.TenantId, tenantId),
            Builders<RefreshToken>.Filter.Eq(t => t.RevokedAt, null)
        );

        var update = Builders<RefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);
        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
