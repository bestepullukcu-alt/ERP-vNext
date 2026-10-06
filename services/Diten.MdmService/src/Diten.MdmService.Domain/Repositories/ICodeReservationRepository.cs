using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface ICodeReservationRepository
{
    Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CodeReservation> ReserveAsync(
        CodeBearingEntityType entityType,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ReservationOperationResult> ConsumeForIdentityAsync(
        Guid reservationId,
        CodeBearingEntityType expectedEntityType,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
        Guid reservationId,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default);

    // LSKU FIX1 B1 (b): a code consumed for an identity whose row was never written is burned — it is never handed out
    // again, and the reason is kept on the reservation.
    Task<ReservationOperationResult> BurnIdentityBindingAsync(
        Guid reservationId,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        string reason,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("CODE_RESERVATION_BURN_NOT_IMPLEMENTED");
}
