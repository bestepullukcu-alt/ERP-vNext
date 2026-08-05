using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.S2S;

public sealed record DelegatedActorProofIssuanceRequest(
    DelegatedActorProofV1 Proof,
    ServicePrincipal Principal,
    ServiceCredentialDescriptor Credential,
    string Kid);
