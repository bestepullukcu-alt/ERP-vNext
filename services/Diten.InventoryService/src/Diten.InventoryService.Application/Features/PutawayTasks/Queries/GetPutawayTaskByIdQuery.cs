using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Queries;

/// <summary>WMS draft §4 <c>getPutawayTask</c>.</summary>
public sealed record GetPutawayTaskByIdQuery(Guid TaskId) : IRequest<Response<WarehouseTaskDto>>;
