using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Commands;

/// <summary>POST /api/locations/{id}/status (v1.1 <c>changeLocationStatus</c>, <c>If-Match</c> required; K-04…K-06).</summary>
public sealed record ChangeLocationStatusCommand(string? LocationId, string? IfMatch, string? Status)
    : IRequest<Response<LocationDetailDto>>;
