using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>What a tenant set-password link says about itself — the only three answers the link door ever gives.</summary>
public enum TenantPasswordLinkState
{
    /// <summary>The link is the account's, unexpired, and the account may set its password with it.</summary>
    Usable,

    /// <summary>The link is the account's, but an administrator deactivated the account (a pending invitation keeps its link).</summary>
    Deactivated,

    /// <summary>Missing, expired, used, replaced, deleted, or not this address's — one answer for all of them.</summary>
    Invalid
}

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 item 3 (KR4) — the ONE reading of a tenant set-password link, shared by the redemption
/// (<c>SetTenantPasswordCommand</c>) and the page's read-only pre-check (<c>CheckTenantPasswordLinkQuery</c>). Sharing it is
/// what keeps the pre-check from saying more than the redemption: both see the same three states, in the same order, from the
/// same checks — the link FIRST (so "deactivated" is said only to the holder of the account's own valid link), then the
/// account. A link an administrator's deactivation DELETED (an account that is not a pending invitation, BL-529 FIX3) is
/// simply not found: "Invalid", never "Deactivated" — nothing remembers it was there.
/// </summary>
public static class TenantPasswordLink
{
    public const string InvalidMessage = "Password setup link is invalid or expired.";
    public const string DeactivatedMessage = "This account has been deactivated by an administrator.";

    public static async Task<(TenantPasswordLinkState State, User? User, string? TokenHash)> ReadAsync(
        IUserRepository users, IRefreshTokenHasher hasher, string? email, string? token, CancellationToken ct)
    {
        // An empty token is a link that does not match — never a hashing exception.
        if (string.IsNullOrWhiteSpace(token))
        {
            return (TenantPasswordLinkState.Invalid, null, null);
        }

        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var tokenHash = hasher.Hash(token);
        var user = await users.GetByPasswordResetTokenHashAsync(tokenHash, ct);

        // Generic failure for every mismatch (don't reveal which check failed). The email is re-verified against the
        // token-owner so a leaked token alone can't target a different email.
        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
            user.PasswordResetTokenExpiresAt is null ||
            user.PasswordResetTokenExpiresAt <= DateTime.UtcNow ||
            // BL-529 FIX7 item 5 — constant-time, as the platform door (hygiene: the stored value is a keyed hash).
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.PasswordResetTokenHash), Encoding.UTF8.GetBytes(tokenHash)) ||
            !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (TenantPasswordLinkState.Invalid, null, tokenHash);
        }

        // BL-529 FIX2 / FIX8 item 1 (b) — marked, or switched off before BL-529 (inactive, no mark, not a pending invitation).
        return user.IsDeactivatedByAdministrator()
            ? (TenantPasswordLinkState.Deactivated, user, tokenHash)
            : (TenantPasswordLinkState.Usable, user, tokenHash);
    }

    public static Response<NoContent> InvalidAnswer()
        => Response<NoContent>.Fail(InvalidMessage, [new ResponseError(AuthRefusalCodes.LinkInvalid)], 400);

    public static Response<NoContent> DeactivatedAnswer()
        => Response<NoContent>.Fail(DeactivatedMessage, [new ResponseError(AuthRefusalCodes.AccountDeactivated)], 409);
}
