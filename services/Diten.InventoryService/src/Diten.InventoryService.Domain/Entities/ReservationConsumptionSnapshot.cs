using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// MOD-0173-FU03 §8.3 (INVENTORY 1.7.0 <c>MovementResponse.reservationConsumption</c>): what a movement that named a reservation did to it
/// inside its posting transaction — stored on the transaction so an idempotent replay answers the same (the 1.3.0 <c>ExpiryChecks</c>
/// pattern). A LOCATION_TRANSFER validates the reservation and consumes nothing (<see cref="ConsumedQuantity"/> 0).
/// </summary>
public sealed class ReservationConsumptionSnapshot
{
    public Guid ReservationId { get; set; }

    public decimal ConsumedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public ReservationStatus Status { get; set; }

    /// <summary><see cref="ReservationClosedReasons"/> once the reservation is closed; null while ACTIVE.</summary>
    public string? ClosedReason { get; set; }
}
