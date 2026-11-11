using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Queries;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.QueryHandlers;

/// <summary>
/// <c>listPutawayTasks</c> — the selected company's putaway tasks only (another company's or tenant's never shows); statuses by exact name,
/// warehouse / assignee as canonical UUIDs, priority by name, <c>q</c> on number / lot number, <c>orderBy</c> <c>createdAt</c> (oldest first) or
/// <c>-createdAt</c> (default), paging ≤ 200. A malformed value is 400 <c>INVALID_REQUEST</c>.
/// </summary>
public sealed class GetPutawayTaskListHandler : IRequestHandler<GetPutawayTaskListQuery, Response<WarehouseTaskPageDto>>
{
    private readonly IWarehouseTaskRepository _tasks;
    private readonly PutawayTaskReads _reads;

    public GetPutawayTaskListHandler(IWarehouseTaskRepository tasks, PutawayTaskReads reads)
    {
        _tasks = tasks;
        _reads = reads;
    }

    public async Task<Response<WarehouseTaskPageDto>> Handle(GetPutawayTaskListQuery request, CancellationToken ct)
    {
        var statuses = new List<WarehouseTaskStatus>();
        foreach (var raw in request.Statuses)
        {
            if (!Enum.TryParse<WarehouseTaskStatus>(raw, false, out var status) || !Enum.IsDefined(status) || raw != status.ToString())
                return WarehouseSupport.Invalid<WarehouseTaskPageDto>("status must be Open, InProgress, Completed or Cancelled.");
            statuses.Add(status);
        }

        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var id)) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("warehouseId must be a UUID.");
            warehouse = id;
        }

        Guid? assignee = null;
        if (request.AssigneeUserId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.AssigneeUserId, out var id)) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("assigneeUserId must be a UUID.");
            assignee = id;
        }

        WarehouseTaskPriority? priority = null;
        if (request.Priority is not null)
        {
            if (!Enum.TryParse<WarehouseTaskPriority>(request.Priority, false, out var p) || !Enum.IsDefined(p) || request.Priority != p.ToString())
                return WarehouseSupport.Invalid<WarehouseTaskPageDto>("priority must be Low, Normal or High.");
            priority = p;
        }

        if (request.OrderBy is not (null or "createdAt" or "-createdAt")) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("orderBy must be createdAt or -createdAt.");
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("page must be ≥ 1 and pageSize 1-200.");

        var page = await _tasks.ListAsync(new WarehouseTaskQuery(statuses, warehouse, assignee, priority, WarehouseSupport.Trimmed(request.Search),
            request.OrderBy == "createdAt", paging.Page, paging.PageSize), ct);
        return Response<WarehouseTaskPageDto>.Success(new WarehouseTaskPageDto(page.Items.Select(t => _reads.Dto(t)).ToList(), page.Total, paging.Page, paging.PageSize));
    }
}
