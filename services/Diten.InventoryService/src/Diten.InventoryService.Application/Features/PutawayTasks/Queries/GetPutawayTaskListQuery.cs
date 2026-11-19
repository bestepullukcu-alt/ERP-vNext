using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Queries;

/// <summary>WMS draft §4 <c>listPutawayTasks</c> — the raw query values; the handler parses them (400 on a malformed one).</summary>
public sealed record GetPutawayTaskListQuery(IReadOnlyList<string> Statuses, string? WarehouseId, string? AssigneeUserId, string? Priority, string? Search, string? OrderBy, string? Page, string? PageSize) : IRequest<Response<WarehouseTaskPageDto>>;
