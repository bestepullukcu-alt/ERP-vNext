using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Pack §4 bucket rule for a balance row with status S:
/// <c>OnHand = Σ</c> · <c>QualityInspection = OnHand if S = QUALITY_INSPECTION</c> ·
/// <c>Blocked = OnHand if S ∈ {BLOCKED, QUARANTINE, DAMAGED, EXPIRED}</c> · <c>InTransit = OnHand if S = IN_TRANSIT</c> ·
/// <c>Available = OnHand − Reserved if S = AVAILABLE else 0</c>.
/// </summary>
public static class BalanceBuckets
{
    public static BalanceBucket BucketFor(StockStatus status) => status switch
    {
        StockStatus.Available => BalanceBucket.Available,
        StockStatus.QualityInspection => BalanceBucket.QualityInspection,
        StockStatus.InTransit => BalanceBucket.InTransit,
        StockStatus.Blocked or StockStatus.Quarantine or StockStatus.Damaged or StockStatus.Expired => BalanceBucket.Blocked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown stock status.")
    };

    public static (decimal Available, decimal QualityInspection, decimal Blocked, decimal InTransit) Derive(
        StockStatus status, decimal onHand, decimal reserved) => BucketFor(status) switch
    {
        BalanceBucket.Available => (onHand - reserved, 0m, 0m, 0m),
        BalanceBucket.QualityInspection => (0m, onHand, 0m, 0m),
        BalanceBucket.Blocked => (0m, 0m, onHand, 0m),
        BalanceBucket.InTransit => (0m, 0m, 0m, onHand),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    /// <summary>
    /// The quantity an outbound leg may consume from a row (DEC-INV-12, negative prohibited): the unreserved
    /// Available for an AVAILABLE row, the whole OnHand otherwise.
    /// </summary>
    public static decimal Coverage(StockStatus status, decimal onHand, decimal reserved)
        => status == StockStatus.Available ? onHand - reserved : onHand;
}
