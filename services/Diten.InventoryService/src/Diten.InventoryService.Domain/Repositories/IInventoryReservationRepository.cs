using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// Reservation persistence (MOD-0173 S2), scoped to the caller's Tenant + LE like the ledger repository. Every write
/// runs in one Mongo transaction together with the <c>inventory_sku_positions</c> projection, the audit record and the
/// Pending outbox event, and first expires the SKU's due reservations (pack ASSUMPTION-0173-16). MOD-0173-FU03: a reservation may be
/// limited to a warehouse or a warehouse + lot; the guard and the projection then run on every level that contains it (§8.2).
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

    // ── MOD-0173-FU03 DL-1 ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>§8.7: every line in ONE transaction (all or nothing); the SKUs' due reservations are expired first.</summary>
    Task<ReservationBatchResult> CreateBatchAsync(IReadOnlyList<ReservationCreation> creations, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary><c>getReservation</c>: the reservation of this Tenant + LE, or null (unknown and other scope alike).</summary>
    Task<InventoryReservation?> FindByIdAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>§8.7 <c>FindByIdempotencyKeysAsync</c>: the reservations of this Tenant + LE created with these keys, any status; committed state, outside any transaction.</summary>
    Task<IReadOnlyList<InventoryReservation>> FindByIdempotencyKeysAsync(IReadOnlyCollection<string> idempotencyKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// §8.8 <c>scopeReserved</c>: Σ remaining quantity of the ACTIVE, not-yet-due reservations that contain the scope (warehouse
    /// <paramref name="warehouseId"/>; with <paramref name="lotId"/> also that lot) — the vade filter of <see cref="SumActiveReservedAsync"/>.
    /// </summary>
    Task<decimal> SumActiveScopeReservedAsync(SkuKey sku, string warehouseId, Guid? lotId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>§4.2: <c>AvailableOnHand</c> of the warehouse (or warehouse + lot) position; 0 when the scope has none.</summary>
    Task<decimal> ScopeAvailableOnHandAsync(SkuKey sku, string warehouseId, Guid? lotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// §4.4 / §21: one refusal row in <c>inventory_audit</c>, after the refused request (no transaction). Best effort, never silent: a
    /// failure is logged as a warning and counted (<c>inventory_refusal_audit_failed_total</c>); the answer of the request is unchanged.
    /// </summary>
    Task WriteRefusalAsync(InventoryReservationAuditRecord refusal, CancellationToken cancellationToken = default);
}
