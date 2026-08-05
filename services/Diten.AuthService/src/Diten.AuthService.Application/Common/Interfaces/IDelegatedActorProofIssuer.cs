using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IDelegatedActorProofIssuer
{
    Task<DelegatedActorProofIssuanceResult> IssueAsync(DelegatedActorProofIssuanceRequest request, CancellationToken cancellationToken);
}
