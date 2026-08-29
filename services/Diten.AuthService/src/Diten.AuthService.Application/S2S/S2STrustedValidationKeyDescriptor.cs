using Diten.AuthService.Domain.S2S;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.S2S;

public sealed record S2STrustedValidationKeyDescriptor(
    string Issuer,
    string Kid,
    Guid CredentialId,
    Guid ServicePrincipalId,
    long Generation,
    ServiceCredentialStatus Status,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? OverlapValidUntilUtc,
    bool Revoked,
    SecurityKey Key);
