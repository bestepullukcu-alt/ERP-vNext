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
/// <c>closePutawayTaskShort</c> (pack §8.3 row 27; AC-11, AC-43, AC-52, AC-55, AC-68): Completed with the remainder left, only with at least one
/// Posted confirmation (else 409 WMS_INVALID_TRANSITION — cancel instead), never for a serial SKU (422), by the assignee or a supervisor (403),
/// with a reason. The ledger is asked for every Refused confirmation first (row 20). No <c>Completed</c> row is written.
/// </summary>
public sealed class ClosePutawayTaskShortHandler : PutawayCommandHandler, IRequestHandler<ClosePutawayTaskShortCommand, Response<WarehouseTaskDto>>
{
    public ClosePutawayTaskShortHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ClosePutawayTaskShortCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpClosePutawayTaskShort), ("taskId", request.TaskId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpClosePutawayTaskShort, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (task.SerialControlled)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SerialPartialNotSupported,
                "A serial-controlled task is put away whole; it is not closed short.", 422, Details(task));
        }

        if (!PutawayTaskRules.HasPosted(task))
            return PutawayTaskSupport.InvalidTransition<WarehouseTaskDto>("A task without a Posted confirmation is cancelled, not closed short.");

        if (await PreCloseAsync(task, task.Confirmations.Where(c => c.Status == ConfirmationStatus.Refused).Select(c => c.Seq), ct) is { } diverged) return diverged;

        var now = Support.Now();
        var copy = task.Copy();
        copy.Status = WarehouseTaskStatus.Completed;
        copy.ShortCloseReason = reason;
        copy.OpenSerialSlot = null;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var closed = Support.Event(copy, WarehouseTaskEventKind.ClosedShort, now,
            new Dictionary<string, string?> { ["planned"] = PutawayTaskSupport.Q(copy.PlannedQuantity), ["confirmed"] = PutawayTaskSupport.Q(copy.ConfirmedQuantity),
                ["shortCloseReason"] = reason },
            request.IdempotencyKey, WarehouseContract.OpClosePutawayTaskShort, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [closed], WarehouseContract.OpClosePutawayTaskShort, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        if (twin is not null) return twin;
        var current = await Repository.FindAsync(task.Id, ct);
        return current is not null && PutawayTaskRules.HasIntegrityError(current) ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(current)) : Conflict(current);
    }
}
