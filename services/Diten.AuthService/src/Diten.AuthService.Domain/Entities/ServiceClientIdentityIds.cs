using System.Security.Cryptography;
using System.Text;

namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// MOD-0033-FU02 §F — the id a DECLARED service client gets. It is derived from the client code, so the id is the
/// same on every environment and is known before the identity exists: Platform's
/// <c>TrustedWorkflowStartAuthorization</c> entry names the client by this id (it is the token's <c>sub</c>), and that
/// entry has to be writable without reading Auth's database. The id is not a secret.
/// </summary>
public static class ServiceClientIdentityIds
{
    private const string Namespace = "diten:auth:service-client:";

    public static Guid FromClientCode(string clientCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientCode);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(Namespace + clientCode), digest);
        Span<byte> bytes = digest[..16];
        // RFC 9562 layout: version 8 (custom, name-based) and the 10xx variant, big-endian field order.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
