namespace Diten.InventoryService.Domain.Enums;

/// <summary>MOD-0173-FU03 §4.4 — the refusal actions of <c>InventoryReservationAuditRecord</c> (rows written after the refused request).</summary>
public static class ReservationAuditActions
{
    /// <summary>A refused create (createReservation, the port's CreateAsync / CreateBatchAsync).</summary>
    public const string Refused = "InventoryReservationRefused";

    /// <summary>An issue refused by a reservation scope guard (422 INSUFFICIENT_STOCK with <c>details.level</c>).</summary>
    public const string ConsumptionRefused = "InventoryReservationConsumptionRefused";

    /// <summary>§8.3.1: a REVERSAL with <c>restoreReservation: true</c> gave a consumed quantity back (success row, written in the transaction).</summary>
    public const string Restored = "InventoryReservationRestored";
}
