using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Projection row per <see cref="InventoryDimension"/> (collection <c>inventory_balances</c>, pack §4). Never edited
/// directly: the only writer is the atomic posting path, which moves it by the signed base quantity of a
/// transaction leg under a conditional filter. All quantities are in the base UoM.
/// </summary>
public sealed class InventoryBalance : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? WarehouseId { get; set; }

    public string? LocationId { get; set; }

    public string? LotId { get; set; }

    public StockStatus StockStatus { get; set; }

    /// <summary>Reserved for P2 (pack §4); always null in S1.</summary>
    public string? OwnerId { get; set; }

    /// <summary>Reserved for P2 (pack §4); always null in S1.</summary>
    public string? ProjectId { get; set; }

    public decimal OnHand { get; set; }

    /// <summary>Always 0 in S1; reservations arrive in S2.</summary>
    public decimal Reserved { get; set; }

    public decimal Available { get; set; }

    public decimal InTransit { get; set; }

    public decimal Blocked { get; set; }

    public decimal QualityInspection { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public Guid? LastMovementId { get; set; }

    public DateTimeOffset? LastPostingDate { get; set; }

    public InventoryDimension Dimension => new(
        ItemId, SkuId, SkuLevel, WarehouseId, LocationId, LotId, StockStatus, OwnerId, ProjectId);

    public BalanceSnapshot ToSnapshot() => new()
    {
        ItemId = ItemId,
        SkuId = SkuId,
        SkuLevel = SkuLevel,
        WarehouseId = WarehouseId,
        LocationId = LocationId,
        LotId = LotId,
        StockStatus = StockStatus,
        OnHand = OnHand,
        Reserved = Reserved,
        Available = Available,
        InTransit = InTransit,
        Blocked = Blocked,
        QualityInspection = QualityInspection,
        BaseUomId = BaseUomId,
        LastMovementId = LastMovementId,
        LastPostingDate = LastPostingDate
    };
}
