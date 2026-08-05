using System.Globalization;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Infrastructure.S2S;

internal static class S2SClaimProjection
{
    internal static IReadOnlyList<S2SClaim> FromProof(DelegatedActorProofV1 proof)
    {
        var claims = new List<S2SClaim>
        {
            new("typ", proof.Type), new("iss", proof.Issuer), new("aud", proof.Audience),
            new("sub", proof.ServicePrincipalId.ToString("D")), new("client_id", proof.ClientId),
            new("azp", proof.AuthorizedParty), new("actor_type", DelegatedActorProofV1.ExactActorType),
            new("tenant_id", proof.TenantId.ToString("D")), new("delegated_actor_id", proof.DelegatedActorId.ToString("D")),
            new("delegated_actor_type", DelegatedActorProofV1.ExactDelegatedActorType),
            new("delegation_id", proof.DelegationId.ToString("D")), new("operation_id", proof.OperationId),
            new("scope", proof.Scope), new("request_hash", proof.RequestHash), new("nonce", proof.Nonce), new("jti", proof.Jti),
            new("iat", proof.IssuedAt.ToString(CultureInfo.InvariantCulture)),
            new("nbf", proof.NotBefore.ToString(CultureInfo.InvariantCulture)),
            new("exp", proof.ExpiresAt.ToString(CultureInfo.InvariantCulture)),
            new("tenant_grant_version", proof.TenantGrantVersion.ToString(CultureInfo.InvariantCulture)),
            new("service_principal_version", proof.ServicePrincipalVersion.ToString(CultureInfo.InvariantCulture)),
            new("credential_generation", proof.CredentialGeneration.ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange(proof.Permissions.Select(x => new S2SClaim("permission", x)));
        return claims;
    }
}
