using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>
/// createReservation (pack ASSUMPTION-0173-12..16). Idempotency first, exactly as postMovement: a key already committed
/// in this Tenant + LE answers from the durable record (same fingerprint → 200 with the original result; different →
/// 400 IDEMPOTENCY_KEY_REUSED). Otherwise one atomic create guarded by <c>AvailableOnHand − Reserved ≥ q</c> at SKU
/// level → 201, or 422 INSUFFICIENT_AVAILABLE with nothing written. No upstream call: an unknown SKU simply has no
/// available stock (ASSUMPTION-S2-03).
/// </summary>
public sealed class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Response<ReservationResponseDto>>
{
    private readonly IInventoryReservationRepository _reservations;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;

    public CreateReservationHandler(
        IInventoryReservationRepository reservations,
        ITenantContext tenantContext,
        ICorrelationContext correlation)
    {
        _reservations = reservations;
        _tenantContext = tenantContext;
        _correlation = correlation;
    }

    public async Task<Response<ReservationResponseDto>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var (parsed, errors) = ReservationRequestParser.Parse(request, now);
        if (parsed is null)
        {
            return Response<ReservationResponseDto>.Fail(errors[0].Code, errors.Select(e => e.Message).ToList(), 400);
        }

        var fingerprint = ReservationFingerprint.Compute(parsed);
        var committed = await _reservations.FindByIdempotencyKeyAsync(parsed.IdempotencyKey, cancellationToken);
        if (committed is not null)
        {
            return Replay(committed, fingerprint);
        }

        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            ItemId = parsed.ItemId,
            SkuId = parsed.SkuId,
            SkuLevel = parsed.SkuLevel,
            Quantity = parsed.Quantity,
            RemainingQuantity = parsed.Quantity,
            Status = ReservationStatus.Active,
            ReservationType = parsed.ReservationType,
            SourceModule = parsed.SourceModule,
            SourceDocumentId = parsed.SourceDocumentId,
            SourceLineId = parsed.SourceLineId,
            Priority = parsed.Priority,
            ExpiresAt = parsed.ExpiresAt,
            IdempotencyKey = parsed.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = _correlation.CorrelationId,
            CreatedBy = _tenantContext.UserId
        };

        var result = await _reservations.CreateAsync(new ReservationCreation(
            reservation,
            new InventoryReservationAuditRecord
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                Action = "InventoryReservationCreated",
                ReservationId = reservation.Id,
                ItemId = reservation.ItemId,
                SkuId = reservation.SkuId,
                SkuLevel = reservation.SkuLevel,
                ChangedQuantity = reservation.Quantity,
                RemainingQuantity = reservation.Quantity,
                Status = ReservationStatus.Active,
                ActorUserId = reservation.CreatedBy,
                CorrelationId = reservation.CorrelationId,
                LeSource = _tenantContext.LegalEntitySource,
                OccurredAt = now
            },
            new InventoryReservationOutboxMessage
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                AggregateId = reservation.Id,
                CorrelationId = reservation.CorrelationId,
                OccurredAt = now,
                Payload = new InventoryReservationChangedPayload
                {
                    ReservationId = reservation.Id,
                    Change = "Created",
                    ItemId = reservation.ItemId,
                    SkuId = reservation.SkuId,
                    SkuLevel = reservation.SkuLevel,
                    ChangedQuantity = reservation.Quantity,
                    RemainingQuantity = reservation.Quantity,
                    Status = ReservationStatus.Active,
                    SourceModule = reservation.SourceModule,
                    SourceDocumentId = reservation.SourceDocumentId,
                    SourceLineId = reservation.SourceLineId
                }
            }), now, cancellationToken);

        switch (result.Outcome)
        {
            case ReservationCreateOutcome.Created:
                return Response<ReservationResponseDto>.Success(InventoryMapping.ToCreatedReservation(result.Reservation!), 201);

            case ReservationCreateOutcome.InsufficientAvailable:
                return Response<ReservationResponseDto>.Fail(InventoryErrorCodes.InsufficientAvailable,
                    "Insufficient unreserved available stock for this SKU.", 422,
                    new Dictionary<string, object?>
                    {
                        ["available"] = InventoryQuantity.ToWire(result.Available ?? 0m),
                        ["requested"] = InventoryQuantity.ToWire(result.Requested ?? 0m)
                    });

            case ReservationCreateOutcome.IdempotencyKeyTaken:
            {
                var winner = await _reservations.FindByIdempotencyKeyAsync(parsed.IdempotencyKey, cancellationToken)
                    ?? throw new InvalidOperationException("Reservation idempotency key reported taken but no reservation carries it.");
                return Replay(winner, fingerprint);
            }

            default:
                throw new InvalidOperationException($"Unhandled reservation outcome {result.Outcome}.");
        }
    }

    private static Response<ReservationResponseDto> Replay(InventoryReservation committed, string fingerprint)
        => string.Equals(committed.RequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<ReservationResponseDto>.Success(InventoryMapping.ToCreatedReservation(committed), 200)
            : Response<ReservationResponseDto>.Fail(InventoryErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different reservation.", 400);
}
