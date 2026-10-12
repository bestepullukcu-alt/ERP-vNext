using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>In-process LOCATION master for API tests; locations are registered per test with their owning legal entity.</summary>
public sealed class StubLocationMaster : ILocationMasterClient
{
    private readonly ConcurrentDictionary<string, (LocationReference Location, LocationPath Path)> _locations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _down = new(StringComparer.Ordinal);

    public void AddBin(string locationId, string warehouseId, string legalEntityId, bool stockable = true, string status = "Active")
        => _locations[locationId] = (
            new LocationReference(locationId, "Bin", legalEntityId, status, stockable),
            new LocationPath(locationId,
            [
                new LocationPathEntry("Site", "site-" + warehouseId, "SITE"),
                new LocationPathEntry("Warehouse", warehouseId, warehouseId.ToUpperInvariant()),
                new LocationPathEntry("Bin", locationId, locationId.ToUpperInvariant())
            ]));

    public void MarkUnavailable(params string[] locationIds)
    {
        foreach (var id in locationIds) _down[id] = true;
    }

    public Task<IReadOnlyList<LocationValidation>> ValidateLocationsAsync(IReadOnlyList<string> locationIds, CancellationToken cancellationToken = default)
    {
        ThrowIfDown(locationIds);
        IReadOnlyList<LocationValidation> results = locationIds
            .Select(id => _locations.TryGetValue(id, out var l)
                ? new LocationValidation(id, true, l.Location.IsStockable)
                : new LocationValidation(id, false, null))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<LocationReference?> GetLocationAsync(string locationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([locationId]);
        return Task.FromResult(_locations.TryGetValue(locationId, out var l) ? l.Location : null);
    }

    public Task<LocationPath?> GetLocationPathAsync(string locationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([locationId]);
        return Task.FromResult(_locations.TryGetValue(locationId, out var l) ? l.Path : null);
    }

    private void ThrowIfDown(IEnumerable<string> ids)
    {
        if (ids.Any(_down.ContainsKey))
        {
            throw new LocationMasterUnavailableException("stub: LOCATION unreachable");
        }
    }
}
