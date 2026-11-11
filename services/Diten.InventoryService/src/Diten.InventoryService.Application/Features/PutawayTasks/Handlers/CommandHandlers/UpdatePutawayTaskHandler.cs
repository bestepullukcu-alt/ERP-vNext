using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>updatePutawayTask</c> (If-Match; pack §8.15, §12; AC-39, AC-40). The source bucket never changes (the reader refuses its members, 400).
/// The planned quantity may not fall below confirmed + pending (422 WMS_QUANTITY_BELOW_CONFIRMED) nor grow past the open quantity, and is fixed
/// for a serial SKU; a changed target passes the target rules, the storage-condition fit and the recall rule. A Pending / IntegrityError task
/// is locked. One write: the task + the <c>Updated</c> key event (before / after) [+ <c>SuggestionApplied</c>].
/// </summary>
public sealed class UpdatePutawayTaskHandler : PutawayCommandHandler, IRequestHandler<UpdatePutawayTaskCommand, Response<WarehouseTaskDto>>
{
    private readonly PutawayChecks _checks;

    public UpdatePutawayTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        PutawayChecks checks)
        : base(repository, reads, support, engine) => _checks = checks;

    public async Task<Response<WarehouseTaskDto>> Handle(UpdatePutawayTaskCommand request, CancellationToken ct)
    {
        var change = request.Change;
        decimal? planned = null;
        if (change.PlannedQuantity is not null)
        {
            if (!InventoryQuantity.TryParsePositive(change.PlannedQuantity, out var parsed))
                return WarehouseSupport.Invalid<WarehouseTaskDto>("plannedQuantity must be a positive decimal with at most 6 fraction digits.");
            planned = parsed;
        }

        var note = WarehouseSupport.Trimmed(change.Note);
        if (note is { Length: > PutawayTaskRules.MaxNoteLength }) return WarehouseSupport.Invalid<WarehouseTaskDto>("note must be at most 1000 characters.");
        if (change.TargetSource == WarehouseTargetSource.SuggestionApplied && !(change.ToLocationSent && change.ToLocationId is not null))
            return WarehouseSupport.Invalid<WarehouseTaskDto>("targetSource SuggestionApplied needs toLocationId.");

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpUpdatePutawayTask), ("taskId", request.TaskId.ToString("D")),
            ("plannedQuantity", planned is { } p ? PutawayTaskSupport.Q(p) : null), ("toLocationSent", change.ToLocationSent ? "1" : "0"),
            ("toLocationId", change.ToLocationId?.ToString("D")), ("targetSource", change.TargetSource?.ToString()), ("assigneeSent", change.AssigneeSent ? "1" : "0"),
            ("assigneeUserId", change.AssigneeUserId?.ToString("D")), ("priority", change.Priority?.ToString()), ("noteSent", change.NoteSent ? "1" : "0"), ("note", note));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpUpdatePutawayTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (change.AssigneeSent && change.AssigneeUserId != task.AssigneeUserId
            && await _checks.AssigneeAsync<WarehouseTaskDto>(change.AssigneeUserId, ct) is { } assignee) return assignee;

        if (planned is { } newPlanned && newPlanned != task.PlannedQuantity)
        {
            if (task.SerialControlled)
            {
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SerialPartialNotSupported,
                    "The planned quantity of a serial-controlled task is the whole bucket and does not change.", 422);
            }

            var floor = task.ConfirmedQuantity + PutawayTaskRules.PendingQuantity(task);
            if (newPlanned < floor)
            {
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.QuantityBelowConfirmed,
                    "The planned quantity cannot fall below the confirmed and pending quantity.", 422,
                    new Dictionary<string, object?> { ["confirmedQuantity"] = PutawayTaskSupport.Q(floor) });
            }

            if (newPlanned > task.PlannedQuantity && await OpenQuantityAsync(task, ct) is { } open && newPlanned - task.ConfirmedQuantity > open)
            {
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.QuantityExceedsOpen, "The planned quantity exceeds the open quantity of the bucket.", 422,
                    new Dictionary<string, object?> { ["openQuantity"] = PutawayTaskSupport.Q(Math.Max(0m, open)) });
            }
        }

        var target = change.ToLocationSent ? change.ToLocationId : task.ToLocationId;
        if (change.ToLocationSent && target is { } to && to != task.ToLocationId)
        {
            if (await _checks.TargetAsync<WarehouseTaskDto>(task.WarehouseId, task.FromLocationId, to, ct) is { } refusal) return refusal;
            if (await _checks.StorageFitAsync<WarehouseTaskDto>(task.SkuId, task.SkuLevel, to, ct) is { } fit) return fit;
            if (await _checks.RecallAsync<WarehouseTaskDto>(task.LotId, task.LotNumber, ct) is { } recalled) return recalled;
        }

        var now = Support.Now();
        var copy = task.Copy();
        var before = Snapshot(task);
        if (planned is { } q) copy.PlannedQuantity = q;
        if (change.ToLocationSent) copy.ToLocationId = change.ToLocationId;
        if (change.TargetSource is { } source) copy.TargetSource = source;
        if (change.AssigneeSent)
        {
            copy.AssigneeUserId = change.AssigneeUserId;
            copy.AssigneeName = null;
        }

        if (change.Priority is { } priority) copy.Priority = priority;
        if (change.NoteSent) copy.Note = note;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;

        var events = new List<WarehouseTaskEvent>
        {
            Support.Event(copy, WarehouseTaskEventKind.Updated, now, Merge(before, Snapshot(copy)), request.IdempotencyKey, WarehouseContract.OpUpdatePutawayTask, fingerprint)
        };
        if (change.TargetSource == WarehouseTargetSource.SuggestionApplied)
        {
            events.Add(Support.Event(copy, WarehouseTaskEventKind.SuggestionApplied, now, new Dictionary<string, string?>
                { ["suggestedToLocationId"] = copy.SuggestedToLocationId?.ToString("D"), ["toLocationId"] = copy.ToLocationId?.ToString("D") }));
        }

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, events, WarehouseContract.OpUpdatePutawayTask, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? Conflict(await Repository.FindAsync(task.Id, ct));
    }

    private async Task<decimal?> OpenQuantityAsync(WarehouseTask task, CancellationToken ct)
    {
        try
        {
            var onHand = (await _checks.BucketRowsAsync(task.WarehouseId, task.FromLocationId, task.SkuId, task.SkuLevel, task.LotId, ct))
                .Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand);
            var others = (await Repository.OpenTasksFromAsync([task.FromLocationId], ct))
                .Where(t => t.Id != task.Id && t.SkuId == task.SkuId && t.SkuLevel == task.SkuLevel && string.Equals(t.LotId, task.LotId, StringComparison.Ordinal))
                .Sum(PutawayTaskRules.OpenHold);
            return onHand - others;
        }
        catch (WarehouseInventoryUnavailableException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?> Snapshot(WarehouseTask t) => new()
    {
        ["plannedQuantity"] = PutawayTaskSupport.Q(t.PlannedQuantity), ["toLocationId"] = t.ToLocationId?.ToString("D"),
        ["assigneeUserId"] = t.AssigneeUserId?.ToString("D"), ["priority"] = t.Priority.ToString(), ["note"] = t.Note
    };

    private static Dictionary<string, string?> Merge(Dictionary<string, string?> before, Dictionary<string, string?> after)
    {
        var data = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in before) data["before." + k] = v;
        foreach (var (k, v) in after) data["after." + k] = v;
        return data;
    }
}
