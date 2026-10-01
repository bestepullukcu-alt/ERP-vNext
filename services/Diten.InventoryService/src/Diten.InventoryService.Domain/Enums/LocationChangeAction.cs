namespace Diten.InventoryService.Domain.Enums;

/// <summary>What a <see cref="Entities.LocationChangeEntry"/> records (K-11).</summary>
public enum LocationChangeAction
{
    Created,
    Updated,
    Moved,
    StatusChanged,
    BulkCreated
}
