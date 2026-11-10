using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary>§4.1 <c>listCycleCountEvents</c>.</summary>
public sealed record GetCycleCountEventListQuery(Guid CountId, string? Page, string? PageSize) : IRequest<Response<CycleCountEventPageDto>>;
