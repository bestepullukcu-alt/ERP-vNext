using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens;

/// <summary>
/// WP-GP-AUDIT-TRAIL-01 D3 — the security event of a service token request, in <c>authAuditLogs</c>.
///
/// <para>Issuing a service token changes no business record (ledger exception İ5), but WHO got a token for WHICH
/// tenant and audience — or was refused — is a security event. One row per (client code, tenant, audience, UTC day)
/// for an issued token, and one per the same key plus the result code for a refusal: the row id is derived from that
/// key and written create-if-absent, so a client that takes thousands of tokens a day leaves one row, not thousands.</para>
///
/// <para>The row carries the key and nothing else: never the secret, the token, or a hash of either.</para>
/// </summary>
public static class ServiceIdentityTokenAudit
{
    public const string IssuedEvent = "auth.service_token.issued";
    public const string DeniedEvent = "auth.service_token.denied";

    /// <summary>Result codes of a refusal. Closed list: the code is part of the row id.</summary>
    public static class Results
    {
        public const string UnknownClient = "unknown_client";
        public const string ClientRevoked = "client_revoked";
        public const string InvalidCredential = "invalid_credential";
        public const string CredentialStateInconsistent = "credential_state_inconsistent";
        public const string PurposeInconsistent = "purpose_inconsistent";
        public const string AudienceNotPermitted = "audience_not_permitted";
        public const string TenantGrantMissing = "tenant_grant_missing";
    }

    /// <summary>
    /// The client code recorded for an <see cref="Results.UnknownClient"/> refusal, instead of the code the caller
    /// sent: an unauthenticated caller could otherwise invent a new code per request and write unlimited rows a day.
    /// With the placeholder it is at most one row per (tenant, audience, day), and the invented code is not stored.
    /// </summary>
    public const string UnknownClientCode = "<unknown>";

    public static DateOnly Day(DateTimeOffset nowUtc) => DateOnly.FromDateTime(nowUtc.UtcDateTime);

    public static Guid IssuedAuditId(string clientCode, Guid tenantId, string audience, DateOnly day) =>
        AuditId(IssuedEvent, clientCode, tenantId, audience, day, null);

    public static Guid DeniedAuditId(string clientCode, Guid tenantId, string audience, DateOnly day, string result) =>
        AuditId(DeniedEvent, clientCode, tenantId, audience, day, result);

    public static string Metadata(string clientCode, string audience, DateOnly day, string? result) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["clientCode"] = clientCode,
            ["audience"] = audience,
            ["dayUtc"] = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["result"] = result ?? "issued"
        });

    /// <summary>Same event and same key → same id, on every instance and every start (name-based, RFC 9562 v8).</summary>
    private static Guid AuditId(string eventName, string clientCode, Guid tenantId, string audience, DateOnly day, string? result)
    {
        // JSON-encoded parts: a client code is caller input, so no separator is trusted to be absent from it.
        var name = "diten:auth:service-token:audit\n" + JsonSerializer.Serialize(new[]
        {
            eventName,
            clientCode,
            tenantId.ToString("D"),
            audience,
            day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            result ?? string.Empty
        });
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(name), digest);
        Span<byte> bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
