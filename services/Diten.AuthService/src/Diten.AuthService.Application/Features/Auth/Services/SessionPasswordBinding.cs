using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.Auth.Services;

/// <summary>
/// BL-529 FIX3 — A REFRESH TOKEN'S AUTHORITY IS BOUND TO THE PASSWORD IT WAS OPENED WITH. Every mint writes a keyed
/// fingerprint of the account's password hash onto the refresh token (the same keyed hash the token itself is stored
/// under — never the password hash); a rotation carries it; a refresh whose fingerprint no longer matches the account's
/// current password is refused and issues nothing. This closes what sweeps and re-reads cannot: a refresh that read the
/// NEW hash, rotated a token the sweep had not reached yet, and would otherwise pass every comparison.
/// <para>A token minted before this change carries no fingerprint: its first refresh is refused (sign in once again).</para>
/// <para>Refresh tokens only — an access token already issued still runs to its expiry (security stamp: BL-532).</para>
/// </summary>
public static class SessionPasswordBinding
{
    public static string Fingerprint(IRefreshTokenHasher hasher, User user)
        => hasher.Hash("session-password:" + user.PasswordHash);

    public static void Bind(RefreshToken token, IRefreshTokenHasher hasher, User user)
        => token.BindToPassword(Fingerprint(hasher, user));

    public static bool Matches(RefreshToken token, IRefreshTokenHasher hasher, User user)
    {
        if (string.IsNullOrEmpty(token.PasswordFingerprint))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Fingerprint(hasher, user));
        var actual = Encoding.UTF8.GetBytes(token.PasswordFingerprint);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
