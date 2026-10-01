using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Commands;

/// <summary>POST /api/inventory/reservations/{id}/release (contract <c>releaseReservation</c>); <c>If-Match</c> required.</summary>
public sealed record ReleaseReservationCommand(string? ReservationId, string? IfMatch) : IRequest<Response<ReservationResponseDto>>;
