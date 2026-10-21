using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Common.Observability;

/// <summary>
/// Q151 D2 — a correlation id that proves the edge minted it, with the signature inside the id itself (the backlog
/// design, product-backlog.md "imzalı korelasyon kimliği"), so no extra header travels and every downstream still sees
/// a canonical lowercase hyphenated UUID. Layout over the 32 hex digits: [0..16) random with the version-4 nibble at
/// index 12; [16..32) the first 8 bytes of HMAC-SHA256(key, the first 8 bytes), with the RFC 4122 variant bits at
/// index 16. Verification recomputes the tail in constant time.
/// </summary>
public static class SignedCorrelation
{
    public static byte[]? KeyOf(CorrelationOptions options) =>
        string.IsNullOrWhiteSpace(options.SigningKey) ? null : Encoding.UTF8.GetBytes(options.SigningKey);

    public static string Mint(byte[] key)
    {
        Span<byte> head = stackalloc byte[8];
        RandomNumberGenerator.Fill(head);
        head[6] = (byte)((head[6] & 0x0F) | 0x40);
        var hex = Convert.ToHexString(head).ToLowerInvariant() + Tail(key, head);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    public static bool IsSignedBy(string? value, byte[] key)
    {
        if (value is null || value.Length != 36 || !Guid.TryParseExact(value, "D", out _))
        {
            return false;
        }

        var hex = value.Replace("-", string.Empty).ToLowerInvariant();
        var head = Convert.FromHexString(hex[..16]);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Tail(key, head)),
            Encoding.ASCII.GetBytes(hex[16..]));
    }

    private static string Tail(byte[] key, ReadOnlySpan<byte> head)
    {
        var mac = HMACSHA256.HashData(key, head);
        mac[0] = (byte)((mac[0] & 0x3F) | 0x80);
        return Convert.ToHexString(mac, 0, 8).ToLowerInvariant();
    }
}
