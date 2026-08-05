using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.AuthService.Domain.S2S;

public static class CanonicalRequestBinding
{
    private static readonly byte[] DomainSeparator = Encoding.UTF8.GetBytes("DITEN-S2S-REQUEST-V1");

    public static string Compute(string method, string path, Guid tenantId, string operationId, ReadOnlySpan<byte> body)
    {
        var exactMethod = S2SExactValue.Required(method, nameof(method));
        var exactPath = S2SExactValue.Required(path, nameof(path));
        var exactOperation = S2SExactValue.RequiredLowercase(operationId, nameof(operationId));
        if (!string.Equals(exactMethod, exactMethod.ToUpperInvariant(), StringComparison.Ordinal))
            throw new S2SContractException("HTTP method must use its exact uppercase representation.", nameof(method));
        if (!exactPath.StartsWith("/", StringComparison.Ordinal) || exactPath.Contains('?', StringComparison.Ordinal))
            throw new S2SContractException("Path must be an exact absolute path without a query string.", nameof(path));
        if (tenantId == Guid.Empty) throw new S2SContractException("Tenant id is required.", nameof(tenantId));

        var bodyDigest = SHA256.HashData(body);
        using var stream = new MemoryStream();
        stream.Write(DomainSeparator);
        WriteField(stream, Encoding.UTF8.GetBytes(exactMethod));
        WriteField(stream, Encoding.UTF8.GetBytes(exactPath));
        WriteField(stream, Encoding.UTF8.GetBytes(tenantId.ToString("D")));
        WriteField(stream, Encoding.UTF8.GetBytes(exactOperation));
        WriteField(stream, bodyDigest);
        return Base64Url(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteField(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
