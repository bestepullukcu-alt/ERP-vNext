using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public static class S2SOutboundCanonicalRequestBinding
{
    public static string Compute(
        string method,
        string absolutePath,
        ReadOnlySpan<byte> rawBody,
        Guid tenantId,
        string operation,
        IReadOnlyList<string> permissions)
    {
        if (string.IsNullOrEmpty(method)
            || !string.Equals(method, method.ToUpperInvariant(), StringComparison.Ordinal)
            || string.IsNullOrEmpty(absolutePath)
            || !absolutePath.StartsWith("/", StringComparison.Ordinal)
            || absolutePath.Contains("?", StringComparison.Ordinal)
            || tenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(operation)
            || permissions.Count == 0
            || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("S2S outbound request binding input is invalid.");
        }

        using var framed = new MemoryStream();
        Write(framed, Encoding.ASCII.GetBytes(method));
        Write(framed, Encoding.UTF8.GetBytes(absolutePath));
        Write(framed, rawBody);
        Write(framed, Encoding.ASCII.GetBytes(tenantId.ToString("D")));
        Write(framed, Encoding.UTF8.GetBytes(operation));
        foreach (var permission in permissions)
            Write(framed, Encoding.UTF8.GetBytes(permission));

        return Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
    }

    public static bool IsLowerHex64(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool FixedTimeMatches(string? supplied, string expected)
    {
        if (!IsLowerHex64(supplied) || !IsLowerHex64(expected))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied!),
            Encoding.ASCII.GetBytes(expected));
    }

    private static void Write(Stream target, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        target.Write(length);
        target.Write(value);
    }
}
