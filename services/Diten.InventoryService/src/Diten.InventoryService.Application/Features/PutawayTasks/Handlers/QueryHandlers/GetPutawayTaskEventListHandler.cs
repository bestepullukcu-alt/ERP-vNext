using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Queries;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.QueryHandlers;

/// <summary>
/// <c>getPutawayTaskEvents</c> — the task's insert-only timeline, oldest first (WMS draft §3.4). The request key, operation and fingerprint of a key
/// event stay in the store; they never reach the wire.
/// </summary>
public sealed class GetPutawayTaskEventListHandler : IRequestHandler<GetPutawayTaskEventListQuery, Response<WarehouseTaskEventPageDto>>
{
    private readonly IWarehouseTaskRepository _tasks;

    public GetPutawayTaskEventListHandler(IWarehouseTaskRepository tasks) => _tasks = tasks;

    public async Task<Response<WarehouseTaskEventPageDto>> Handle(GetPutawayTaskEventListQuery request, CancellationToken ct)
    {
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging)
            return WarehouseSupport.Invalid<WarehouseTaskEventPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (await _tasks.FindAsync(request.TaskId, ct) is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskEventPageDto>();

        var page = await _tasks.EventsAsync(request.TaskId, paging.Page, paging.PageSize, ct);
        return Response<WarehouseTaskEventPageDto>.Success(new WarehouseTaskEventPageDto(
            page.Items.Select(e => new WarehouseTaskEventDto(e.Seq, e.Kind.ToString(),
                e.ActorUserId is { } actor ? new TaskPersonDto(actor.ToString("D"), e.ActorName) : null, e.System, PutawayTaskSupport.Wire(e.AtUtc),
                e.Data.Where(d => d.Value is not null).ToDictionary(d => d.Key, d => d.Value), e.CorrelationId.ToString("D"))).ToList(),
            page.Total, paging.Page, paging.PageSize));
    }
}
