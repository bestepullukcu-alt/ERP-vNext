using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary>§4.1 <c>getCycleCount</c>.</summary>
public sealed record GetCycleCountByIdQuery(Guid CountId) : IRequest<Response<CycleCountDto>>;
