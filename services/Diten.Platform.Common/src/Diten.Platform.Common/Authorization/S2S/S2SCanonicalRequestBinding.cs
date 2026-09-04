using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Common.Authorization.S2S;

public static class S2SCanonicalRequestBinding
{
    public static async Task<string> ComputeAsync(
        HttpRequest request,
        Guid tenantId,
        string operationId,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(permissions);

        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(operationId) || permissions.Count == 0)
            throw new ArgumentException("S2S request binding input is invalid.");

        request.EnableBuffering();
        request.Body.Position = 0;
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;

        using var framed = new MemoryStream();
        Write(framed, Encoding.ASCII.GetBytes(request.Method));
        Write(framed, Encoding.UTF8.GetBytes(request.PathBase + request.Path));
        Write(framed, body.ToArray());
        Write(framed, Encoding.ASCII.GetBytes(tenantId.ToString("D")));
        Write(framed, Encoding.UTF8.GetBytes(operationId));

        foreach (var permission in permissions)
            Write(framed, Encoding.UTF8.GetBytes(permission));

        return Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
    }

    private static void Write(Stream target, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        target.Write(length);
        target.Write(value);
    }
}
