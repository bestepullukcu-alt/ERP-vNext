using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Queries;

/// <summary>WMS draft §4 <c>getPutawayTaskEvents</c> — the timeline, oldest first.</summary>
public sealed record GetPutawayTaskEventListQuery(Guid TaskId, string? Page, string? PageSize) : IRequest<Response<WarehouseTaskEventPageDto>>;
