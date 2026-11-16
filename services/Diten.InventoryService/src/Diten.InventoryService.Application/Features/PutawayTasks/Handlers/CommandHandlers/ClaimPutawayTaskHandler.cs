using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>claimPutawayTask</c> (pack §8.13, M-5; AC-47): an operator with <c>warehouse.putaway-tasks.confirm</c> assigns an UNASSIGNED open task to
/// himself or herself; an assigned one is 409 WMS_TASK_ALREADY_ASSIGNED (re-assignment is the supervisor's update). Locked by a Pending.
/// </summary>
public sealed class ClaimPutawayTaskHandler : PutawayCommandHandler, IRequestHandler<ClaimPutawayTaskCommand, Response<WarehouseTaskDto>>
{
    public ClaimPutawayTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ClaimPutawayTaskCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpClaimPutawayTask), ("taskId", request.TaskId.ToString("D")));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpClaimPutawayTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (task.AssigneeUserId is not null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.TaskAlreadyAssigned,
                "The task is already assigned; a supervisor re-assigns it.", 409, Details(task));
        }

        var now = Support.Now();
        var copy = task.Copy();
        copy.AssigneeUserId = Support.UserId;
        copy.AssigneeName = Support.ActorName;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var claimed = Support.Event(copy, WarehouseTaskEventKind.Claimed, now,
            new Dictionary<string, string?> { ["before.assigneeUserId"] = null, ["after.assigneeUserId"] = Support.UserId.ToString("D") },
            request.IdempotencyKey, WarehouseContract.OpClaimPutawayTask, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [claimed], WarehouseContract.OpClaimPutawayTask, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? Conflict(await Repository.FindAsync(task.Id, ct));
    }
}
