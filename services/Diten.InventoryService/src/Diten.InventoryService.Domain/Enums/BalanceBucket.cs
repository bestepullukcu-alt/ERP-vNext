namespace Diten.InventoryService.Domain.Enums;

/// <summary>The status-driven bucket a balance row's OnHand is also counted in (pack §4 bucket rule).</summary>
public enum BalanceBucket
{
    Available,
    QualityInspection,
    Blocked,
    InTransit
}
