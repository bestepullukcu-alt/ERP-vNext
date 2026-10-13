namespace Diten.InventoryService.Domain.Enums;

/// <summary>StockStatus dimension values of INVENTORY-BUNDLE v1 (pack §3).</summary>
public enum StockStatus
{
    Available,
    QualityInspection,
    Quarantine,
    Blocked,
    InTransit,
    Damaged,
    Expired
}
