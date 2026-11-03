using System.Text.RegularExpressions;

namespace Diten.CrmService.Application.Common;

/// <summary>
/// MOD-0150 Contact Location &amp; PII/KVKK Hardening (2026-07-21). Defence-in-depth redaction for audit / log / error
/// telemetry. Contact identity and communication fields (name, phone, e-mail, address) are PII and must NEVER be written
/// raw into an audit event, structured log, or error message — call sites are expected to pass entityId + correlationId
/// instead. This helper is the second line of defence: even if an identity/communication substring slips through, its
/// e-mail and long-digit (phone) shapes are masked before the sink sees them. GUIDs, country codes and short numbers are
/// preserved so structured, non-PII context stays useful.
/// </summary>
public static class PiiMasking
{
    private static readonly Regex EmailPattern =
        new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    // 7+ consecutive digits ≈ a phone number. A GUID's 12-hex last group can hold 7+ digits in a row, so a whole GUID is
    // matched first (alternation order) and returned as is; only a standalone digit run is masked.
    private static readonly Regex LongDigitPattern = new(
        @"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b|\d{7,}", RegexOptions.Compiled);

    /// <summary>Redacts e-mail and phone-shaped substrings from an otherwise non-PII detail string.</summary>
    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var masked = EmailPattern.Replace(value, "***@***");
        masked = LongDigitPattern.Replace(masked, m => m.Value.Contains('-') ? m.Value : "***");
        return masked;
    }
}
