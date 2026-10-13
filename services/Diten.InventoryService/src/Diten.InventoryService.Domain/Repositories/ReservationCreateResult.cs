using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

public sealed record ReservationCreateResult(
    ReservationCreateOutcome Outcome,
    InventoryReservation? Reservation = null,
    decimal? Available = null,
    decimal? Requested = null);
