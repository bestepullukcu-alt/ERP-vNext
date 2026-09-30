using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Exact wire spellings of the frozen INVENTORY-BUNDLE v1 enums. Parsing is ordinal and case-sensitive: the
/// contract enumerates exact values, so <c>opening_balance</c> is not a movement type.
/// </summary>
public static class WireNames
{
    private static readonly IReadOnlyDictionary<MovementType, string> MovementTypes = new Dictionary<MovementType, string>
    {
        [MovementType.OpeningBalance] = "OPENING_BALANCE",
        [MovementType.GoodsReceiptPo] = "GOODS_RECEIPT_PO",
        [MovementType.GoodsReceiptProd] = "GOODS_RECEIPT_PROD",
        [MovementType.GoodsIssueSales] = "GOODS_ISSUE_SALES",
        [MovementType.GoodsIssueProd] = "GOODS_ISSUE_PROD",
        [MovementType.WarehouseTransfer] = "WAREHOUSE_TRANSFER",
        [MovementType.LocationTransfer] = "LOCATION_TRANSFER",
        [MovementType.StatusTransfer] = "STATUS_TRANSFER",
        [MovementType.CustomerReturn] = "CUSTOMER_RETURN",
        [MovementType.SupplierReturn] = "SUPPLIER_RETURN",
        [MovementType.CountGain] = "COUNT_GAIN",
        [MovementType.CountLoss] = "COUNT_LOSS",
        [MovementType.Scrap] = "SCRAP",
        [MovementType.WriteOff] = "WRITE_OFF",
        [MovementType.Reversal] = "REVERSAL"
    };

    private static readonly IReadOnlyDictionary<StockStatus, string> StockStatuses = new Dictionary<StockStatus, string>
    {
        [StockStatus.Available] = "AVAILABLE",
        [StockStatus.QualityInspection] = "QUALITY_INSPECTION",
        [StockStatus.Quarantine] = "QUARANTINE",
        [StockStatus.Blocked] = "BLOCKED",
        [StockStatus.InTransit] = "IN_TRANSIT",
        [StockStatus.Damaged] = "DAMAGED",
        [StockStatus.Expired] = "EXPIRED"
    };

    private static readonly IReadOnlyDictionary<SkuLevel, string> SkuLevels = new Dictionary<SkuLevel, string>
    {
        [SkuLevel.Gsku] = "Gsku",
        [SkuLevel.Lsku] = "Lsku",
        [SkuLevel.FinishedGood] = "FinishedGood"
    };

    private static readonly IReadOnlyDictionary<string, MovementType> MovementTypesByWire =
        MovementTypes.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, StockStatus> StockStatusesByWire =
        StockStatuses.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, SkuLevel> SkuLevelsByWire =
        SkuLevels.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<StockStatus> AllStockStatuses => StockStatuses.Keys.ToArray();

    public static string ToWire(MovementType value) => MovementTypes[value];

    public static string ToWire(StockStatus value) => StockStatuses[value];

    public static string ToWire(SkuLevel value) => SkuLevels[value];

    public static bool TryParseMovementType(string? wire, out MovementType value)
        => TryParse(MovementTypesByWire, wire, out value);

    public static bool TryParseStockStatus(string? wire, out StockStatus value)
        => TryParse(StockStatusesByWire, wire, out value);

    public static bool TryParseSkuLevel(string? wire, out SkuLevel value)
        => TryParse(SkuLevelsByWire, wire, out value);

    private static bool TryParse<T>(IReadOnlyDictionary<string, T> map, string? wire, out T value)
        where T : struct
    {
        if (wire is not null && map.TryGetValue(wire, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
