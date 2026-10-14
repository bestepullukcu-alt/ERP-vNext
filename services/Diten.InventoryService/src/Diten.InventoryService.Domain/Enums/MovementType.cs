namespace Diten.InventoryService.Domain.Enums;

/// <summary>
/// The 15 canonical movement types of INVENTORY-BUNDLE v1 (DEC-INV-16). Not user-extensible. Wire names are the
/// SCREAMING_SNAKE values of the frozen contract; see <see cref="Rules.WireNames"/>.
/// </summary>
public enum MovementType
{
    OpeningBalance,
    GoodsReceiptPo,
    GoodsReceiptProd,
    GoodsIssueSales,
    GoodsIssueProd,
    WarehouseTransfer,
    LocationTransfer,
    StatusTransfer,
    CustomerReturn,
    SupplierReturn,
    CountGain,
    CountLoss,
    Scrap,
    WriteOff,
    Reversal
}
