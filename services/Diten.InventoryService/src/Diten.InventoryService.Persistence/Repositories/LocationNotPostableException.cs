namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// Raised inside a posting transaction when a leg location is no longer an Active stockable location of the caller's
/// company under the leg's warehouse (MOD-0173-FU02 K-13). Aborts the whole posting; nothing is written.
/// </summary>
internal sealed class LocationNotPostableException : Exception
{
    public LocationNotPostableException(string locationId)
        : base($"Location '{locationId}' is not postable.")
        => LocationId = locationId;

    public string LocationId { get; }
}
