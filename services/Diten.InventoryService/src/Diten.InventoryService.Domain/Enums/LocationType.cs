namespace Diten.InventoryService.Domain.Enums;

/// <summary>The five levels of the place hierarchy (MOD-0173-FU02 §4). Set at create, immutable (ASSUMPTION-LOC-04).</summary>
public enum LocationType
{
    Site,
    Warehouse,
    StorageLocation,
    Zone,
    Bin
}
