using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>retryPutawayConfirmation</c> (pack §8.3 "Kurtarma", flow rows 3 / 6 / 7 / 17 / 21 / 28; AC-5, AC-53, AC-54, AC-56, AC-64, AC-69). Only a Pending
/// confirmation (else 409 WMS_CONFIRMATION_NOT_RETRYABLE), by the assignee or a supervisor. (0) the flight lock, taken atomically — another
/// attempt's VALID lock → 409 WMS_CONFIRMATION_IN_FLIGHT, nothing written; (i) the ledger key FIRST: committed → Posted without any pre-check
/// or upstream call (reversed → IntegrityError HolderReversed; another request → IntegrityError KeyReused); (ii) only when not committed: the
/// pre-checks (recall, AVAILABLE, storage fit, serial bucket) → Refused (PreCheck) without calling the ledger, then the posting with the SAME
/// key, dates and serials. No attempt limit.
/// </summary>
public sealed class RetryPutawayConfirmationHandler : PutawayCommandHandler, IRequestHandler<RetryPutawayConfirmationCommand, Response<WarehouseTaskDto>>
{
    private readonly PutawayChecks _checks;

    public RetryPutawayConfirmationHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        PutawayChecks checks)
        : base(repository, reads, support, engine) => _checks = checks;

    public async Task<Response<WarehouseTaskDto>> Handle(RetryPutawayConfirmationCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpRetryPutawayConfirmation), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpRetryPutawayConfirmation, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null || task.Confirmations.All(c => c.Seq != request.Seq)) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (!PutawayTaskRules.MayAct(task, Support.UserId, request.Supervisor)) return PutawayTaskSupport.NotAssignee<WarehouseTaskDto>();

        // ── (0) the flight lock (row 7 / 17 / 28) ──
        var attemptId = Guid.NewGuid();
        WarehouseTask? locked = null;
        for (var attempt = 0; attempt < PutawayTaskRules.MaxResultWriteAttempts && locked is null; attempt++)
        {
            var c = task.Confirmations.Single(x => x.Seq == request.Seq);
            var now = Support.Now();
            if (c.Status != ConfirmationStatus.Pending) return NotRetryable(task);
            if (PutawayTaskRules.LockValid(c, now)) return InFlight(task, c);

            var copy = task.Copy();
            var target = copy.Confirmations.Single(x => x.Seq == request.Seq);
            target.AttemptId = attemptId;
            target.InFlightUntilUtc = now + Support.Lease;
            target.Attempts++;
            target.LastAttemptAtUtc = now;
            var retried = Support.Event(copy, WarehouseTaskEventKind.ConfirmationRetried, now,
                new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(request.Seq), ["attempt"] = PutawayTaskSupport.Int(target.Attempts) },
                request.IdempotencyKey, WarehouseContract.OpRetryPutawayConfirmation, fingerprint);
            var (written, twin) = await SaveKeyedAsync(copy, task.Version, [retried], WarehouseContract.OpRetryPutawayConfirmation, request.IdempotencyKey,
                fingerprint, ct);
            if (written)
            {
                locked = copy;
                break;
            }

            if (twin is not null) return twin;
            task = await Repository.FindAsync(task.Id, ct) ?? throw new InvalidOperationException("A task vanished while its lock was taken.");
        }

        if (locked is null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationConflictRetry,
                "The task kept changing while the lock was taken; retry.", 503, Details(task, request.Seq));
        }

        var confirmation = locked.Confirmations.Single(x => x.Seq == request.Seq);

        // ── (i) the ledger key first — no pre-check, no upstream call when it is already posted ──
        var result = await Engine.LookupAsync(locked, confirmation, ct) ?? await PreCheckedPostAsync(locked, confirmation, ct);
        var outcome = await Engine.WriteResultAsync(locked, request.Seq, attemptId, result, ResultWriteMode.Retry, ct);
        return Answer(outcome, result, request.Seq, 200);
    }

    /// <summary>(ii) — not committed: the pre-checks of §8.3 "Yeniden denemede ön kontroller", then the posting with the stored request.</summary>
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

    private Response<WarehouseTaskDto> NotRetryable(WarehouseTask task)
        => PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotRetryable, "Only a pending confirmation is retried.", 409, Details(task));

    private Response<WarehouseTaskDto> InFlight(WarehouseTask task, TaskConfirmation c)
        => PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationInFlight, "The confirmation is being processed; retry when the lock expires.", 409,
            Details(task, c.Seq, ("inFlightUntilUtc", PutawayTaskSupport.Wire(c.InFlightUntilUtc))));
}
