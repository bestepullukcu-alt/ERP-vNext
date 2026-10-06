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

    /// <summary>A set-password link of an account an administrator deactivated — the link does not switch it on (409).</summary>
    public const string AccountDeactivated = "AUTH_ACCOUNT_DEACTIVATED";
}
