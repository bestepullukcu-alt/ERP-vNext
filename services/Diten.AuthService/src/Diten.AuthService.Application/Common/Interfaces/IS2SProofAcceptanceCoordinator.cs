using Diten.AuthService.Application.S2S;
namespace Diten.AuthService.Application.Common.Interfaces;
public interface IS2SProofAcceptanceCoordinator
{
    Task<S2SProofAcceptanceResult> TryAcceptAsync(S2SProofAcceptanceRequest request, CancellationToken cancellationToken);
}
