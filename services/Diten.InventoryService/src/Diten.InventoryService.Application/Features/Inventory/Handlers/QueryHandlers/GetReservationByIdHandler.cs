using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>
/// getReservation (INVENTORY 1.6.0, MOD-0173-FU03 §8.8): the reservation of the caller's Tenant + LE as it is now, its <c>etag</c> the
/// current version (the one a release needs). An id that is not a UUID, unknown, or of another tenant / company → 404 in the SAME shape as
/// releaseReservation's 404 (no code — nothing about the id leaks).
/// </summary>
public sealed class GetReservationByIdHandler : IRequestHandler<GetReservationByIdQuery, Response<ReservationViewDto>>
{
    private readonly IInventoryReservationRepository _reservations;

    public GetReservationByIdHandler(IInventoryReservationRepository reservations) => _reservations = reservations;

    public async Task<Response<ReservationViewDto>> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        if (!WireParsing.TryParseUuid(request.ReservationId, out var id)
            || await _reservations.FindByIdAsync(id, cancellationToken) is not { } reservation)
        {
            return ReservationCommandService.NotFound<ReservationViewDto>();
        }

        return Response<ReservationViewDto>.Success(InventoryMapping.ToReservationView(reservation));
    }
}
