using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class MfaChallengeRepository : RepositoryBase<MfaChallenge>, IMfaChallengeRepository
{
    public MfaChallengeRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mfaChallenges") { }

    public async Task CreateAsync(MfaChallenge challenge, CancellationToken ct)
    {
        await InsertOneAsync(challenge, ct);
    }

    public async Task<MfaChallenge?> GetByChallengeIdHashAsync(string challengeIdHash, CancellationToken ct)
    {
        var filter = Builders<MfaChallenge>.Filter.And(
            Builders<MfaChallenge>.Filter.Eq(x => x.ChallengeIdHash, challengeIdHash),
            Builders<MfaChallenge>.Filter.Eq(x => x.IsDeleted, false));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(MfaChallenge challenge, CancellationToken ct)
    {
        var filter = Builders<MfaChallenge>.Filter.And(
            Builders<MfaChallenge>.Filter.Eq(x => x.Id, challenge.Id),
            Builders<MfaChallenge>.Filter.Eq(x => x.TenantId, challenge.TenantId),
            Builders<MfaChallenge>.Filter.Eq(x => x.IsDeleted, false));

        await Collection.ReplaceOneAsync(filter, challenge, cancellationToken: ct);
    }
}
