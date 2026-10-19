using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX1 (AUD-001 standard field 9) — the correlation an audit record carries is the
/// REQUEST's: every record one request writes (RegisterTenantCommand writes two) can then be found together.
///
/// <para>The request correlation is the <c>X-Correlation-Id</c> the Platform's correlation middleware settles on (an
/// inbound value or one it minted), a string. A GUID string is used as it is; any other safe value is turned into a
/// stable GUID (SHA-256 of the value, first 16 bytes), so the same request still gives the same id. With no request in
/// scope (a background job, a startup seed) the caller's own correlation is kept.</para>
///
/// <para>⚠ Only the record's correlation FIELD comes from here. An idempotency key keeps the caller's own
/// correlation: a client that reuses one correlation header for two separate requests must not have the second
/// record de-duplicated away.</para>
/// </summary>
public static class AuditCorrelation
{
    /// <summary>INTX FIX2 — the metadata key of the value the CLIENT sent as its correlation (never the record's own).</summary>
    public const string ClientCorrelationMetadataKey = "ClientCorrelation";

    private const int MaxClientCorrelationLength = 128;

    /// <summary>The client's correlation value as a record may carry it: safe characters, bounded length, else nothing.</summary>
    public static string? ClientValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxClientCorrelationLength)
        {
            return null;
        }

        return value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? value : null;
    }

    public static Guid Resolve(string? requestCorrelation, Guid fallback)
    {
        if (!string.IsNullOrWhiteSpace(requestCorrelation))
        {
            var value = requestCorrelation.Trim();
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                return parsed;
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return new Guid(hash.AsSpan(0, 16));
        }

        return fallback == Guid.Empty ? Guid.NewGuid() : fallback;
    }
}
