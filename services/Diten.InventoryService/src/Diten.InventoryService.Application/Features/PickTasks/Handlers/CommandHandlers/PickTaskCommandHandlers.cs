using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.PickTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PickTasks.Handlers.CommandHandlers;

// MOD-0178 DL-5 — the S2 pick task commands (pack §8.19). The confirmation machine is S1's (§8.3; PutawayPostingEngine — one core for both kinds:
// the movement is bin → Staging LOCATION_TRANSFER, PICK_TASK, documentGroupId = outboundNumber, reservationId = the allocation's). What differs
// from the putaway twins: the source and the target are FIXED (no target in the body, no target rules — the target IS the Staging node), the
// Staging node's storage-condition fit is checked, there is no edit and no cancel of a pick task (the outbound's cancel closes it), and "Kalanı
// kapat" works with nothing picked. A pick task that becomes terminal moves its outbound in the same transaction (IPickTaskCompletion).

/// <summary>The skeleton of a pick command: the pick kind (a putaway task id is unknown here) and the pick refusals.</summary>
public abstract class PickCommandHandler : PutawayCommandHandler
{
    protected PickCommandHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    protected override WarehouseTaskKind Kind => WarehouseTaskKind.Pick;
}

/// <summary><c>claimPickTask</c> (§8.19 "Atama / üstlenme S1 kuralıyla").</summary>
public sealed class ClaimPickTaskHandler : PickCommandHandler, IRequestHandler<ClaimPickTaskCommand, Response<WarehouseTaskDto>>
{
    public ClaimPickTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ClaimPickTaskCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpClaimPickTask), ("taskId", request.TaskId.ToString("D")));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpClaimPickTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (task.AssigneeUserId is not null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.TaskAlreadyAssigned, "The task is already assigned; a supervisor re-assigns it.", 409,
                Details(task));
        }

        var now = Support.Now();
        var copy = task.Copy();
        copy.AssigneeUserId = Support.UserId;
        copy.AssigneeName = Support.ActorName;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var claimed = Support.Event(copy, WarehouseTaskEventKind.Claimed, now,
            new Dictionary<string, string?> { ["before.assigneeUserId"] = null, ["after.assigneeUserId"] = Support.UserId.ToString("D") },
            request.IdempotencyKey, WarehouseContract.OpClaimPickTask, fingerprint);
        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [claimed], WarehouseContract.OpClaimPickTask, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? Conflict(await FindTaskAsync(task.Id, ct));
    }
}

/// <summary>
/// <c>confirmPickTask</c> (§8.19; AC-27, AC-33 (ii), AC-75): the key first; then open task, no Pending / IntegrityError, If-Match, the assignee or a
/// supervisor, quantity ≤ remaining (whole bucket for a serial SKU), the Staging node's storage-condition fit (§8.12's target rule applied to
/// Staging), recall (§8.5), the bucket still AVAILABLE, the serial bucket, the ledger lookup of the unresolved Refused siblings; transaction 1;
/// the posting with the reservation id; the result write. INVENTORY's RESERVATION_* refusals make the confirmation Refused (the ledger 4xx rule).
/// </summary>
public sealed class ConfirmPickTaskHandler : PickCommandHandler, IRequestHandler<ConfirmPickTaskCommand, Response<WarehouseTaskDto>>
{
    private readonly PutawayChecks _checks;

    public ConfirmPickTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        PutawayChecks checks)
        : base(repository, reads, support, engine) => _checks = checks;

    public async Task<Response<WarehouseTaskDto>> Handle(ConfirmPickTaskCommand request, CancellationToken ct)
    {
        var input = request.Confirmation;
        if (!InventoryQuantity.TryParsePositive(input.Quantity, out var quantity))
            return WarehouseSupport.Invalid<WarehouseTaskDto>("quantity must be a positive decimal with at most 6 fraction digits.");
        var scanRef = WarehouseSupport.Trimmed(input.ScanRef);
        if (scanRef is { Length: > PutawayTaskRules.MaxScanRefLength }) return WarehouseSupport.Invalid<WarehouseTaskDto>("scanRef must be at most 200 characters.");

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpConfirmPickTask), ("taskId", request.TaskId.ToString("D")),
            ("quantity", PutawayTaskSupport.Q(quantity)), ("channel", input.Channel.ToString()), ("scanRef", scanRef));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpConfirmPickTask, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
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

        var staging = task.ToLocationId ?? throw new InvalidOperationException("A pick task has no Staging target.");
        if (await _checks.StorageFitAsync<WarehouseTaskDto>(task.SkuId, task.SkuLevel, staging, ct) is { } fit) return fit;
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

        // ── transaction 1 ──
        var now = Support.Now();
        var attemptId = Guid.NewGuid();
        var copy = task.Copy();
        var seq = copy.Confirmations.Count == 0 ? 1 : copy.Confirmations.Max(c => c.Seq) + 1;
        var at = PutawayPostingEngine.Second(now);
        var confirmation = new TaskConfirmation
        {
            Seq = seq,
            Quantity = quantity,
            ToLocationId = staging,
            Status = ConfirmationStatus.Pending,
            PostingKey = OutboundFlowRules.PickPostingKey(task.Id, seq),
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
            ["toLocationId"] = staging.ToString("D"), ["postingKey"] = confirmation.PostingKey, ["transactionDate"] = PutawayTaskSupport.Wire(at),
            ["serialIds"] = PutawayTaskSupport.Json(confirmation.SerialIds), ["reservationId"] = task.ReservationId?.ToString("D"), ["channel"] = input.Channel.ToString()
        }, request.IdempotencyKey, WarehouseContract.OpConfirmPickTask, fingerprint);

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [recorded], WarehouseContract.OpConfirmPickTask, request.IdempotencyKey, fingerprint, ct);
        if (!written)
        {
            if (twin is not null) return twin;
            var current = await FindTaskAsync(task.Id, ct);
            return current is not null && PutawayTaskRules.HasPending(current) ? PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
                "A confirmation of this task is waiting for the stock answer; only \"Retry\" is open.", 409, Details(current)) : Conflict(current);
        }

        var result = await Engine.PostAsync(copy, confirmation, ct);
        var outcome = await Engine.WriteResultAsync(copy, seq, attemptId, result, ResultWriteMode.Confirm, ct);
        return Answer(outcome, result, seq, 201);
    }
}

/// <summary>
/// <c>retryPickConfirmation</c> (§8.19 / §8.3 "Kurtarma"): the flight lock; the ledger key FIRST (committed → Posted without a pre-check);
/// otherwise the pre-checks (recall, AVAILABLE, the Staging fit, the serial bucket) and the posting with the SAME stored request.
/// </summary>
public sealed class RetryPickConfirmationHandler : PickCommandHandler, IRequestHandler<RetryPickConfirmationCommand, Response<WarehouseTaskDto>>
{
    private readonly PutawayChecks _checks;

    public RetryPickConfirmationHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        PutawayChecks checks)
        : base(repository, reads, support, engine) => _checks = checks;

    public async Task<Response<WarehouseTaskDto>> Handle(RetryPickConfirmationCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpRetryPickConfirmation), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpRetryPickConfirmation, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
        if (task is null || task.Confirmations.All(c => c.Seq != request.Seq)) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();

        var attemptId = Guid.NewGuid();
        WarehouseTask? locked = null;
        for (var attempt = 0; attempt < PutawayTaskRules.MaxResultWriteAttempts && locked is null; attempt++)
        {
            var c = task.Confirmations.Single(x => x.Seq == request.Seq);
            var now = Support.Now();
            if (c.Status != ConfirmationStatus.Pending)
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotRetryable, "Only a pending confirmation is retried.", 409, Details(task));
            if (PutawayTaskRules.LockValid(c, now))
            {
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationInFlight, "The confirmation is being processed; retry when the lock expires.",
                    409, Details(task, c.Seq, ("inFlightUntilUtc", PutawayTaskSupport.Wire(c.InFlightUntilUtc))));
            }

            var copy = task.Copy();
            var target = copy.Confirmations.Single(x => x.Seq == request.Seq);
            target.AttemptId = attemptId;
            target.InFlightUntilUtc = now + Support.Lease;
            target.Attempts++;
            target.LastAttemptAtUtc = now;
            var retried = Support.Event(copy, WarehouseTaskEventKind.ConfirmationRetried, now,
                new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(request.Seq), ["attempt"] = PutawayTaskSupport.Int(target.Attempts) },
                request.IdempotencyKey, WarehouseContract.OpRetryPickConfirmation, fingerprint);
            var (written, twin) = await SaveKeyedAsync(copy, task.Version, [retried], WarehouseContract.OpRetryPickConfirmation, request.IdempotencyKey, fingerprint, ct);
            if (written)
            {
                locked = copy;
                break;
            }

            if (twin is not null) return twin;
            task = await FindTaskAsync(task.Id, ct) ?? throw new InvalidOperationException("A task vanished while its lock was taken.");
        }

        if (locked is null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationConflictRetry, "The task kept changing while the lock was taken; retry.", 503,
                Details(task, request.Seq));
        }

        var confirmation = locked.Confirmations.Single(x => x.Seq == request.Seq);
        var result = await Engine.LookupAsync(locked, confirmation, ct) ?? await PreCheckedPostAsync(locked, confirmation, ct);
        var outcome = await Engine.WriteResultAsync(locked, request.Seq, attemptId, result, ResultWriteMode.Retry, ct);
        return Answer(outcome, result, request.Seq, 200);
    }

    private async Task<PostingResult> PreCheckedPostAsync(WarehouseTask task, TaskConfirmation c, CancellationToken ct)
    {
        var refusal = await _checks.RecallAsync<bool>(task.LotId, task.LotNumber, ct)
                      ?? await _checks.StillAvailableAsync<bool>(task, ct)
                      ?? await _checks.StorageFitAsync<bool>(task.SkuId, task.SkuLevel, c.ToLocationId, ct);
        if (refusal is null && task.SerialControlled)
        {
            var (serialRefusal, serials) = await _checks.SerialsAsync<bool>(task, c.Quantity, ct);
            refusal = serialRefusal;
            if (refusal is null && !serials.Order(StringComparer.Ordinal).SequenceEqual((c.SerialIds ?? []).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                refusal = PutawayTaskSupport.Fail<bool>(WarehouseErrorCodes.SerialBucketChanged, "The serials of the source bucket changed since the confirmation.", 422,
                    new Dictionary<string, object?> { ["expected"] = PutawayTaskSupport.Q(c.Quantity), ["found"] = PutawayTaskSupport.Q(serials.Count) });
            }
        }

        if (refusal is null) return await Engine.PostAsync(task, c, ct);
        return refusal.StatusCode >= 500
            ? new PostingResult(PostingResultKind.Unknown, Code: refusal.ErrorCode, Message: string.Join(" ", refusal.Errors), Status: refusal.StatusCode)
            : new PostingResult(PostingResultKind.Refused, Code: refusal.ErrorCode, Message: string.Join(" ", refusal.Errors), Details: refusal.ErrorDetails,
                Status: 422, RefusalSource: RefusalSource.PreCheck);
    }
}

/// <summary><c>markPickConfirmationReturned</c> — the S1 "Geri taşındı" rule on a pick confirmation (ledger looked up first, row 20).</summary>
public sealed class MarkPickConfirmationReturnedHandler : PickCommandHandler, IRequestHandler<MarkPickConfirmationReturnedCommand, Response<WarehouseTaskDto>>
{
    public MarkPickConfirmationReturnedHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(MarkPickConfirmationReturnedCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpMarkPickConfirmationReturned), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpMarkPickConfirmationReturned, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        var c = task.Confirmations.SingleOrDefault(x => x.Seq == request.Seq);
        if (c is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();
        if (!PutawayTaskRules.IsUnresolvedRefused(c))
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotResolvable, "Only an unresolved refused confirmation is marked as returned.",
                409, Details(task, c.Seq));
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
            new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(c.Seq), ["resolution"] = nameof(ConfirmationResolution.ReturnedToSource), ["resolutionReason"] = reason },
            request.IdempotencyKey, WarehouseContract.OpMarkPickConfirmationReturned, fingerprint);
        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [returned], WarehouseContract.OpMarkPickConfirmationReturned, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        if (twin is not null) return twin;
        var current = await FindTaskAsync(task.Id, ct);
        return current?.Confirmations.SingleOrDefault(x => x.Seq == c.Seq)?.Status == ConfirmationStatus.IntegrityError
            ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(current, c.Seq))
            : Conflict(current, c.Seq);
    }
}

/// <summary>
/// <c>resolvePickIntegrity</c> — "Uzlaştır" of a pick confirmation (supervisor key <c>warehouse.pick-tasks.resolve</c>): the holder of the key decides —
/// THIS confirmation's own PICK_TASK movement, not reversed → Posted with the ledger's values; otherwise an unresolved Refused (the S1 rule).
/// </summary>
public sealed class ResolvePickIntegrityHandler : PickCommandHandler, IRequestHandler<ResolvePickIntegrityCommand, Response<WarehouseTaskDto>>
{
    public ResolvePickIntegrityHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ResolvePickIntegrityCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpResolvePickIntegrity), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpResolvePickIntegrity, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        var c = task.Confirmations.SingleOrDefault(x => x.Seq == request.Seq);
        if (c is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (c.Status != ConfirmationStatus.IntegrityError)
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotResolvable, "Only an integrity error is resolved.", 409, Details(task, c.Seq));
        if (PutawayTaskRules.HasPending(task)) return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
            "A confirmation of this task is waiting for the stock answer; resolve after it.", 409, Details(task));

        var found = await Engine.RawLookupAsync(task, c, ct);
        var holder = found.Holder;
        var own = found.State != PostingKeyState.NotCommitted && holder is not null && holder.ReversedByTransactionId is null
                  && holder.SourceModule == PutawayPostingEngine.SourceModule && holder.SourceType == PutawayPostingEngine.PickSourceType
                  && holder.SourceDocumentId == task.Id.ToString("D") && holder.SourceLineId == c.Seq.ToString(CultureInfo.InvariantCulture);

        var now = Support.Now();
        var copy = task.Copy();
        var target = copy.Confirmations.Single(x => x.Seq == c.Seq);
        target.HolderMovement = holder is null ? null : PutawayPostingEngine.Holder(holder);
        target.ResolvedBy = Support.UserId;
        target.ResolvedAtUtc = now;
        target.ResolveReason = reason;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var data = new Dictionary<string, string?>
        {
            ["seq"] = PutawayTaskSupport.Int(c.Seq), ["reason"] = reason, ["holderTransactionId"] = holder?.TransactionId.ToString("D"),
            ["holderSourceModule"] = holder?.SourceModule, ["holderSourceDocumentId"] = holder?.SourceDocumentId, ["holderSourceLineId"] = holder?.SourceLineId
        };
        var rows = new List<WarehouseTaskEvent>();
        if (own)
        {
            var wasTerminal = PutawayTaskRules.IsTerminal(copy.Status);
            target.Status = ConfirmationStatus.Posted;
            target.TransactionId = holder!.TransactionId.ToString("D");
            target.TransactionNumber = holder.TransactionNumber;
            target.Quantity = holder.BaseQuantity;
            target.SerialIds = holder.SerialIds?.ToList();
            target.IntegrityResolution = ConfirmationIntegrityResolution.ResolvedOwnMovement;
            PutawayTaskRules.ClearLock(target);
            copy.ConfirmedQuantity += holder.BaseQuantity;
            var superseded = PutawayTaskRules.Supersede(copy, c.Seq);
            var before = copy.Status;
            copy.Status = PutawayTaskRules.Derive(copy);
            if (PutawayTaskRules.IsTerminal(copy.Status) && !wasTerminal) copy.OpenSerialSlot = null;
            data["integrityResolution"] = nameof(ConfirmationIntegrityResolution.ResolvedOwnMovement);
            data["superseded"] = superseded.Count == 0 ? null : PutawayTaskSupport.Seqs(superseded);
            rows.Add(Support.Event(copy, WarehouseTaskEventKind.IntegrityResolved, now, data, request.IdempotencyKey, WarehouseContract.OpResolvePickIntegrity, fingerprint));
            if (copy.ConfirmedQuantity > copy.PlannedQuantity)
            {
                rows.Add(Support.Event(copy, WarehouseTaskEventKind.ConfirmationDeviation, now,
                    new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(c.Seq), ["overPlanned"] = "true" }));
            }

            rows.AddRange(superseded.Select(s => Support.Event(copy, WarehouseTaskEventKind.ConfirmationSuperseded, now,
                new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(s), ["by"] = PutawayTaskSupport.Int(c.Seq) })));
            if (before != WarehouseTaskStatus.Completed && copy.Status == WarehouseTaskStatus.Completed)
            {
                rows.Add(Support.Event(copy, WarehouseTaskEventKind.Completed, now, new Dictionary<string, string?>
                    { ["planned"] = PutawayTaskSupport.Q(copy.PlannedQuantity), ["confirmed"] = PutawayTaskSupport.Q(copy.ConfirmedQuantity) }));
            }
        }
        else
        {
            target.Status = ConfirmationStatus.Refused;
            target.RefusalSource = RefusalSource.Resolve;
            target.RefusedOnRetry = false;
            target.RefusedAtUtc = now;
            target.Resolution = null;
            target.IntegrityResolution = ConfirmationIntegrityResolution.ReturnedToRefused;
            PutawayTaskRules.ClearLock(target);
            data["integrityResolution"] = nameof(ConfirmationIntegrityResolution.ReturnedToRefused);
            rows.Add(Support.Event(copy, WarehouseTaskEventKind.IntegrityResolved, now, data, request.IdempotencyKey, WarehouseContract.OpResolvePickIntegrity, fingerprint));
        }

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, rows, WarehouseContract.OpResolvePickIntegrity, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? Conflict(await FindTaskAsync(task.Id, ct), c.Seq);
    }
}

/// <summary>
/// <c>closePickTaskShort</c> (§8.19; AC-72): Completed with the remainder left — also with NOTHING picked (a pick task has no cancel of its own;
/// all tasks closed with 0 keep the outbound <c>Allocated</c>, §8.18). Never for a serial SKU, by the assignee or a supervisor, with a reason; the
/// Refused confirmations' keys are looked up first (row 20). The reservation does not shrink (FU03 offers no partial release — FU-0178-RSV-PARTIAL).
/// </summary>
public sealed class ClosePickTaskShortHandler : PickCommandHandler, IRequestHandler<ClosePickTaskShortCommand, Response<WarehouseTaskDto>>
{
    public ClosePickTaskShortHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ClosePickTaskShortCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpClosePickTaskShort), ("taskId", request.TaskId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpClosePickTaskShort, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await FindTaskAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();
        if (OpenAndUnlocked(task) is { } locked) return locked;
        if (task.SerialControlled)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SerialPartialNotSupported, "A serial-controlled task is picked whole; it is not closed short.",
                422, Details(task));
        }

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
            request.IdempotencyKey, WarehouseContract.OpClosePickTaskShort, fingerprint);
        var (written, twin) = await SaveKeyedAsync(copy, task.Version, [closed], WarehouseContract.OpClosePickTaskShort, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        if (twin is not null) return twin;
        var current = await FindTaskAsync(task.Id, ct);
        return current is not null && PutawayTaskRules.HasIntegrityError(current) ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(current)) : Conflict(current);
    }
}
