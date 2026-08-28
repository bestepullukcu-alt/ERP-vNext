namespace Diten.AuthService.Application.Common;

/// <summary>
/// Privacy-preserving identity masking for user references. Produces hints like "D***a" and "a***@diten.com" so a
/// caller can recognise which user is referenced without exposing full PII (used by the user lookup-validation path).
/// </summary>
public static class MaskedIdentity
{
    public static string MaskToken(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) return string.Empty;
        if (s.Length == 1) return s + "*";
        if (s.Length == 2) return s[0] + "*";
        return $"{s[0]}***{s[^1]}";
    }

    public static string MaskName(string? firstName, string? lastName)
    {
        var first = MaskToken(firstName);
        // A single masked token (the first name) matches the "D***a" convention; fall back to the last name if needed.
        return first.Length > 0 ? first : MaskToken(lastName);
    }

    public static string MaskEmail(string? email)
    {
        var e = (email ?? string.Empty).Trim();
        if (e.Length == 0) return string.Empty;
        var at = e.IndexOf('@');
        if (at <= 0) return MaskToken(e);
        var local = e[..at];
        var domain = e[at..]; // keeps the leading '@'
        var maskedLocal = local.Length == 0 ? string.Empty : local[0] + "***";
        return maskedLocal + domain;
    }
}
