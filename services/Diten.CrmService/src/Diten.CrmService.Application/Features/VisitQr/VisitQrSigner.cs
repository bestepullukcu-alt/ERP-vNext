using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Diten.CrmService.Application.Features.VisitQr;

/// <summary>WP-VW-W3E (W3E-BE-a, §2.2 / §2.7) — the keys (PKS-001; Platform CrmManifestProvider; catalog rows in the Auth
/// seed; no grant).</summary>
public static class VisitQrPermissions
{
    public const string Read = "crm.visit-qr.read";
    public const string Manage = "crm.visit-qr.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage };
}

public static class VisitQrLimits
{
    public const int MaxPayloadLength = 200;
    public const int MaxBulk = 200;
    public const int MaxReasonLength = 500;
    public const int MaxPageSize = 100;

    /// <summary>K-W3E-5 — at most this many parents above the visit's institution count (the 6th does not).</summary>
    public const int MaxParentDepth = 5;

    /// <summary>The "last 30 days" scan count of the institution read.</summary>
    public const int RecentScanDays = 30;
}

/// <summary>
/// The signing keys as the host provides them (secret prefix <c>crm-visit-qr-signing</c>, active key
/// <c>Crm:VisitQr:ActiveKeyId</c>). <see cref="Available"/> = an active key with its bytes; old key ids stay in
/// <see cref="Keys"/> so the codes they signed keep verifying after a rotation.
/// </summary>
public sealed record VisitQrKeys(string? ActiveKeyId, IReadOnlyDictionary<string, byte[]> Keys)
{
    public static VisitQrKeys None { get; } = new(null, new Dictionary<string, byte[]>(StringComparer.Ordinal));

    public bool Available => ActiveKeyId is not null && Keys.TryGetValue(ActiveKeyId, out var key) && key.Length >= VisitQrSigner.MinKeyBytes;
}

/// <summary>The host seam that reads the signing keys (Api: <c>ISecretsProvider</c>; tests inject their own).</summary>
public interface IVisitQrKeyRing
{
    Task<VisitQrKeys> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.2, K-W3E-2) — the institution QR payload: <c>DQR1.&lt;accountId base64url 22&gt;.&lt;version&gt;.&lt;keyId&gt;.&lt;HMAC base64url&gt;</c>
/// (≤ 200 characters). The HMAC-SHA256 key is DERIVED PER TENANT — HKDF-SHA256(master[keyId], info = tenant id) — so a
/// code of another tenant fails the signature (<c>invalid</c>) and the payload carries no tenant information. The
/// comparison is constant-time (<see cref="CryptographicOperations.FixedTimeEquals"/>, the Auth ESignReauthTicketService
/// pattern). Pure: no I/O, no clock; the keys come in.
/// </summary>
public static class VisitQrSigner
{
    public const string Prefix = "DQR1";
    public const int MinKeyBytes = 32;

    private static readonly Regex KeyIdPattern = new("^[A-Za-z0-9_-]{1,32}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("diten-crm-visit-qr-v1");

    public static bool IsValidKeyId(string? keyId) => keyId is not null && KeyIdPattern.IsMatch(keyId);

    /// <summary>The tenant's key: HKDF(master, salt, info = tenant id "D") → 32 bytes.</summary>
    public static byte[] DeriveTenantKey(byte[] master, Guid tenantId)
        => HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, Salt, Encoding.UTF8.GetBytes(tenantId.ToString("D")));

    /// <summary>The payload of <paramref name="accountId"/> at <paramref name="version"/>, signed with the ACTIVE key.</summary>
    public static string Sign(VisitQrKeys keys, Guid tenantId, Guid accountId, int version)
    {
        if (!keys.Available)
        {
            throw new InvalidOperationException("The visit QR signing key is not available.");
        }

        return Sign(keys.Keys[keys.ActiveKeyId!], keys.ActiveKeyId!, tenantId, accountId, version);
    }

    /// <summary>The payload signed with a GIVEN key id (a reprint uses the key the code was issued with).</summary>
    public static string Sign(VisitQrKeys keys, string keyId, Guid tenantId, Guid accountId, int version)
    {
        if (!keys.Keys.TryGetValue(keyId, out var master) || master.Length < MinKeyBytes)
        {
            throw new InvalidOperationException("The visit QR signing key is not available.");
        }

        return Sign(master, keyId, tenantId, accountId, version);
    }

    private static string Sign(byte[] master, string keyId, Guid tenantId, Guid accountId, int version)
    {
        if (!IsValidKeyId(keyId) || version < 1)
        {
            throw new ArgumentException("Invalid key id or version.");
        }

        var body = Body(accountId, version, keyId);
        return body + "." + Base64Url(HMACSHA256.HashData(DeriveTenantKey(master, tenantId), Encoding.UTF8.GetBytes(body)));
    }

    /// <summary>The parsed + VERIFIED payload, or null (bad shape, unknown key, wrong signature, another tenant).</summary>
    public static VisitQrPayload? Verify(VisitQrKeys keys, Guid tenantId, string? payload)
    {
        var parsed = Parse(payload);
        if (parsed is null || !keys.Keys.TryGetValue(parsed.KeyId, out var master) || master.Length < MinKeyBytes)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(DeriveTenantKey(master, tenantId), Encoding.UTF8.GetBytes(Body(parsed.AccountId, parsed.Version, parsed.KeyId)));
        return FixedEquals(expected, parsed.Signature) ? parsed : null;
    }

    /// <summary>The shape only (no signature check): <c>DQR1.&lt;22&gt;.&lt;1..9 digits&gt;.&lt;keyId&gt;.&lt;43&gt;</c>, ≤ 200.</summary>
    public static VisitQrPayload? Parse(string? payload)
    {
        var text = payload?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > VisitQrLimits.MaxPayloadLength)
        {
            return null;
        }

        var parts = text.Split('.');
        if (parts.Length != 5 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal) || parts[1].Length != 22
            || parts[2].Length is < 1 or > 9 || !parts[2].All(char.IsAsciiDigit) || parts[2][0] == '0'
            || !IsValidKeyId(parts[3]) || parts[4].Length != 43)
        {
            return null;
        }

        var account = FromBase64Url(parts[1]);
        var signature = FromBase64Url(parts[4]);
        // canonical only: the last base64url character carries unused bits, so "…A" and "…B" can decode to the same bytes —
        // a payload must be exactly the encoding we write (one changed character is always invalid)
        if (account is not { Length: 16 } || signature is not { Length: 32 } || !int.TryParse(parts[2], out var version)
            || !string.Equals(Base64Url(account), parts[1], StringComparison.Ordinal)
            || !string.Equals(Base64Url(signature), parts[4], StringComparison.Ordinal))
        {
            return null;
        }

        return new VisitQrPayload(new Guid(account), version, parts[3], signature);
    }

    private static string Body(Guid accountId, int version, string keyId)
        => Prefix + "." + Base64Url(accountId.ToByteArray()) + "." + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + keyId;

    private static bool FixedEquals(byte[] expected, byte[] actual)
        => expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string text)
    {
        if (text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            return null;
        }

        var s = text.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>A parsed payload (<see cref="Signature"/> = the 32 HMAC bytes).</summary>
public sealed record VisitQrPayload(Guid AccountId, int Version, string KeyId, byte[] Signature);
