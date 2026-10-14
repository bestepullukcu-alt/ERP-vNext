namespace Diten.InventoryService.Domain.Enums;

/// <summary>Location lifecycle (K-04…K-06): Active ⇄ Inactive; Active / Inactive → Archived (terminal).</summary>
public enum LocationStatus
{
    Active,
    Inactive,
    Archived
}
