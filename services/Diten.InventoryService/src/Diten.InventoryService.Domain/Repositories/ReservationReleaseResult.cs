using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

public sealed record ReservationReleaseResult(
    ReservationReleaseOutcome Outcome,
    InventoryReservation? Reservation = null,
    decimal? AvailableAfter = null);
