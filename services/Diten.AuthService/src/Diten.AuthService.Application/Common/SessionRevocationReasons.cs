namespace Diten.AuthService.Application.Common;

/// <summary>
/// BL-529 FIX3 — the <c>RevokedReason</c>s that mean "this session ended because the password changed". A token revoked
/// for one of them that is presented again is a stale tab, not a stolen token: it gets 401 and nothing else (the reuse
/// detection that ends every other session is for "rotated" and the rest).
/// </summary>
public static class SessionRevocationReasons
{
    /// <summary>An administrator reset the password (AdminPasswordReset).</summary>
    public const string AdminReset = "admin-reset";

    /// <summary>The owner changed or set the password, or a session issued against the old one was caught afterwards.</summary>
    public const string PasswordChanged = "password-changed";

    public static bool EndedByPasswordChange(string? reason)
        => string.Equals(reason, AdminReset, StringComparison.Ordinal)
           || string.Equals(reason, PasswordChanged, StringComparison.Ordinal);
}
