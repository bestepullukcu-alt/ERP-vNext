namespace Diten.InventoryService.Domain.Traceability.Enums;

/// <summary>From PRODUCT-MASTER <c>serialControlled</c> when the lot is created (pack §4, v1.1 <c>trackingMode</c>).</summary>
public enum LotTrackingMode
{
    Lot,
    LotAndSerial
}
