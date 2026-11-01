using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// The shared skeleton of the task commands (pack §8.1, §8.3): the caller's key event of this task and operation answers first (same
/// fingerprint → 200 + the current task, other → 400), BEFORE If-Match and every business rule; a keyed write that loses its compare-and-set
/// or hits the key index re-reads the key before answering 409; the closing commands look the ledger up for their Refused targets first
/// (flow row 20: a late commit → that confirmation IntegrityError, the command 409 and writes nothing of its own).
/// </summary>
public abstract class PutawayCommandHandler
{
    protected PutawayCommandHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
    {
        Repository = repository;
        Reads = reads;
        Support = support;
        Engine = engine;
    }

    protected IWarehouseTaskRepository Repository { get; }

    protected PutawayTaskReads Reads { get; }

    protected PutawayTaskSupport Support { get; }

    protected PutawayPostingEngine Engine { get; }

    /// <summary>The refusal details of a task command: the current task (and the confirmation's seq once transaction 1 opened it — 11 F-1).</summary>
    protected IReadOnlyDictionary<string, object?> Details(WarehouseTask task, int? seq = null, params (string Key, object? Value)[] extra)
    {
        var details = new Dictionary<string, object?> { ["task"] = Reads.Dto(task) };
        if (seq is { } s) details["seq"] = s;
        foreach (var (key, value) in extra.Where(e => e.Value is not null)) details[key] = value;
        return details;
    }

    /// <summary>null = not a replay; otherwise the answer (200 + current task, with the request's own confirmation seq when it has one).</summary>
    protected async Task<Response<WarehouseTaskDto>?> ReplayAsync(Guid taskId, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(taskId, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<WarehouseTaskDto>();
        var task = await Repository.FindAsync(taskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        int? seq = earlier.Data.TryGetValue("seq", out var raw) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;
        return Response<WarehouseTaskDto>.Success(Reads.Dto(task, op is WarehouseContract.OpConfirmPutawayTask or WarehouseContract.OpRetryPutawayConfirmation ? seq : null));
    }

    /// <summary>
    /// A command's own keyed write: the task (version + 1) and its rows with a compare-and-set on <paramref name="expectedVersion"/>. Lost (0
    /// records or a duplicate key event) → the key is re-read first: found → its replay; not found → null (the caller answers its own 409).
    /// </summary>
    protected async Task<(bool Written, Response<WarehouseTaskDto>? Replay)> SaveKeyedAsync(WarehouseTask copy, int expectedVersion,
        IReadOnlyList<WarehouseTaskEvent> events, string op, string key, string fingerprint, CancellationToken ct)
    {
        try
        {
            if (await Engine.SaveAsync(copy, expectedVersion, events, Support.Now(), ct)) return (true, null);
        }
        catch (WarehouseTaskDuplicateException)
        {
            // a concurrent twin of this request committed its key event
        }

        return (false, await ReplayAsync(copy.Id, op, key, fingerprint, ct));
    }

    /// <summary>
    /// Flow row 20 — before a command closes or affects Refused confirmations it looks their keys up in the ledger. A Committed (not reversed)
    /// one is a late commit: that confirmation becomes IntegrityError (StatusDiverged) in its own write, and the command answers 409
    /// WMS_POSTING_INTEGRITY_ERROR without writing its own change. null = nothing committed.
    /// </summary>
    protected async Task<Response<WarehouseTaskDto>?> PreCloseAsync(WarehouseTask task, IEnumerable<int> refusedSeqs, CancellationToken ct)
    {
        foreach (var seq in refusedSeqs.ToList())
        {
            var c = task.Confirmations.Single(x => x.Seq == seq);
            if (c.Status != ConfirmationStatus.Refused) continue;
            var found = await Engine.LookupAsync(task, c, ct);
            if (found?.Kind != PostingResultKind.Posted) continue;

            var written = await Engine.MarkDivergedAsync(task, seq, ct);
            return written.Kind is ResultWriteKind.IntegrityError or ResultWriteKind.AlreadyIntegrityError
                ? PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(written.Task, seq, ("reason", "StatusDiverged")))
                : PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.Conflict, "The task changed while it was checked; reload it.", 409, Details(written.Task));
        }

        return null;
    }

    /// <summary>The common gates of a write on an open task: Open / InProgress, no IntegrityError, no Pending (§8.3 M-4 lock).</summary>
    protected Response<WarehouseTaskDto>? OpenAndUnlocked(WarehouseTask task)
    {
        if (!PutawayTaskRules.IsOpen(task)) return PutawayTaskSupport.InvalidTransition<WarehouseTaskDto>($"The task is {task.Status}.");
        if (PutawayTaskRules.HasIntegrityError(task)) return PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(task));
        return PutawayTaskRules.HasPending(task) ? PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
            "A confirmation of this task is waiting for the stock answer; only \"Retry\" is open.", 409, Details(task)) : null;
    }

    /// <summary>
    /// The answer of a confirmation / retry after its result write (pack §8.3 step 4; WMS draft §4): Posted → <paramref name="successStatus"/> +
    /// <c>confirmationSeq</c>; Refused → 422 with the ledger's (or pre-check's) code and details; IntegrityError → 409; still Pending → 503;
    /// (C1) → 409 IN_FLIGHT; (C2) / (E) / (D) exhausted → 409 WMS_CONFLICT; (A) exhausted → 503 CONFLICT_RETRY. Every refusal carries
    /// <c>details.seq</c> and <c>details.task</c>.
    /// </summary>
    protected Response<WarehouseTaskDto> Answer(ResultWrite written, PostingResult result, int seq, int successStatus)
    {
        var task = written.Task;
        var c = task.Confirmations.Single(x => x.Seq == seq);
        switch (written.Kind)
        {
            case ResultWriteKind.Posted:
            case ResultWriteKind.AlreadyDone:
                return Response<WarehouseTaskDto>.Success(Reads.Dto(task, seq), successStatus);
            case ResultWriteKind.Refused:
            {
                var details = new Dictionary<string, object?>(result.Details ?? new Dictionary<string, object?>()) { ["task"] = Reads.Dto(task), ["seq"] = seq };
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(result.Code ?? WarehouseErrorCodes.Conflict, result.Message ?? "The stock refused the movement.", 422, details);
            }

            case ResultWriteKind.IntegrityError:
            case ResultWriteKind.AlreadyIntegrityError:
                return PutawayTaskSupport.IntegrityLocked<WarehouseTaskDto>(Details(task, seq, ("reason", c.ErrorReason)));
            case ResultWriteKind.StillPending:
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(result.Code ?? Common.InventoryErrorCodes.InventoryUnavailable,
                    result.Message ?? "The stock answer was not received; the confirmation stays pending.", 503, Details(task, seq, ("reason", result.Reason)));
            case ResultWriteKind.InFlightElsewhere:
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationInFlight, "The confirmation is being processed by another attempt.", 409,
                    Details(task, seq, ("inFlightUntilUtc", PutawayTaskSupport.Wire(c.InFlightUntilUtc))));
            case ResultWriteKind.ConflictRetryExhausted:
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationConflictRetry,
                    "The task kept changing while the result was written; the confirmation stays pending — retry.", 503, Details(task, seq));
            default:
                return Conflict(task, seq);
        }
    }

    protected Response<WarehouseTaskDto> Conflict(WarehouseTask? task = null, int? seq = null)
        => PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.Conflict, "The task changed since it was read; reload it and retry with its current ETag.", 409,
            task is null ? null : Details(task, seq));
}
