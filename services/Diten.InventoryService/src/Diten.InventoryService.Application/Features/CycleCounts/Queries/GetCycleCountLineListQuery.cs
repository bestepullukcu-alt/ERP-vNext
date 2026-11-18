using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary>§4.1 <c>listCycleCountLines</c> — <paramref name="ReadsVariances"/> comes from the caller's token (pack §8.2 rule 5).</summary>
public sealed record GetCycleCountLineListQuery(Guid CountId, string? Page, string? PageSize, bool ReadsVariances) : IRequest<Response<CycleCountLinePageDto>>;
