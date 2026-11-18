using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Queries;

/// <summary>WMS draft §4 <c>listPutawayCandidates</c> (pack §8.2) — the stock to put away, computed at read time.</summary>
public sealed record GetPutawayCandidateListQuery(string? WarehouseId, string? SkuId, string? LotId, string? Eligible, string? Search, string? Page, string? PageSize) : IRequest<Response<PutawayCandidatePageDto>>;
