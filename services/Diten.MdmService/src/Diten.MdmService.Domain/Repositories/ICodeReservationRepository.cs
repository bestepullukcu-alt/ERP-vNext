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

}
