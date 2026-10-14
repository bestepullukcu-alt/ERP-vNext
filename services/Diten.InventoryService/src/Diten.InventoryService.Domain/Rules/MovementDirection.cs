using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Normalised source / target of a movement after the pack §12 direction rules were applied: forbidden sides are
/// null, omitted statuses carry their default.
/// </summary>
public sealed record MovementDirection(
    string? FromWarehouseId,
    string? FromLocationId,
    StockStatus? FromStockStatus,
    string? ToWarehouseId,
    string? ToLocationId,
    StockStatus? ToStockStatus)
{
    public bool HasSource => FromLocationId is not null;

    public bool HasTarget => ToLocationId is not null;
}
