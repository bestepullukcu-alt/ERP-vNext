namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>LOCATION <c>getLocationPath</c>.</summary>
public sealed record LocationPath(string? LocationId, IReadOnlyList<LocationPathEntry> Path)
{
    public bool ContainsWarehouse(string warehouseId) => Path.Any(p =>
        string.Equals(p.Type, "Warehouse", StringComparison.Ordinal)
        && string.Equals(p.Id, warehouseId, StringComparison.Ordinal));
}
