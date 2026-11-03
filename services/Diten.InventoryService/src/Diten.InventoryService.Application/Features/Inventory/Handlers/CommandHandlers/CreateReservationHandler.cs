using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>
/// createReservation (pack ASSUMPTION-0173-12..16; MOD-0173-FU03 scope). The rules live in <see cref="ReservationCommandService"/>, shared
/// with the in-process port so both answer the same; the actor is the caller. The structural rules are checked there too and not by a
/// pipeline validator: a 400 is a refusal that must leave its <c>InventoryReservationRefused</c> row (AUD-001 §3, FU03 AC-15), and a
/// validator answers before the handler is reached.
/// </summary>
public sealed class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Response<ReservationResponseDto>>
{
    private readonly ReservationCommandService _reservations;
    private readonly ITenantContext _tenantContext;

    public CreateReservationHandler(ReservationCommandService reservations, ITenantContext tenantContext)
    {
        _reservations = reservations;
        _tenantContext = tenantContext;
    }

    public Task<Response<ReservationResponseDto>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
        => _reservations.CreateAsync(request, _tenantContext.UserId, cancellationToken);
}
