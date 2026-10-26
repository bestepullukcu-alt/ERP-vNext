namespace Diten.InventoryService.Domain.Traceability.Enums;

/// <summary>The kind of a lot history row (pack §4 <c>LotChangeEntry</c>).</summary>
public enum LotChangeAction
{
    Created,
    Updated,
    StatusChanged,
    Linked,
    Migrated,
    SystemExpired,

    /// <summary>MOD-0176 F2 (TRACE 1.4.0, KF-D): an expiry / retest date set apart from the suggestion with a consumed override.</summary>
    ExpiryOverridden
}
