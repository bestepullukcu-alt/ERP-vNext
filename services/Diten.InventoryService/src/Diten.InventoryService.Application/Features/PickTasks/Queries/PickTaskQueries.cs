using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PickTasks.Queries;

/// <summary>WMS draft §11 <c>listPickTasks</c> — the raw query values (+ <c>outboundId</c>); the handler parses them.</summary>
public sealed record GetPickTaskListQuery(IReadOnlyList<string> Statuses, string? WarehouseId, string? OutboundId, string? AssigneeUserId, string? Search, string? OrderBy,
    string? Page, string? PageSize) : IRequest<Response<WarehouseTaskPageDto>>;

public sealed record GetPickTaskByIdQuery(Guid TaskId) : IRequest<Response<WarehouseTaskDto>>;

public sealed record GetPickTaskEventListQuery(Guid TaskId, string? Page, string? PageSize) : IRequest<Response<WarehouseTaskEventPageDto>>;

/// <summary>
/// The pick reads (WMS draft §11): <c>kind: Pick</c> tasks of the selected company only, each with its outbound's id / number / status (the
/// "Paketle" link — never the ship-to, so an operator without <c>warehouse.outbound-shipments.read</c> works end to end, Q-10a).
/// </summary>
public sealed class PickTaskQueryHandlers :
    IRequestHandler<GetPickTaskListQuery, Response<WarehouseTaskPageDto>>,
    IRequestHandler<GetPickTaskByIdQuery, Response<WarehouseTaskDto>>,
    IRequestHandler<GetPickTaskEventListQuery, Response<WarehouseTaskEventPageDto>>
{
    private readonly IWarehouseTaskRepository _tasks;
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly PutawayTaskReads _reads;

    public PickTaskQueryHandlers(IWarehouseTaskRepository tasks, IOutboundShipmentRepository outbounds, PutawayTaskReads reads)
    {
        _tasks = tasks;
        _outbounds = outbounds;
        _reads = reads;
    }

    public async Task<Response<WarehouseTaskPageDto>> Handle(GetPickTaskListQuery request, CancellationToken ct)
    {
        var statuses = new List<WarehouseTaskStatus>();
        foreach (var raw in request.Statuses)
        {
            if (!Enum.TryParse<WarehouseTaskStatus>(raw, false, out var status) || !Enum.IsDefined(status) || raw != status.ToString())
                return WarehouseSupport.Invalid<WarehouseTaskPageDto>("status must be Open, InProgress, Completed or Cancelled.");
            statuses.Add(status);
        }

        Guid? warehouse = null, outbound = null, assignee = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var id)) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("warehouseId must be a UUID.");
            warehouse = id;
        }

        if (request.OutboundId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.OutboundId, out var id)) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("outboundId must be a UUID.");
            outbound = id;
        }

        if (request.AssigneeUserId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.AssigneeUserId, out var id)) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("assigneeUserId must be a UUID.");
            assignee = id;
        }

        if (request.OrderBy is not (null or "createdAt" or "-createdAt")) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("orderBy must be createdAt or -createdAt.");
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging) return WarehouseSupport.Invalid<WarehouseTaskPageDto>("page must be ≥ 1 and pageSize 1-200.");

        var page = await _tasks.ListAsync(new WarehouseTaskQuery(statuses, warehouse, assignee, null, WarehouseSupport.Trimmed(request.Search), request.OrderBy == "createdAt",
            paging.Page, paging.PageSize, WarehouseTaskKind.Pick, outbound), ct);
        var statusOf = await StatusesAsync(page.Items.Select(t => t.OutboundId).OfType<Guid>().Distinct().ToList(), ct);
        return Response<WarehouseTaskPageDto>.Success(new WarehouseTaskPageDto(page.Items.Select(t => WithOutbound(_reads.Dto(t), statusOf)).ToList(), page.Total,
            paging.Page, paging.PageSize));
    }

    public async Task<Response<WarehouseTaskDto>> Handle(GetPickTaskByIdQuery request, CancellationToken ct)
    {
        var task = await _tasks.FindOfKindAsync(request.TaskId, WarehouseTaskKind.Pick, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        var statusOf = await StatusesAsync(task.OutboundId is { } o ? [o] : [], ct);
        return Response<WarehouseTaskDto>.Success(WithOutbound(_reads.Dto(task), statusOf));
    }

    public async Task<Response<WarehouseTaskEventPageDto>> Handle(GetPickTaskEventListQuery request, CancellationToken ct)
    {
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging)
            return WarehouseSupport.Invalid<WarehouseTaskEventPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (await _tasks.FindOfKindAsync(request.TaskId, WarehouseTaskKind.Pick, ct) is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskEventPageDto>();
        var page = await _tasks.EventsAsync(request.TaskId, paging.Page, paging.PageSize, ct);
        return Response<WarehouseTaskEventPageDto>.Success(new WarehouseTaskEventPageDto(
            page.Items.Select(e => new WarehouseTaskEventDto(e.Seq, e.Kind.ToString(),
                e.ActorUserId is { } actor ? new TaskPersonDto(actor.ToString("D"), e.ActorName) : null, e.System, PutawayTaskSupport.Wire(e.AtUtc),
                e.Data.Where(d => d.Value is not null).ToDictionary(d => d.Key, d => d.Value), e.CorrelationId.ToString("D"))).ToList(),
            page.Total, paging.Page, paging.PageSize));
    }

    private async Task<IReadOnlyDictionary<string, string>> StatusesAsync(IReadOnlyList<Guid> outboundIds, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in outboundIds)
        {
            if (await _outbounds.FindAsync(id, ct) is { } o) map[id.ToString("D")] = o.Status.ToString();
        }

        return map;
    }

    private static WarehouseTaskDto WithOutbound(WarehouseTaskDto dto, IReadOnlyDictionary<string, string> statusOf)
        => dto.Outbound is { } o ? dto with { Outbound = o with { Status = statusOf.GetValueOrDefault(o.OutboundId) } } : dto;
}
