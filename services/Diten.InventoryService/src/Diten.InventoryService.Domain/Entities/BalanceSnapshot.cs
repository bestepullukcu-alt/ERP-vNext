using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// The balance row as it stood immediately after a posting — embedded in the transaction so an idempotent replay
/// returns the ORIGINAL durable result (pack §8) instead of today's balance.
/// </summary>
public sealed class BalanceSnapshot
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? WarehouseId { get; set; }

    public string? LocationId { get; set; }

    public string? LotId { get; set; }

    public StockStatus StockStatus { get; set; }

    public decimal OnHand { get; set; }

    public decimal Reserved { get; set; }

    public decimal Available { get; set; }

    public decimal InTransit { get; set; }

    public decimal Blocked { get; set; }

    public decimal QualityInspection { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public Guid? LastMovementId { get; set; }

    public DateTimeOffset? LastPostingDate { get; set; }
}
