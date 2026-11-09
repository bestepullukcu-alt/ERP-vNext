using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Queries;

/// <summary>
/// GET /api/inventory/reservations/{id} (INVENTORY 1.6.0 <c>getReservation</c>, MOD-0173-FU03 §8.8): the reservation of the caller's
/// Tenant + LE with its current ETag. A query (AUD-001 §2): it writes no audit row.
/// </summary>
public sealed record GetReservationByIdQuery(string? ReservationId) : IRequest<Response<ReservationViewDto>>;
