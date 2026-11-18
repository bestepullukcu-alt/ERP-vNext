using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary>§4.1 <c>listCycleCounts</c> — the raw query parameters (parsed by the handler; a bad one is 400).</summary>
public sealed record GetCycleCountListQuery(IReadOnlyList<string> Status, string? WarehouseId, string? Q, string? Page, string? PageSize)
    : IRequest<Response<CycleCountPageDto>>;
