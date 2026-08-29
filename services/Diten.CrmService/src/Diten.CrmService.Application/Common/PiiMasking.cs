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

    // 7+ consecutive digits ≈ a phone number. GUID hex segments contain letters, so they are not caught here.
    private static readonly Regex LongDigitPattern = new(@"\d{7,}", RegexOptions.Compiled);

    /// <summary>Redacts e-mail and phone-shaped substrings from an otherwise non-PII detail string.</summary>
    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var masked = EmailPattern.Replace(value, "***@***");
        masked = LongDigitPattern.Replace(masked, "***");
        return masked;
    }
}
