namespace Diten.PpmService.Application.Features.Initiatives;

/// <summary>
/// Supplies the PPM-owned Initiative lifecycle contract for validation and projection.
/// This is an internal application boundary, not an external system-of-record provider.
/// </summary>
public interface IInitiativeLifecycleContractAuthority
{
    Task<InitiativeLifecycleContractsV2> GetLifecycleContractsAsync(CancellationToken cancellationToken);
}
