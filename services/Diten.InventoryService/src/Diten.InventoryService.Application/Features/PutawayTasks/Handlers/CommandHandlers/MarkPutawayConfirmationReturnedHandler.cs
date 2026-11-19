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
/// <c>markPutawayConfirmationReturned</c> — "Geri taşındı" (pack §8.3 row 16, F-4; AC-55, AC-68, AC-70): the goods went back to the source, no
/// movement. Only an UNRESOLVED Refused confirmation (Posted / Pending / IntegrityError / resolved → 409 WMS_CONFIRMATION_NOT_RESOLVABLE), no
/// Pending on the task, the assignee or a supervisor, a reason 10–1000. The ledger is asked for THIS confirmation's key first (row 20). Open on
/// a terminal task too (the only action left there for a Refused confirmation).
/// </summary>
public sealed class MarkPutawayConfirmationReturnedHandler : PutawayCommandHandler, IRequestHandler<MarkPutawayConfirmationReturnedCommand, Response<WarehouseTaskDto>>
{
    public MarkPutawayConfirmationReturnedHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(MarkPutawayConfirmationReturnedCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpMarkPutawayConfirmationReturned), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpMarkPutawayConfirmationReturned, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        var c = task.Confirmations.SingleOrDefault(x => x.Seq == request.Seq);
        if (c is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();
        if (!PutawayTaskRules.IsUnresolvedRefused(c))
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotResolvable,
                "Only an unresolved refused confirmation is marked as returned.", 409, Details(task, c.Seq));
        }

        if (PutawayTaskRules.HasPending(task)) return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
            "A confirmation of this task is waiting for the stock answer; only \"Retry\" is open.", 409, Details(task));

        if (await PreCloseAsync(task, [c.Seq], ct) is { } diverged) return diverged;

        var now = Support.Now();
        var copy = task.Copy();
        var target = copy.Confirmations.Single(x => x.Seq == c.Seq);
        target.Resolution = ConfirmationResolution.ReturnedToSource;
        target.ResolutionReason = reason;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var returned = Support.Event(copy, WarehouseTaskEventKind.ConfirmationReturned, now,
            new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(c.Seq), ["resolution"] = nameof(ConfirmationResolution.ReturnedToSource),
                ["resolutionReason"] = reason },
            request.IdempotencyKey, WarehouseContract.OpMarkPutawayConfirmationReturned, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [returned], WarehouseContract.OpMarkPutawayConfirmationReturned, request.IdempotencyKey,
            fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        if (twin is not null) return twin;
        var current = await Repository.FindAsync(task.Id, ct);
        return current?.Confirmations.SingleOrDefault(x => x.Seq == c.Seq)?.Status == ConfirmationStatus.IntegrityError
            ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(current, c.Seq))
            : Conflict(current, c.Seq);
    }
}
