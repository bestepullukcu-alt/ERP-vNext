using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>confirmPutawayTask</c> (pack §8.3 steps 1–5, flow rows 1 / 2 / 5 / 9 / 20 / 25–29; AC-2…AC-5, AC-8…AC-12, AC-37, AC-39, AC-40, AC-47, AC-49,
/// AC-51…AC-57, AC-63, AC-64, AC-68). (1) the key first, then the checks — open task, no Pending / IntegrityError, If-Match, the assignee or a
/// supervisor, quantity ≤ remaining (whole bucket for a serial SKU), the deviation reason, the target, the storage-condition fit, recall, the
/// bucket still AVAILABLE, the serial bucket — and the ledger lookup of the unresolved Refused siblings (row 20). (2) transaction 1: the Pending
/// confirmation with its posting key, dates (once), serials and flight lock + <c>ConfirmationRecorded</c> (the key event). (3) the posting
/// under the time limit. (4) the result write with the one rule (A)–(E).
/// </summary>
public sealed class ConfirmPutawayTaskHandler : PutawayCommandHandler, IRequestHandler<ConfirmPutawayTaskCommand, Response<WarehouseTaskDto>>
{
    private readonly PutawayChecks _checks;

    public ConfirmPutawayTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        PutawayChecks checks)
        : base(repository, reads, support, engine) => _checks = checks;

    public async Task<Response<WarehouseTaskDto>> Handle(ConfirmPutawayTaskCommand request, CancellationToken ct)
    {
        var input = request.Confirmation;
        if (!InventoryQuantity.TryParsePositive(input.Quantity, out var quantity))
            return WarehouseSupport.Invalid<WarehouseTaskDto>("quantity must be a positive decimal with at most 6 fraction digits.");
        var deviation = WarehouseSupport.Trimmed(input.DeviationReason);
        var scanRef = WarehouseSupport.Trimmed(input.ScanRef);
        if (scanRef is { Length: > PutawayTaskRules.MaxScanRefLength }) return WarehouseSupport.Invalid<WarehouseTaskDto>("scanRef must be at most 200 characters.");

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpConfirmPutawayTask), ("taskId", request.TaskId.ToString("D")),
            ("quantity", PutawayTaskSupport.Q(quantity)), ("toLocationId", input.ToLocationId.ToString("D")), ("deviationReason", deviation),
            ("channel", input.Channel.ToString()), ("scanRef", scanRef));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpConfirmPutawayTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();

        if (task.SerialControlled && quantity != task.PlannedQuantity)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SerialPartialNotSupported,
                "A serial-controlled task is confirmed whole (no partial confirmation).", 422, Details(task));
        }

        var remaining = PutawayTaskRules.Remaining(task);
        if (quantity > remaining)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.QuantityExceedsRemaining, "The quantity exceeds the remaining quantity of the task.", 422,
                Details(task, null, ("remainingQuantity", PutawayTaskSupport.Q(remaining))));
        }

        if (task.ToLocationId is { } plannedTarget && plannedTarget != input.ToLocationId && !PutawayTaskRules.IsReason(deviation))
        {
            return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("deviationReason");
        }

        if (await _checks.TargetAsync<WarehouseTaskDto>(task.WarehouseId, task.FromLocationId, input.ToLocationId, ct) is { } target) return target;
        if (await _checks.StorageFitAsync<WarehouseTaskDto>(task.SkuId, task.SkuLevel, input.ToLocationId, ct) is { } fit) return fit;
        if (await _checks.RecallAsync<WarehouseTaskDto>(task.LotId, task.LotNumber, ct) is { } recalled) return recalled;
        if (await _checks.StillAvailableAsync<WarehouseTaskDto>(task, ct) is { } held) return held;

        IReadOnlyList<string> serials = [];
        if (task.SerialControlled)
        {
            var (serialRefusal, read) = await _checks.SerialsAsync<WarehouseTaskDto>(task, quantity, ct);
            if (serialRefusal is not null) return serialRefusal;
            serials = read;
        }

        if (await PreCloseAsync(task, task.Confirmations.Where(PutawayTaskRules.IsUnresolvedRefused).Select(c => c.Seq), ct) is { } diverged) return diverged;

        // ── transaction 1: the Pending confirmation, its key, dates, serials and flight lock (+ the key event) ──
        var now = Support.Now();
        var attemptId = Guid.NewGuid();
        var copy = task.Copy();
        var seq = copy.Confirmations.Count == 0 ? 1 : copy.Confirmations.Max(c => c.Seq) + 1;
        var at = PutawayPostingEngine.Second(now);
        var confirmation = new TaskConfirmation
        {
            Seq = seq,
            Quantity = quantity,
            ToLocationId = input.ToLocationId,
            DeviationReason = deviation,
            Status = ConfirmationStatus.Pending,
            PostingKey = PutawayTaskRules.PostingKey(task.Id, seq),
            TransactionDate = at,
            PostingDate = at,
            SerialIds = serials.Count > 0 ? [.. serials] : null,
            AttemptId = attemptId,
            InFlightUntilUtc = now + Support.Lease,
            Channel = input.Channel,
            ScanRef = scanRef,
            ConfirmedBy = Support.UserId,
            ConfirmedByName = Support.ActorName,
            ConfirmedAtUtc = now,
            Attempts = 1,
            LastAttemptAtUtc = now
        };
        copy.Confirmations.Add(confirmation);
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var recorded = Support.Event(copy, WarehouseTaskEventKind.ConfirmationRecorded, now, new Dictionary<string, string?>
        {
            ["seq"] = PutawayTaskSupport.Int(seq), ["quantity"] = PutawayTaskSupport.Q(quantity), ["fromLocationId"] = task.FromLocationId.ToString("D"),
            ["toLocationId"] = input.ToLocationId.ToString("D"), ["postingKey"] = confirmation.PostingKey, ["transactionDate"] = PutawayTaskSupport.Wire(at),
            ["serialIds"] = PutawayTaskSupport.Json(confirmation.SerialIds), ["deviationReason"] = deviation, ["channel"] = input.Channel.ToString()
        }, request.IdempotencyKey, WarehouseContract.OpConfirmPutawayTask, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [recorded], WarehouseContract.OpConfirmPutawayTask, request.IdempotencyKey, fingerprint, ct);
        if (!written)
        {
            if (twin is not null) return twin;
            var current = await Repository.FindAsync(task.Id, ct);
            return current is not null && PutawayTaskRules.HasPending(current) ? PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
                "A confirmation of this task is waiting for the stock answer; only \"Retry\" is open.", 409, Details(current)) : Conflict(current);
        }

        // ── (3) the posting, (4) the result write ──
        var result = await Engine.PostAsync(copy, confirmation, ct);
        var outcome = await Engine.WriteResultAsync(copy, seq, attemptId, result, ResultWriteMode.Confirm, ct);
        return Answer(outcome, result, seq, 201);
    }
}
