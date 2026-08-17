using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IMfaChallengeRepository
{
    Task CreateAsync(MfaChallenge challenge, CancellationToken ct);
    Task<MfaChallenge?> GetByChallengeIdHashAsync(string challengeIdHash, CancellationToken ct);
    Task UpdateAsync(MfaChallenge challenge, CancellationToken ct);
}
