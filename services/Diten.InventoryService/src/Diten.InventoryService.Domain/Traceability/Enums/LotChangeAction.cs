namespace Diten.InventoryService.Domain.Traceability.Enums;

/// <summary>The kind of a lot history row (pack §4 <c>LotChangeEntry</c>).</summary>
public enum LotChangeAction
{
    Created,
    Updated,
    StatusChanged,
    Linked,
    Migrated,
    SystemExpired
}
