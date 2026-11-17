using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary>§4.1 <c>listCycleCountEvents</c> — <paramref name="ReadsVariances"/> comes from the caller's token (pack §4.3, r5 SB1/F-6).</summary>
public sealed record GetCycleCountEventListQuery(Guid CountId, string? Page, string? PageSize, bool ReadsVariances) : IRequest<Response<CycleCountEventPageDto>>;
