using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// BL-529 — WHAT AN ADMINISTRATOR'S PASSWORD RESET DOES TO THE ACCOUNT, in one place for every path that resets one
/// (<see cref="AdminResetVia"/>): the tenant administrator's "Reset password" (<c>AdminResetPasswordCommandHandler</c>),
/// "Resend invitation" of an account that is no longer a pending invitation (<c>ResendUserInvitationCommandHandler</c>),
/// a platform administrator's reset of another platform administrator and the re-invitation of an existing platform
/// account (<c>PlatformAuthController</c>), and the re-invitation of an existing account as tenant administrator
/// (<c>InternalEventsController</c> tenant-admin-invited).
///
/// <para>The reset used to write a set-password link and stop there: the old password still logged in (and, because the
/// account was marked "must change password", whoever knew it then chose the NEW password — the account taken over by the
/// very person the reset was meant to lock out), and every open session went on refreshing. SAP (SU01) and Oracle both
/// invalidate the old password the moment an administrator resets it.</para>
///
/// <para><b>Order.</b> AuthService writes the user and the refresh tokens separately (no transaction). The sessions are
/// ended FIRST: if the user write then fails, the password is unchanged and the reset is simply asked again; the other
/// order would leave the account "reset" with its sessions alive.</para>
///
/// <para><b>The write is conditional</b> on the password hash the reset read: a password changed in between (the user's own
/// change, another reset) is never overwritten with a stale copy — the account is read again and the reset re-applied, at
/// most <see cref="MaxAttempts"/> times. Sign-ins no longer write the whole account (<c>RecordLoginOutcomeAsync</c>), so
/// a sign-in racing the reset cannot undo it either.</para>
///
/// <para><b>The audit row</b> (<see cref="UserAuditEvents.PasswordResetByAdmin"/>) is written in a <c>finally</c>: once a
/// session was ended the row exists, whatever fails after (the account write, the e-mail). Ids, counts and the path
/// only — never the token, the link or a password.</para>
/// </summary>
public static class AdminPasswordReset
{
    /// <summary>The <c>RevokedReason</c> every session ended by an administrator's reset carries.</summary>
    public const string RevokeReason = "admin-reset";

    public const int MaxAttempts = 3;

    /// <summary>A password nobody knows — the hash of a fresh random secret that is never shown, the same placeholder an
    /// invitation starts with. The old password then fails exactly like a wrong one (401, the lockout counter counts).</summary>
    public static string UnusableHash(IPasswordHasher passwordHasher, ITokenService tokenService)
        => passwordHasher.Hash(tokenService.GenerateRefreshToken());

    /// <summary>How a reset ended: done (<see cref="User"/> set), the account vanished, or it kept changing under it.</summary>
    public sealed record Outcome(User? User, long SessionsRevoked, bool Conflict)
    {
        public bool Succeeded => User is not null && !Conflict;
    }

    /// <summary>
    /// Resets <paramref name="user"/> (already read and checked by the caller): ends its live sessions in
    /// <paramref name="tenantId"/>, replaces its password hash with <paramref name="replacementHash"/>, applies the path's
    /// own changes (<paramref name="apply"/>: the link, the forced change, …) and writes the account — conditionally, read
    /// again through <paramref name="reload"/> and re-applied when the password changed in between. Then
    /// <paramref name="afterWrite"/> runs (the e-mail; returns whether it left). The audit row is written in every case.
    /// </summary>
    public static async Task<Outcome> ResetAsync(
        User user,
        Guid tenantId,
        AdminResetVia via,
        Func<User, string> replacementHash,
        Action<User> apply,
        Func<CancellationToken, Task<User?>> reload,
        Func<User, CancellationToken, Task<bool>>? afterWrite,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IUserAuditRecorder audit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        var targetUserId = user.Id;
        long sessionsRevoked = 0;
        var outcome = "failed";
        var emailSent = false;

        try
        {
            User? current = user;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (current is null)
                {
                    outcome = "not_found";
                    return new Outcome(null, sessionsRevoked, Conflict: false);
                }

                var readHash = current.PasswordHash;
                sessionsRevoked += await refreshTokens.RevokeLiveSessionsAsync(current.Id, tenantId, RevokeReason, ct);
                current.UpdatePassword(replacementHash(current));
                apply(current);

                if (await users.TryUpdateForTenantIfPasswordHashAsync(current, tenantId, readHash, ct))
                {
                    outcome = "reset";
                    if (afterWrite is not null)
                    {
                        emailSent = await afterWrite(current, ct);
                    }

                    return new Outcome(current, sessionsRevoked, Conflict: false);
                }

                current = await reload(ct);
            }

            outcome = "conflict";
            return new Outcome(null, sessionsRevoked, Conflict: true);
        }
        finally
        {
            await audit.RecordAsync(UserAuditEvents.PasswordResetByAdmin, tenantId, targetUserId,
                new Dictionary<string, object?>
                {
                    ["via"] = via.ToWire(),
                    ["outcome"] = outcome,
                    ["emailSent"] = emailSent,
                    ["sessionsRevoked"] = sessionsRevoked
                }, ct);
        }
    }
}

/// <summary>BL-529 — the administrator reset paths, named in the audit row's <c>via</c>.</summary>
public enum AdminResetVia
{
    UsersScreen,
    ResendInvitation,
    PlatformAdministratorReset,
    PlatformAdministratorReinvite,
    TenantAdministratorReinvite
}

public static class AdminResetViaExtensions
{
    public static string ToWire(this AdminResetVia via) => via switch
    {
        AdminResetVia.UsersScreen => "users-screen",
        AdminResetVia.ResendInvitation => "resend-invitation",
        AdminResetVia.PlatformAdministratorReset => "platform-administrator-reset",
        AdminResetVia.PlatformAdministratorReinvite => "platform-administrator-reinvite",
        AdminResetVia.TenantAdministratorReinvite => "tenant-administrator-reinvite",
        _ => throw new ArgumentOutOfRangeException(nameof(via), via, null)
    };
}
