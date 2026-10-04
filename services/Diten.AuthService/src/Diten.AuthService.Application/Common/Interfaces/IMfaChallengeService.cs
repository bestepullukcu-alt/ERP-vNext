using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IMfaChallengeService
{
    Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct);
    Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct);
    Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct);

    /// <summary>
    /// BL-529 FIX2 — whether <paramref name="user"/>'s password is still the one the challenge was issued against. False
    /// after a reset (or any password change) between the two steps, and for a challenge that carries no fingerprint.
    /// </summary>
    bool IsBoundToCurrentPassword(MfaChallenge challenge, User user);
}

public sealed record MfaChallengeCreated(
    string ChallengeId,
    string MaskedDestination,
    string Channel,
    DateTime ExpiresAtUtc);
