using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IDelegatedActorProofValidator
{
    Task<DelegatedActorProofValidationResult> ValidateAsync(DelegatedActorProofValidationRequest request, CancellationToken cancellationToken);
}
