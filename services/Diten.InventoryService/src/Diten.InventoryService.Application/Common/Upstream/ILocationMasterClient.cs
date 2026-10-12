namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// Consumer of the frozen LOCATION v1 contract (shared Location master, DEC-INV-03). HTTP only. A lookup returns
/// <c>null</c> for a 404; every other failure throws <see cref="LocationMasterUnavailableException"/> (fail-closed,
/// ASSUMPTION-0173-06).
/// </summary>
public interface ILocationMasterClient
{
    Task<IReadOnlyList<LocationValidation>> ValidateLocationsAsync(IReadOnlyList<string> locationIds, CancellationToken cancellationToken = default);

    Task<LocationReference?> GetLocationAsync(string locationId, CancellationToken cancellationToken = default);

    Task<LocationPath?> GetLocationPathAsync(string locationId, CancellationToken cancellationToken = default);
}
