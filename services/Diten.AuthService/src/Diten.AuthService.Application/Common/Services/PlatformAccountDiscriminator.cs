using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// BL-521 FIX7 item 1 — THE DISCRIMINATOR FOLLOWS THE PLATFORM RECORD AT EVERY PLATFORM-DOOR MINT. What makes an account a
/// platform account on the tenant side (<see cref="User.IsPlatformAccount"/>, which the tenant doors and the user repository's
/// target protection ask) is Auth's own copy of the actor type on the user (<c>User.PlatformActorType</c>). That copy is
/// written by provision, sync and the seeder — never by the platform door, which trusts Platform's administrator RECORD alone
/// (FIX2). An account whose copy is missing (an older provisioning, a sync that was lost, a record created before the copy
/// existed) was therefore a platform administrator at the platform door and an ORDINARY user at the tenant door — the exact
/// hole FIX6 closed for accounts that carry the copy.
///
/// <para>So the platform login, the platform refresh and the platform forced change — every place a platform token is minted
/// from a confirmed record — write the record's actor type onto the account when it is missing or different (in the token's
/// spelling, <c>platform_admin</c> / <c>partner_admin</c>). The write is TARGETED (only that field, by id and tenant): the
/// callers that later replace the whole document (login records the sign-in, the forced change the new password) carry the
/// healed value as well, because the in-memory user is updated here too — a replace of the old object would undo the write.
/// Only an account of the platform system tenant is touched. A failure is NOT swallowed: a mint that cannot mark its account
/// fails (500, retry) rather than leave an account the tenant door would accept — and FIX8 makes "cannot mark" include the
/// write that matched no account.</para>
/// </summary>
public static class PlatformAccountDiscriminator
{
    /// <summary>The event code of the log line written when a copy was missing or different and was corrected.</summary>
    public const string HealedEventCode = "AUTH_PLATFORM_ACCOUNT_DISCRIMINATOR_HEALED";

    /// <summary>The message of the failure when the marker write matched no account (no e-mail, no user id).</summary>
    public const string MarkerNotWrittenMessage = "The platform account marker was not written: no live account matched, so no platform token is minted.";

    public static async Task EnsureAsync(IUserRepository users, User user, string recordActorType, ILogger logger, CancellationToken ct)
    {
        if (user.TenantId != PlatformSystemTenant.Id || string.IsNullOrWhiteSpace(recordActorType))
        {
            return;
        }

        var actorType = recordActorType.Trim();
        if (string.Equals(user.PlatformActorType, actorType, StringComparison.Ordinal))
        {
            return;
        }

        var from = user.PlatformActorType;

        // FIX8 item 1 - FAIL CLOSED, BOTH WAYS: a write that throws propagates (nothing here catches it), and a write that
        // MATCHED NO ACCOUNT (deleted or moved since the sign-in read it) is a failure too — the loaded user is not marked and
        // no platform token is minted from an account the tenant door could still accept.
        var matched = await users.SetPlatformActorTypeAsync(user.Id, PlatformSystemTenant.Id, actorType, ct);
        if (!matched)
        {
            throw new InvalidOperationException(MarkerNotWrittenMessage);
        }

        user.SetPlatformActorType(actorType);
        logger.LogInformation(
            "Platform account discriminator corrected at mint. EventCode={EventCode} From={From} To={To} UserId={UserId}",
            HealedEventCode, from ?? "(none)", actorType, user.Id);
    }
}
