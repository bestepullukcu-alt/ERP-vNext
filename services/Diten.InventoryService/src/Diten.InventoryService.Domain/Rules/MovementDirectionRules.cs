using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Pack §12 movement direction matrix (derived from DEC-INV-16 semantics).
/// <list type="table">
/// <item>Inbound — from* forbidden · to warehouse + location required · target status defaults to AVAILABLE.</item>
/// <item>Outbound — from warehouse + location required · to* forbidden · source status defaults to AVAILABLE.</item>
/// <item>Warehouse transfer — both sides required, warehouse differs, single-step: same status on both sides.</item>
/// <item>Location transfer — both sides required, same warehouse, location differs, same status on both sides.</item>
/// <item>Status transfer — both sides required, same warehouse + location, from status ≠ to status.</item>
/// <item>Reversal — every side is derived from the original transaction; the request must not carry any.</item>
/// </list>
/// "from*" / "to*" include the side's stock status, so an inbound request naming a <c>fromStockStatus</c> is rejected
/// like one naming a <c>fromWarehouseId</c>.
/// </summary>
public static class MovementDirectionRules
{
    public static MovementClass Classify(MovementType type) => type switch
    {
        MovementType.OpeningBalance or MovementType.GoodsReceiptPo or MovementType.GoodsReceiptProd
            or MovementType.CustomerReturn or MovementType.CountGain => MovementClass.Inbound,
        MovementType.GoodsIssueSales or MovementType.GoodsIssueProd or MovementType.SupplierReturn
            or MovementType.CountLoss or MovementType.Scrap or MovementType.WriteOff => MovementClass.Outbound,
        MovementType.WarehouseTransfer => MovementClass.WarehouseTransfer,
        MovementType.LocationTransfer => MovementClass.LocationTransfer,
        MovementType.StatusTransfer => MovementClass.StatusTransfer,
        MovementType.Reversal => MovementClass.Reversal,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown movement type.")
    };

    /// <summary>
    /// Applies the matrix. Returns the normalised direction, or a human-readable reason (English) the request is
    /// structurally invalid. Blank strings count as omitted.
    /// </summary>
    public static (MovementDirection? Direction, string? Error) Resolve(
        MovementType type,
        string? fromWarehouseId,
        string? fromLocationId,
        StockStatus? fromStockStatus,
        string? toWarehouseId,
        string? toLocationId,
        StockStatus? toStockStatus)
    {
        var fromWh = Blank(fromWarehouseId);
        var fromLoc = Blank(fromLocationId);
        var toWh = Blank(toWarehouseId);
        var toLoc = Blank(toLocationId);
        var hasAnyFrom = fromWh is not null || fromLoc is not null || fromStockStatus.HasValue;
        var hasAnyTo = toWh is not null || toLoc is not null || toStockStatus.HasValue;
        var name = WireNames.ToWire(type);

        switch (Classify(type))
        {
            case MovementClass.Inbound:
                if (hasAnyFrom)
                    return Fail($"{name} is inbound: fromWarehouseId, fromLocationId and fromStockStatus must be omitted.");
                if (toWh is null || toLoc is null)
                    return Fail($"{name} is inbound: toWarehouseId and toLocationId are required.");
                return Ok(new MovementDirection(null, null, null, toWh, toLoc, toStockStatus ?? StockStatus.Available));

            case MovementClass.Outbound:
                if (hasAnyTo)
                    return Fail($"{name} is outbound: toWarehouseId, toLocationId and toStockStatus must be omitted.");
                if (fromWh is null || fromLoc is null)
                    return Fail($"{name} is outbound: fromWarehouseId and fromLocationId are required.");
                return Ok(new MovementDirection(fromWh, fromLoc, fromStockStatus ?? StockStatus.Available, null, null, null));

            case MovementClass.WarehouseTransfer:
            {
                if (fromWh is null || fromLoc is null || toWh is null || toLoc is null)
                    return Fail($"{name} requires fromWarehouseId, fromLocationId, toWarehouseId and toLocationId.");
                if (string.Equals(fromWh, toWh, StringComparison.Ordinal))
                    return Fail($"{name} requires toWarehouseId to differ from fromWarehouseId.");
                var status = fromStockStatus ?? StockStatus.Available;
                if (toStockStatus.HasValue && toStockStatus.Value != status)
                    return Fail($"{name} is single-step: toStockStatus must equal fromStockStatus (use STATUS_TRANSFER to change status).");
                return Ok(new MovementDirection(fromWh, fromLoc, status, toWh, toLoc, status));
            }

            case MovementClass.LocationTransfer:
            {
                if (fromWh is null || fromLoc is null || toWh is null || toLoc is null)
                    return Fail($"{name} requires fromWarehouseId, fromLocationId, toWarehouseId and toLocationId.");
                if (!string.Equals(fromWh, toWh, StringComparison.Ordinal))
                    return Fail($"{name} requires the same warehouse on both sides (use WAREHOUSE_TRANSFER).");
                if (string.Equals(fromLoc, toLoc, StringComparison.Ordinal))
                    return Fail($"{name} requires toLocationId to differ from fromLocationId.");
                var status = fromStockStatus ?? StockStatus.Available;
                if (toStockStatus.HasValue && toStockStatus.Value != status)
                    return Fail($"{name} is single-step: toStockStatus must equal fromStockStatus (use STATUS_TRANSFER to change status).");
                return Ok(new MovementDirection(fromWh, fromLoc, status, toWh, toLoc, status));
            }

            case MovementClass.StatusTransfer:
            {
                if (fromWh is null || fromLoc is null || toWh is null || toLoc is null)
                    return Fail($"{name} requires fromWarehouseId, fromLocationId, toWarehouseId and toLocationId.");
                if (!string.Equals(fromWh, toWh, StringComparison.Ordinal) || !string.Equals(fromLoc, toLoc, StringComparison.Ordinal))
                    return Fail($"{name} keeps stock in place: toWarehouseId/toLocationId must equal fromWarehouseId/fromLocationId.");
                var fromStatus = fromStockStatus ?? StockStatus.Available;
                var toStatus = toStockStatus ?? StockStatus.Available;
                if (fromStatus == toStatus)
                    return Fail($"{name} requires fromStockStatus to differ from toStockStatus.");
                return Ok(new MovementDirection(fromWh, fromLoc, fromStatus, toWh, toLoc, toStatus));
            }

            case MovementClass.Reversal:
                if (hasAnyFrom || hasAnyTo)
                    return Fail($"{name} derives both sides from the original transaction: from*/to* fields must be omitted.");
                return (null, null);

            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /// <summary>The inverse of an original transaction's direction (ASSUMPTION-0173-05).</summary>
    public static MovementDirection InverseOf(MovementDirection original) => new(
        original.ToWarehouseId,
        original.ToLocationId,
        original.ToStockStatus,
        original.FromWarehouseId,
        original.FromLocationId,
        original.FromStockStatus);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static (MovementDirection?, string?) Ok(MovementDirection direction) => (direction, null);

    private static (MovementDirection?, string?) Fail(string error) => (null, error);
}
