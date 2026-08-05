using Diten.AuthService.Domain.S2S;
namespace Diten.AuthService.Application.S2S;
public sealed record S2SProofAcceptanceRequest(Guid ServicePrincipalId, string ClientId, long PrincipalVersion,
    DateTimeOffset PrincipalNotBeforeUtc, DateTimeOffset? PrincipalExpiresAtUtc,
    Guid CredentialId, long CredentialGeneration, string Kid, DateTimeOffset CredentialNotBeforeUtc,
    DateTimeOffset CredentialExpiresAtUtc, DateTimeOffset AcceptedAtUtc, S2SReplayReceipt ReplayReceipt);
