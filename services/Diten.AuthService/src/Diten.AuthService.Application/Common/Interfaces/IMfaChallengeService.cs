using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IMfaChallengeService
{
    Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct);
    Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct);
    Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct);
}

public sealed record MfaChallengeCreated(
    string ChallengeId,
    string MaskedDestination,
    string Channel,
    DateTime ExpiresAtUtc);
