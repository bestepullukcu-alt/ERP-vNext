using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;

namespace Diten.InventoryService.Domain.Recall.Rules;

/// <summary>
/// Pack §8.5 (KR-C, CT-R4, CT-R11) — the recall exit block, pure. A movement of a lot with an ACTIVE <see cref="RecallLotBlock"/> is refused when
/// its TYPE takes the goods out of the recall's control: <c>GOODS_ISSUE_SALES</c> (MVP-6 shipments included), <c>GOODS_ISSUE_PROD</c>,
/// <c>WAREHOUSE_TRANSFER</c> (CT-R4). Every other type is exempt (REVERSAL, SCRAP, WRITE_OFF, STATUS_TRANSFER, LOCATION_TRANSFER, SUPPLIER_RETURN,
/// COUNT_GAIN / COUNT_LOSS, CUSTOMER_RETURN, the receipts). The exemption is decided by the movement type ONLY — there is deliberately no
/// <c>sourceModule</c> parameter (0176 O-F2-2): caller data never opens the block, and no reason or signature overrides it.
/// </summary>
public static class RecallExitRule
{
    public static readonly IReadOnlySet<MovementType> BlockedTypes = new HashSet<MovementType>
    {
        MovementType.GoodsIssueSales, MovementType.GoodsIssueProd, MovementType.WarehouseTransfer
    };

    public static bool IsBlocked(MovementType movementType) => BlockedTypes.Contains(movementType);

    /// <summary>
    /// The block that refuses the movement, or null. <paramref name="activeBlocks"/> are the blocks of THIS lot (by id, or by number + SKU for a
    /// typed lot); a lot in two active cases is refused by the first case number (deterministic).
    /// </summary>
    public static RecallLotBlock? Evaluate(MovementType movementType, StockStatus? fromStatus, StockStatus? toStatus, string lotId,
        IReadOnlyList<RecallLotBlock> activeBlocks)
    {
        _ = fromStatus;
        _ = toStatus;
        _ = lotId;
        if (!IsBlocked(movementType)) return null;
        return activeBlocks.Where(b => b.Active).OrderBy(b => b.CaseNumber, StringComparer.Ordinal).ThenBy(b => b.CaseId).FirstOrDefault();
    }

    /// <summary>The typed-lot match (ASSUMPTION-0177-06): the text equals the recalled lot's number of the same SKU (trimmed, case-insensitive).</summary>
    public static bool TypedMatches(string typedLot, Guid skuId, SkuLevel skuLevel, RecallLotBlock block)
        => block.SkuId == skuId && block.SkuLevel == skuLevel && block.LotNumber is { Length: > 0 } number
           && string.Equals(typedLot.Trim(), number.Trim(), StringComparison.OrdinalIgnoreCase);
}
