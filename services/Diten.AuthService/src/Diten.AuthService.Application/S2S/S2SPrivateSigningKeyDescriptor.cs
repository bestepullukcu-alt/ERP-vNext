using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.S2S;

public sealed record S2SPrivateSigningKeyDescriptor(
    string Issuer,
    string Kid,
    Guid CredentialId,
    Guid ServicePrincipalId,
    long Generation,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc,
    SecurityKey Key);
