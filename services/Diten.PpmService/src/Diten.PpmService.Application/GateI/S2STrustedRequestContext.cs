using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public sealed record S2STrustedRequestContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid DelegatedActorId,
    Guid DelegationId,
    Guid ServicePrincipalId,
    Guid CredentialId,
    string ClientId,
    string Issuer,
    string Audience,
    string TokenType,
    string ProtocolScope,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string RequestHash,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public S2STrustedRequestContext Validate(TimeProvider? timeProvider = null)
    {
        if (TenantId == Guid.Empty || EffectiveActorId == Guid.Empty || DelegatedActorId == Guid.Empty
            || DelegationId == Guid.Empty || ServicePrincipalId == Guid.Empty || CredentialId == Guid.Empty)
            throw new ArgumentException("S2S trusted identity is incomplete.");
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || string.IsNullOrWhiteSpace(TokenType)
            || string.IsNullOrWhiteSpace(ProtocolScope) || string.IsNullOrWhiteSpace(OperationId)
            || !S2SOutboundCanonicalRequestBinding.IsLowerHex64(RequestHash))
            throw new ArgumentException("S2S trusted strings are invalid.");
        if (Permissions.Count == 0 || Permissions.Any(string.IsNullOrWhiteSpace)
            || Permissions.Distinct(StringComparer.Ordinal).Count() != Permissions.Count)
            throw new ArgumentException("S2S permissions must be non-empty and ordinally unique.");
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (TenantGrantVersion < 0 || ServicePrincipalVersion < 0 || CredentialGeneration < 0
            || IssuedAtUtc > NotBeforeUtc || NotBeforeUtc >= ExpiresAtUtc
            || ExpiresAtUtc - IssuedAtUtc > TimeSpan.FromMinutes(5)
            || now < NotBeforeUtc || now >= ExpiresAtUtc)
            throw new ArgumentException("S2S freshness fields are invalid.");
        return this;
    }
}
