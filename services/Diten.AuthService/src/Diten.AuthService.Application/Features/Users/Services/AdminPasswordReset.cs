using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// BL-529 — WHAT AN ADMINISTRATOR'S PASSWORD RESET DOES TO THE ACCOUNT, in one place for every path that resets one: the
/// tenant administrator's "Reset password" (<c>AdminResetPasswordCommandHandler</c>), a platform administrator's
/// re-invitation of an existing platform account (<c>PlatformAuthController</c> provision) and of an existing tenant
/// administrator (<c>InternalEventsController</c> tenant-admin-invited).
///
/// <para>The reset used to write a set-password link and stop there: the old password still logged in (and, because the
/// account was marked "must change password", whoever knew it then chose the NEW password — the account taken over by the
/// very person the reset was meant to lock out), and every open session went on refreshing. SAP (SU01) and Oracle both
/// invalidate the old password the moment an administrator resets it.</para>
///
/// <para><b>Order.</b> AuthService writes the user and the refresh tokens separately (no transaction). The sessions are
/// ended FIRST: if the user write then fails, the password is unchanged and the reset is simply asked again; the other
/// order would leave the account "reset" with its sessions alive and the reset refused as already pending.</para>
/// </summary>
public static class AdminPasswordReset
{
    /// <summary>A password nobody knows — the hash of a fresh random secret that is never shown, the same placeholder an
    /// invitation starts with. The old password then fails exactly like a wrong one (401, the lockout counter counts).</summary>
    public static string UnusableHash(IPasswordHasher passwordHasher, ITokenService tokenService)
        => passwordHasher.Hash(tokenService.GenerateRefreshToken());

    /// <summary>
    /// Ends every session of the account in <paramref name="tenantId"/> and replaces its password hash on the entity with
    /// <paramref name="replacementHash"/> (the caller persists the user). Returns how many sessions were ended.
    /// </summary>
    public static async Task<long> InvalidateAsync(
        User user, Guid tenantId, string replacementHash, IRefreshTokenRepository refreshTokens, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        var sessionsRevoked = await refreshTokens.RevokeAllByUserAsync(user.Id, tenantId, ct);
        user.UpdatePassword(replacementHash);
        return sessionsRevoked;
    }
}
