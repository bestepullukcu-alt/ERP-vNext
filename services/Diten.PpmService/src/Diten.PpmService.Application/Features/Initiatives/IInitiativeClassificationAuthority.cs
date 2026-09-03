namespace Diten.PpmService.Application.Features.Initiatives;

public interface IInitiativeClassificationAuthority
{
    Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken cancellationToken);
    Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken cancellationToken);
}
