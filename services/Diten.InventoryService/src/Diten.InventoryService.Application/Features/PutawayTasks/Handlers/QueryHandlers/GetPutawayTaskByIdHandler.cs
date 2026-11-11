using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Queries;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.QueryHandlers;

/// <summary><c>getPutawayTask</c> — a task of the selected company; another company's or tenant's id is 404 (F-11).</summary>
public sealed class GetPutawayTaskByIdHandler : IRequestHandler<GetPutawayTaskByIdQuery, Response<WarehouseTaskDto>>
{
    private readonly IWarehouseTaskRepository _tasks;
    private readonly PutawayTaskReads _reads;

    public GetPutawayTaskByIdHandler(IWarehouseTaskRepository tasks, PutawayTaskReads reads)
    {
        _tasks = tasks;
        _reads = reads;
    }

    public async Task<Response<WarehouseTaskDto>> Handle(GetPutawayTaskByIdQuery request, CancellationToken ct)
        => await _tasks.FindAsync(request.TaskId, ct) is { } task
            ? Response<WarehouseTaskDto>.Success(_reads.Dto(task))
            : PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
}
