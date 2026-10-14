using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// The ledger's LOCATION source, in-process (MOD-0173-FU02 §8, ASSUMPTION-LOC-07): answers the three calls of the
/// frozen LOCATION v1 consumer interface from <see cref="ILocationRepository"/> with the request's own scope — no
/// HTTP, no second copy or cache of the location list. A location of another company is unknown (null / exists =
/// false); so is an id that is not the canonical lower-case UUID string the location API returns (ASSUMPTION-LOC1-03).
/// A database failure is reported as <see cref="LocationMasterUnavailableException"/>, so the ledger keeps its
/// fail-closed 503 (nothing posted).
/// </summary>
public sealed class InventoryLocationDirectory : ILocationMasterClient
{
    private readonly ILocationRepository _locations;

    public InventoryLocationDirectory(ILocationRepository locations) => _locations = locations;

    public async Task<IReadOnlyList<LocationValidation>> ValidateLocationsAsync(IReadOnlyList<string> locationIds, CancellationToken cancellationToken = default)
    {
        var ids = locationIds.Select(Canonical).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var found = (await Guard(() => _locations.FindManyAsync(ids, cancellationToken))).ToDictionary(l => l.Id);

        return locationIds
            .Select(wire => Canonical(wire) is { } id && found.TryGetValue(id, out var location)
                ? new LocationValidation(wire, true, location.IsStockable)
                : new LocationValidation(wire, false, null))
            .ToList();
    }

    public async Task<LocationReference?> GetLocationAsync(string locationId, CancellationToken cancellationToken = default)
    {
        var location = await FindAsync(locationId, cancellationToken);
        return location is null
            ? null
            : new LocationReference(
                location.Id.ToString("D"),
                location.Type.ToString(),
                location.LegalEntityId?.ToString("D"),
                location.Status.ToString(),
                location.IsStockable);
    }

    public async Task<LocationPath?> GetLocationPathAsync(string locationId, CancellationToken cancellationToken = default)
    {
        var location = await FindAsync(locationId, cancellationToken);
        if (location is null)
        {
            return null;
        }

        var path = await PathAsync(_locations, location, cancellationToken);
        return new LocationPath(location.Id.ToString("D"),
            path.Select(p => new LocationPathEntry(p.Type.ToString(), p.Id.ToString("D"), p.Code)).ToList());
    }

    /// <summary>Root Site → … → the node, from the node's materialized path (ancestors outside the scope are skipped).</summary>
    public static async Task<IReadOnlyList<Location>> PathAsync(ILocationRepository locations, Location location, CancellationToken ct)
    {
        var ancestors = (await locations.FindManyAsync(location.AncestorIds, ct)).ToDictionary(a => a.Id);
        return location.AncestorIds
            .Where(ancestors.ContainsKey)
            .Select(id => ancestors[id])
            .Append(location)
            .ToList();
    }

    private async Task<Location?> FindAsync(string locationId, CancellationToken ct)
        => Canonical(locationId) is { } id ? await Guard(() => _locations.FindAsync(id, ct)) : null;

    private static Guid? Canonical(string? wire)
        => Guid.TryParseExact(wire, "D", out var id) && string.Equals(wire, id.ToString("D"), StringComparison.Ordinal) ? id : null;

    private static async Task<T> Guard<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException or MongoExecutionTimeoutException)
        {
            throw new LocationMasterUnavailableException("The location store could not be read.", ex);
        }
    }
}
