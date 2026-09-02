using Diten.PpmService.Application.Features.Initiatives;

namespace Diten.PpmService.Infrastructure.Initiatives;

public sealed class UnavailableInitiativeClassificationAuthority : IInitiativeClassificationAuthority
{
    private static readonly InitiativeClassificationResult Unavailable =
        new(InitiativeAuthorityDisposition.Unavailable, []);
    public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Unavailable);
    public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Unavailable);
}
