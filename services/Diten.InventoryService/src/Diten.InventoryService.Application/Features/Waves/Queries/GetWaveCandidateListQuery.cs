using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>listWaveCandidates</c> — the planner's left list.</summary>
public sealed record GetWaveCandidateListQuery(string? WarehouseId, string? Q, string? ShipTo, string? Page, string? PageSize)
    : IRequest<Response<WaveCandidatePageDto>>;
