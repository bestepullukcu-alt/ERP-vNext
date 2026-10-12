using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>
/// releaseReservation (pack ASSUMPTION-0173-17). Order of refusals: missing <c>If-Match</c> → 400 (validator);
/// unknown or other-scope id → 404; stale ETag, or a reservation that is no longer ACTIVE (released, consumed,
/// expired — due ones are expired first in the same transaction) → 409 RESERVATION_CONFLICT. An ETag that is not of
/// the form <c>W/"n"</c> can never match, so it is a 409 too (after the 404 check).
/// </summary>
public sealed partial class ReleaseReservationHandler : IRequestHandler<ReleaseReservationCommand, Response<ReservationResponseDto>>
{
    private readonly IInventoryReservationRepository _reservations;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;

    public ReleaseReservationHandler(
        IInventoryReservationRepository reservations,
        ITenantContext tenantContext,
        ICorrelationContext correlation)
    {
        _reservations = reservations;
        _tenantContext = tenantContext;
        _correlation = correlation;
    }

    public async Task<Response<ReservationResponseDto>> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IfMatch))
        {
            return Response<ReservationResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, "The If-Match header is required.", 400);
        }

        if (!WireParsing.TryParseUuid(request.ReservationId, out var reservationId))
        {
            return NotFound();
        }

        var match = WeakETag().Match(request.IfMatch.Trim());
        var expectedVersion = match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? v
            : -1;

        var result = await _reservations.ReleaseAsync(
            reservationId, expectedVersion, _correlation.CorrelationId, _tenantContext.UserId, DateTimeOffset.UtcNow, cancellationToken);

        return result.Outcome switch
        {
            ReservationReleaseOutcome.Released => Response<ReservationResponseDto>.Success(
                InventoryMapping.ToReleasedReservation(result.Reservation!, result.AvailableAfter ?? 0m), 200),
            ReservationReleaseOutcome.NotFound => NotFound(),
            ReservationReleaseOutcome.Conflict => Response<ReservationResponseDto>.Fail(InventoryErrorCodes.ReservationConflict,
                "The reservation changed or is no longer active; reload it and retry with its current ETag.", 409),
            _ => throw new InvalidOperationException($"Unhandled release outcome {result.Outcome}.")
        };
    }

    /// <summary>No declared code for an unknown reservation — the Error body carries none (ASSUMPTION-S2-07).</summary>
    private static Response<ReservationResponseDto> NotFound()
        => Response<ReservationResponseDto>.Fail("Reservation not found.", 404);

    [GeneratedRegex("^W/\"(\\d{1,9})\"$", RegexOptions.CultureInvariant)]
    private static partial Regex WeakETag();
}
