using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>releaseReservation (pack ASSUMPTION-0173-17) — the rules are <see cref="ReservationCommandService.ReleaseAsync"/>, shared with the port.</summary>
public sealed class ReleaseReservationHandler : IRequestHandler<ReleaseReservationCommand, Response<ReservationResponseDto>>
{
    private readonly ReservationCommandService _reservations;
    private readonly ITenantContext _tenantContext;

    public ReleaseReservationHandler(ReservationCommandService reservations, ITenantContext tenantContext)
    {
        _reservations = reservations;
        _tenantContext = tenantContext;
    }

    public Task<Response<ReservationResponseDto>> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
        => _reservations.ReleaseAsync(request.ReservationId, request.IfMatch, _tenantContext.UserId, cancellationToken);
}
