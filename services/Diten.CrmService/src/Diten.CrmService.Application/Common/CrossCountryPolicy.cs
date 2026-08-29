namespace Diten.CrmService.Application.Common;

/// <summary>
/// MOD-0150 Contact Location &amp; PII/KVKK Hardening (2026-07-21). Cross-country compatibility gate for
/// Contact↔Account links and Account↔Account relationships. A cross-country relationship is a <b>controlled</b>
/// relationship, not a silent default: when both sides carry a known country and the countries differ, a business
/// reason is required before the link/relationship may be created or updated (soft-warning / reason-required — Option 1).
/// If either side has no country the check is inconclusive and never blocks (a missing country is not a violation).
/// Country codes are non-PII and safe for audit; the free-text reason is NOT written to audit/log (it may contain PII).
/// No new permission and no hard 400-block override is introduced here (that would need a new RBAC key — out of scope).
/// </summary>
public static class CrossCountryPolicy
{
    public sealed record Result(bool IsCrossCountry, string? CountryA, string? CountryB, bool ReasonRequiredButMissing);

    public static Result Evaluate(string? countryA, string? countryB, string? reason)
    {
        var a = Normalize(countryA);
        var b = Normalize(countryB);
        var isCross = a is not null && b is not null && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        var reasonMissing = isCross && string.IsNullOrWhiteSpace(reason);
        return new Result(isCross, a, b, reasonMissing);
    }

    /// <summary>Non-PII audit descriptor: country codes only, never the reason text.</summary>
    public static string AuditNote(Result r)
        => r.IsCrossCountry ? $"crossCountry={r.CountryA}->{r.CountryB}" : "sameCountry";

    private static string? Normalize(string? c) => string.IsNullOrWhiteSpace(c) ? null : c.Trim();
}
