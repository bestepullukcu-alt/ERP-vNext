using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// Reservation persistence (MOD-0173 S2), scoped to the caller's Tenant + LE like the ledger repository. Every write
/// runs in one Mongo transaction together with the <c>inventory_sku_positions</c> projection, the audit record and the
/// Pending outbox event, and first expires the SKU's due reservations (pack ASSUMPTION-0173-16).
/// </summary>
public interface IInventoryReservationRepository
{
    Task<InventoryReservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<ReservationCreateResult> CreateAsync(ReservationCreation creation, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<ReservationReleaseResult> ReleaseAsync(
        Guid reservationId,
        int expectedVersion,
        Guid correlationId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Σ remaining quantity of ACTIVE, not-yet-due reservations of the matching SKUs (read path, ASSUMPTION-0173-12).</summary>
    Task<decimal> SumActiveReservedAsync(AvailabilityFilter filter, DateTimeOffset now, CancellationToken cancellationToken = default);
}
