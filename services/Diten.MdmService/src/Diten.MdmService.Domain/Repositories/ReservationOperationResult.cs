using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public sealed record ReservationOperationResult(
    bool Succeeded,
    CodeReservation? Reservation,
    string? ErrorCode = null);
