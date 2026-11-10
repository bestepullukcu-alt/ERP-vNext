using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>
/// MOD-0173-FU03 §8.7 — the in-process reservation port: MOD-0178's ONE door to INVENTORY reservations (it never writes the ledger,
/// balance or position collections itself). Same parser, fingerprint, idempotency, guards, lot / warehouse rules and audit as the HTTP
/// operations. No controller uses it and it checks no HTTP permission — the calling command carries its own (architecture-tested, AC-16).
/// Every member works in the resolved Tenant + LE; a caller that names another company (or tenant) is a programming error:
/// <see cref="InvalidOperationException"/> before anything is read or written, no audit row.
/// </summary>
public interface IInventoryReservationPort
{
    /// <summary>createReservation for the record of <paramref name="legalEntityId"/> (MOD-0178: the outbound's company).</summary>
    Task<Response<ReservationResponseDto>> CreateAsync(
        CreateReservationCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>1…100 lines, all or nothing in one transaction; per-line results (§8.7, Q-06).</summary>
    Task<ReservationBatchResponse> CreateBatchAsync(
        IReadOnlyList<CreateReservationCommand> lines, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>releaseReservation (If-Match, 409 RESERVATION_CONFLICT, 404).</summary>
    Task<Response<ReservationResponseDto>> ReleaseAsync(
        Guid reservationId, string? ifMatch, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>getReservation's view; null when the id is not a reservation of this Tenant + LE.</summary>
    Task<ReservationViewDto?> GetAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The scope's free quantity for an allocation proposal: the warehouse (or warehouse + lot) position's <c>AvailableOnHand</c> − the
    /// ACTIVE, not-yet-due reservations containing the scope (the same read as the 1.6.0 scope figures). Unfloored — may be negative.
    /// </summary>
    Task<decimal> GetScopeFreeAsync(
        Guid itemId, Guid skuId, SkuLevel skuLevel, string warehouseId, Guid? lotId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reservations CREATED with these idempotency keys (any status; at most one per key; an unknown key is absent) — MOD-0178's
    /// allocation recovery (FU-0178-RSV-BYKEY). <paramref name="tenantId"/> / <paramref name="legalEntityId"/> must be the resolved
    /// scope (each checked on its own, fail-closed); 1…100 keys, none blank, none repeated (<see cref="ArgumentException"/>). A read:
    /// no audit row, no transaction, never the ledger's session — the committed state.
    /// </summary>
    Task<IReadOnlyList<ReservationKeyMatch>> FindByIdempotencyKeysAsync(
        Guid tenantId, Guid legalEntityId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
