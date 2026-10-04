using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Features.Auth.Services;

/// <summary>
/// BL-529 — A SESSION IS ISSUED ONLY AGAINST THE PASSWORD IT WAS CHECKED AGAINST. A sign-in or a refresh reads the account,
/// checks it, and only then writes the new refresh token; an administrator's reset (or any password change) that ends
/// every session can fall between the two — it revokes what exists, and the token written a moment later would live on.
/// So after the new token is written the account is read AGAIN: if its password hash is no longer the one read at the
/// start, the new token is revoked at once and the caller answers 401.
/// <para>Not the security stamp (that is separate work): an access token already issued still runs to its expiry.</para>
/// </summary>
public static class IssuedSessionGuard
{
    public static async Task<bool> StillValidAsync(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        Guid userId,
        Guid tenantId,
        string passwordHashAtRead,
        string issuedRefreshToken,
        CancellationToken ct)
    {
        var current = await users.GetByIdAndTenantAsync(userId, tenantId, ct);
        if (current is not null && string.Equals(current.PasswordHash, passwordHashAtRead, StringComparison.Ordinal))
        {
            return true;
        }

        await refreshTokens.RevokeAsync(issuedRefreshToken, ct);
        return false;
    }
}
