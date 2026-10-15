namespace Diten.AuthService.Application.Common;

/// <summary>
/// BL-529 FIX3 — the stable codes of the sign-in / password refusals BL-529 added (the sibling of
/// <see cref="PasswordErrorCodes"/>, which stays the password POLICY's list). AuthService is a headless API: the English
/// sentence stays in <c>errors</c>, the code travels in <c>errorCodes</c>, and the Web's AuthGateway turns it into the
/// reader's language (SharedResource, seven languages). <c>AuthRefusalCodeContractTests</c> holds code ⇔ map ⇔ resx.
/// </summary>
public static class AuthRefusalCodes
{
    /// <summary>An anonymous password door (forgot password, the set-password link) was asked too often (429).</summary>
    public const string TooManyRequests = "AUTH_TOO_MANY_REQUESTS";

    /// <summary>The password changed while a change of it was running (an administrator's reset, typically) — 409.</summary>
    public const string PasswordChangedMeanwhile = "AUTH_PASSWORD_CHANGED_MEANWHILE";

    /// <summary>A set-password link of an account an administrator deactivated — the link does not switch it on (409). BL-496:
    /// also a sign-in with the RIGHT password on a deactivated account (said only after the password matched).</summary>
    public const string AccountDeactivated = "AUTH_ACCOUNT_DEACTIVATED";

    // WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — every refusal of the sign-in, forced-change and set-password doors carries a
    // code; the screens never show the service's English again.

    /// <summary>The one answer of a sign-in that did not match — no such address, a wrong password, an account of another kind
    /// at this door, a LOCKED account whatever the password (FIX3: a lock is never said — "AUTH_ACCOUNT_LOCKED" is gone): the
    /// SAME code and sentence, so the answer tells neither an address that exists nor a password that is right.</summary>
    public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";

    /// <summary>The sign-in cannot be decided now (a dependency did not answer) — try again.</summary>
    public const string LoginUnavailable = "AUTH_LOGIN_UNAVAILABLE";

    /// <summary>The tenant switched e-mail sign-in off.</summary>
    public const string EmailLoginDisabled = "AUTH_EMAIL_LOGIN_DISABLED";

    /// <summary>A platform administrator's temporary password expired before it was changed.</summary>
    public const string TemporaryPasswordExpired = "AUTH_TEMPORARY_PASSWORD_EXPIRED";

    /// <summary>The platform door: the account holds no platform role (403).</summary>
    public const string PlatformPrivilegesRequired = "AUTH_PLATFORM_PRIVILEGES_REQUIRED";

    /// <summary>The second step: the verification code did not match (or its challenge is over).</summary>
    public const string InvalidVerificationCode = "AUTH_INVALID_VERIFICATION_CODE";

    /// <summary>A forced change whose current password did not match (the person is signed in: no enumeration here).</summary>
    public const string CurrentPasswordIncorrect = "AUTH_CURRENT_PASSWORD_INCORRECT";

    /// <summary>The session that asked is no longer valid (ended, the account switched off, the password moved) — sign in again.</summary>
    public const string SessionEnded = "AUTH_SESSION_ENDED";

    /// <summary>A set-password link that is missing, expired, used, replaced or deleted — one answer for all of them.</summary>
    public const string LinkInvalid = "AUTH_LINK_INVALID";
}
