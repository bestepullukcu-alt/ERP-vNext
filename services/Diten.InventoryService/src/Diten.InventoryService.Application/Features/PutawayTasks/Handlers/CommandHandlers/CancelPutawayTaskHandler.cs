using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>cancelPutawayTask</c> (pack §8.3 row 27, M-4; AC-11, AC-55, AC-68): only while no confirmation is Posted (else 409
/// WMS_INVALID_TRANSITION — the remainder is closed short instead), no Pending, no IntegrityError, with a reason 10–1000. Before writing, the
/// ledger is asked for EVERY Refused confirmation of the task (whatever its Resolution — 07 F-2): a late commit → 409, nothing of the cancel.
/// Posted movements are never undone here (a REVERSAL is MOD-0173's).
/// </summary>
public sealed class CancelPutawayTaskHandler : PutawayCommandHandler, IRequestHandler<CancelPutawayTaskCommand, Response<WarehouseTaskDto>>
{
    public CancelPutawayTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(CancelPutawayTaskCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpCancelPutawayTask), ("taskId", request.TaskId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpCancelPutawayTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (PutawayTaskRules.HasPosted(task))
            return PutawayTaskSupport.InvalidTransition<WarehouseTaskDto>("A task with a Posted confirmation is not cancelled; close the remainder short.");

        if (await PreCloseAsync(task, task.Confirmations.Where(c => c.Status == ConfirmationStatus.Refused).Select(c => c.Seq), ct) is { } diverged) return diverged;

        var now = Support.Now();
        var copy = task.Copy();
        var before = copy.Status;
        copy.Status = WarehouseTaskStatus.Cancelled;
        copy.CancelReason = reason;
        copy.OpenSerialSlot = null;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var cancelled = Support.Event(copy, WarehouseTaskEventKind.Cancelled, now,
            new Dictionary<string, string?> { ["beforeStatus"] = before.ToString(), ["cancelReason"] = reason, ["planned"] = PutawayTaskSupport.Q(copy.PlannedQuantity),
                ["confirmed"] = PutawayTaskSupport.Q(copy.ConfirmedQuantity) },
            request.IdempotencyKey, WarehouseContract.OpCancelPutawayTask, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [cancelled], WarehouseContract.OpCancelPutawayTask, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? await LostAsync(task.Id, ct);
    }

    private async Task<Response<WarehouseTaskDto>> LostAsync(Guid taskId, CancellationToken ct)
    {
        var current = await Repository.FindAsync(taskId, ct);
        return current is not null && PutawayTaskRules.HasIntegrityError(current) ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(current)) : Conflict(current);
    }
}
