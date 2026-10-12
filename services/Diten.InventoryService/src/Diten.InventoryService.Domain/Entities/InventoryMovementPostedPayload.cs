using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Internal payload of <c>InventoryMovementPosted</c>. Not part of the frozen bundle (it has no event section);
/// consumers must not depend on it until an additive contract publishes it (ASSUMPTION-0173-08).
/// </summary>
public sealed class InventoryMovementPostedPayload
{
    public Guid TransactionId { get; set; }

    public string TransactionNumber { get; set; } = string.Empty;

    public MovementType MovementType { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? FromWarehouseId { get; set; }

    public string? FromLocationId { get; set; }

    public string? ToWarehouseId { get; set; }

    public string? ToLocationId { get; set; }

    public string? LotId { get; set; }

    public StockStatus? FromStockStatus { get; set; }

    public StockStatus? ToStockStatus { get; set; }

    public decimal BaseQuantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public Guid? ReversesTransactionId { get; set; }

    public DateTimeOffset PostingDate { get; set; }
}
