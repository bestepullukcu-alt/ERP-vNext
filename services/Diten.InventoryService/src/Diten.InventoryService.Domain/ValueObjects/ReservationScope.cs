using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.ValueObjects;

/// <summary>
/// MOD-0173-FU03 §4.1 — the scope of a reservation inside its SKU, derived from its fields: a lot → <see cref="ReservationScopeLevel.Lot"/>
/// (it always names its warehouse), only a warehouse → <see cref="ReservationScopeLevel.Warehouse"/>, neither → <see cref="ReservationScopeLevel.Sku"/>
/// (the 1.5.0 reservation).
/// </summary>
public sealed record ReservationScope(string? WarehouseId, Guid? LotId)
{
    public static readonly ReservationScope Sku = new(null, null);

    public ReservationScopeLevel Level => LotId.HasValue
        ? ReservationScopeLevel.Lot
        : WarehouseId is not null ? ReservationScopeLevel.Warehouse : ReservationScopeLevel.Sku;

    public static ReservationScope Of(InventoryReservation reservation) => new(reservation.WarehouseId, reservation.LotId);

    /// <summary>The wire / audit name of a level (<c>details.level</c>, <c>InventoryReservationAuditRecord.Level</c>).</summary>
    public static string ToWire(ReservationScopeLevel level) => level switch
    {
        ReservationScopeLevel.Sku => "SKU",
        ReservationScopeLevel.Warehouse => "WAREHOUSE",
        ReservationScopeLevel.Lot => "LOT",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}
