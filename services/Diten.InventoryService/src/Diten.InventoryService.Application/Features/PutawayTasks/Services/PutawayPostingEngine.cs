using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

public enum PostingResultKind
{
    Posted,
    Refused,
    KeyReused,
    HolderReversed,

    /// <summary>The commit outcome is not known (5xx, timeout, cancellation, unexpected exception) — the confirmation stays Pending (Codex P2).</summary>
    Unknown
}

/// <summary>One posting attempt's result (a ledger answer, a ledger key lookup, or a pre-check refusal).</summary>
public sealed record PostingResult(
    PostingResultKind Kind,
    string? TransactionId = null,
    string? TransactionNumber = null,
    string? Code = null,
    string? Message = null,
    IReadOnlyDictionary<string, object?>? Details = null,
    int Status = 0,
    string? Reason = null,
    TaskHolderMovement? Holder = null,
    RefusalSource RefusalSource = RefusalSource.Ledger4xx);

/// <summary>Who writes a result: the confirmation's own attempt, a user's retry, or the reconciler (no lock, no attempt id).</summary>
public enum ResultWriteMode
{
    Confirm,
    Retry,
    Reconcile
}

public enum ResultWriteKind
{
    Posted,
    Refused,
    IntegrityError,
    StillPending,
    AlreadyDone,
    InFlightElsewhere,
    OwnedByExpiredAttempt,
    NotPending,
    AlreadyIntegrityError,
    ConflictRetryExhausted,
    DivergedExhausted
}

public sealed record ResultWrite(ResultWriteKind Kind, WarehouseTask Task);

/// <summary>
/// The confirmation algorithm's shared core (pack §8.3 steps 3–5, flow rows 2–12 / 21 / 25–28): build the confirmation's STORED ledger request
/// (the same bytes on every attempt), post it through <see cref="IWarehouseMovementPoster"/> under the posting time limit, look its key up
/// through <see cref="IPostingKeyLookup"/>, and write the result with the one rule (A)–(E) — every write a version compare-and-set.
/// </summary>
public sealed class PutawayPostingEngine
{
    public const string SourceModule = WarehouseContract.ModuleId;
    public const string SourceType = "PUTAWAY_TASK";
    private const string WireDate = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private readonly IWarehouseTaskRepository _tasks;
    private readonly IWarehouseMovementPoster _poster;
    private readonly IPostingKeyLookup _lookup;
    private readonly PutawayTaskSupport _support;
    private readonly TimeProvider _clock; private readonly IPickTaskCompletion? _pickCompletion; // DL-5: the pick task's outbound step

    public PutawayPostingEngine(IWarehouseTaskRepository tasks, IWarehouseMovementPoster poster, IPostingKeyLookup lookup, PutawayTaskSupport support,
        TimeProvider clock, IPickTaskCompletion? pickCompletion = null)
    {
        _tasks = tasks;
        _poster = poster;
        _lookup = lookup;
        _support = support;
        _clock = clock; _pickCompletion = pickCompletion;
    }

    /// <summary>Times are stored to the second, so the stored value IS the value sent (the ledger fingerprint covers the dates, F-2).</summary>
    public static DateTimeOffset Second(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    /// <summary>
    /// §8.3 step 3: the confirmation's <c>LOCATION_TRANSFER</c> — same warehouse, from the source to the confirmation's target, stock status
    /// kept, lot and serials, quantity in the base unit, source MOD-0178 / PUTAWAY_TASK / task / seq, the posting key and the dates of
    /// transaction 1. Deterministic: a retry and a lookup build the identical request.
    /// </summary>
    public static PostMovementCommand Request(WarehouseTask task, TaskConfirmation c) => new()
    {
        HeaderIdempotencyKey = c.PostingKey,
        IdempotencyKey = c.PostingKey,
        DocumentGroupId = task.Kind == WarehouseTaskKind.Pick ? task.OutboundNumber ?? task.Number : task.Number, ReservationId = PickReservation(task),
        MovementType = WireNames.ToWire(Domain.Enums.MovementType.LocationTransfer),
        ItemId = task.ItemId.ToString("D"),
        SkuId = task.SkuId.ToString("D"),
        SkuLevel = WireNames.ToWire(task.SkuLevel),
        FromWarehouseId = task.WarehouseId.ToString("D"),
        FromLocationId = task.FromLocationId.ToString("D"),
        ToWarehouseId = task.WarehouseId.ToString("D"),
        ToLocationId = c.ToLocationId.ToString("D"),
        Quantity = InventoryQuantity.ToWire(c.Quantity),
        UomId = task.BaseUomId,
        LotId = task.LotId,
        SerialIds = c.SerialIds is { Count: > 0 } serials ? serials : null,
        FromStockStatus = WireNames.ToWire(task.StockStatus),
        ToStockStatus = WireNames.ToWire(task.StockStatus),
        SourceModule = SourceModule,
        SourceType = SourceTypeOf(task.Kind),
        SourceDocumentId = task.Id.ToString("D"),
        SourceLineId = c.Seq.ToString(CultureInfo.InvariantCulture),
        TransactionDate = c.TransactionDate.UtcDateTime.ToString(WireDate, CultureInfo.InvariantCulture),
        PostingDate = c.PostingDate.UtcDateTime.ToString(WireDate, CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// §8.3 step 3 + the INVENTORY answer mapping (F-2, Codex P2): 2xx → Posted; 400 IDEMPOTENCY_KEY_REUSED → integrity error; any other definite
    /// 4xx → Refused; every outcome whose commit is not known — any 5xx, the posting time limit, cancellation, an unexpected exception — → Unknown.
    /// </summary>
    public async Task<PostingResult> PostAsync(WarehouseTask task, TaskConfirmation c, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_support.Options.PostingTimeoutSeconds), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            var response = await _poster.PostAsync(Request(task, c), _support.LegalEntityId, _support.UserId, linked.Token);
            if (response.IsSuccessful && response.Data is { } posted) return new PostingResult(PostingResultKind.Posted, posted.TransactionId, posted.TransactionNumber);
            if (string.Equals(response.ErrorCode, InventoryErrorCodes.IdempotencyKeyReused, StringComparison.Ordinal))
            {
                return new PostingResult(PostingResultKind.KeyReused, Code: response.ErrorCode, Message: string.Join(" ", response.Errors), Status: response.StatusCode,
                    Reason: "KeyReused");
            }

            if (response.StatusCode == 503)
            {
                return new PostingResult(PostingResultKind.Unknown, Code: response.ErrorCode ?? InventoryErrorCodes.InventoryUnavailable,
                    Message: string.Join(" ", response.Errors), Status: 503);
            }

            if (response.StatusCode >= 500)
            {
                return Unknown();
            }

            return new PostingResult(PostingResultKind.Refused, Code: response.ErrorCode ?? InventoryErrorCodes.InvalidRequest, Message: string.Join(" ", response.Errors),
                Details: response.ErrorDetails, Status: response.StatusCode, RefusalSource: RefusalSource.Ledger4xx);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return Unknown("PostingTimeout");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // an unexpected exception or a cancellation: the commit outcome is unknown — Refused is never written for it (Codex P2)
            return Unknown();
        }
    }

    private static PostingResult Unknown(string reason = "OutcomeUnknown")
        => new(PostingResultKind.Unknown, Code: InventoryErrorCodes.InventoryUnavailable,
            Message: "The stock answer was not received; the confirmation stays pending and is retried with the same key.", Status: 503, Reason: reason);

    /// <summary>
    /// The ledger key lookup (§8.3 retry step (i), reconciler): Committed and not reversed → Posted (the holder's transaction); Committed but
    /// reversed → HolderReversed; another request under the key → KeyReused; nothing under the key → null.
    /// </summary>
    public async Task<PostingResult?> LookupAsync(WarehouseTask task, TaskConfirmation c, CancellationToken ct)
    {
        var found = await _lookup.LookupAsync(Request(task, c), ct);
        if (found.State == PostingKeyState.NotCommitted || found.Holder is not { } h) return null;
        var holder = Holder(h);
        return found.State switch
        {
            PostingKeyState.Committed when h.ReversedByTransactionId is null => new PostingResult(PostingResultKind.Posted, h.TransactionId.ToString("D"),
                h.TransactionNumber, Holder: holder),
            PostingKeyState.Committed => new PostingResult(PostingResultKind.HolderReversed, Code: WarehouseErrorCodes.PostingIntegrityError,
                Message: "The movement that holds this confirmation's key was reversed.", Status: 409, Reason: "HolderReversed", Holder: holder),
            _ => new PostingResult(PostingResultKind.KeyReused, Code: InventoryErrorCodes.IdempotencyKeyReused,
                Message: "The ledger holds a different movement under this confirmation's key.", Status: 400, Reason: "KeyReused", Holder: holder)
        };
    }

    /// <summary>The raw lookup with its holder (the resolve decides on the holder's source).</summary>
    public Task<PostingKeyLookupResult> RawLookupAsync(WarehouseTask task, TaskConfirmation c, CancellationToken ct) => _lookup.LookupAsync(Request(task, c), ct);

    public static TaskHolderMovement Holder(PostingKeyHolder h) => new()
    {
        TransactionId = h.TransactionId.ToString("D"),
        TransactionNumber = h.TransactionNumber,
        SourceModule = h.SourceModule,
        SourceType = h.SourceType,
        SourceDocumentId = h.SourceDocumentId,
        SourceLineId = h.SourceLineId,
        Quantity = h.Quantity,
        UomId = h.UomId,
        BaseQuantity = h.BaseQuantity,
        FromWarehouseId = h.FromWarehouseId,
        FromLocationId = h.FromLocationId,
        ToWarehouseId = h.ToWarehouseId,
        ToLocationId = h.ToLocationId,
        LotId = h.LotId,
        SerialIds = h.SerialIds?.ToList(),
        FromStockStatus = h.FromStockStatus,
        ToStockStatus = h.ToStockStatus,
        PostingDate = h.PostingDate,
        ReversedByTransactionId = h.ReversedByTransactionId?.ToString("D")
    };

    /// <summary>
    /// Writes one attempt's result (pack §8.3 step 4): read → the one rule → write with the read version; a lost compare-and-set re-reads and
    /// decides again ((A) ≤ 3 times, then 503 WMS_CONFIRMATION_CONFLICT_RETRY with the confirmation still Pending). An Unknown result writes
    /// nothing (the lock stays until it expires). The timeline rows go with the write; the central event is enlisted only when it wrote.
    /// </summary>
    public async Task<ResultWrite> WriteResultAsync(WarehouseTask known, int seq, Guid? attemptId, PostingResult result, ResultWriteMode mode, CancellationToken ct)
    {
        if (result.Kind == PostingResultKind.Unknown) return new ResultWrite(ResultWriteKind.StillPending, known);

        var outcome = result.Kind switch
        {
            PostingResultKind.Posted => AttemptOutcome.Posted,
            PostingResultKind.Refused => AttemptOutcome.Refused,
            PostingResultKind.HolderReversed => AttemptOutcome.HolderReversed,
            _ => AttemptOutcome.KeyReused
        };
        var task = known;
        for (var attempt = 0; attempt < PutawayTaskRules.MaxResultWriteAttempts; attempt++)
        {
            var c = task.Confirmations.Single(x => x.Seq == seq);
            var now = _support.Now();
            switch (PutawayTaskRules.Decide(c, attemptId, now, outcome, result.TransactionId))
            {
                case ResultDecision.AlreadyDone: return new ResultWrite(ResultWriteKind.AlreadyDone, task);
                case ResultDecision.InFlightElsewhere: return new ResultWrite(ResultWriteKind.InFlightElsewhere, task);
                case ResultDecision.OwnedByExpiredAttempt: return new ResultWrite(ResultWriteKind.OwnedByExpiredAttempt, task);
                case ResultDecision.NotPending: return new ResultWrite(ResultWriteKind.NotPending, task);
                case ResultDecision.AlreadyIntegrityError: return new ResultWrite(ResultWriteKind.AlreadyIntegrityError, task);
                case ResultDecision.MarkDiverged: return await MarkDivergedAsync(task, seq, ct);
            }

            var copy = task.Copy();
            var target = copy.Confirmations.Single(x => x.Seq == seq);
            var (kind, events) = Apply(copy, target, result, mode, now);
            if (await SaveAsync(copy, task.Version, events, now, ct)) return new ResultWrite(kind, copy);
            task = await FindAsync(task, ct) ?? throw new InvalidOperationException("A task vanished while its result was written.");
        }

        return new ResultWrite(ResultWriteKind.ConflictRetryExhausted, task);
    }

    private (ResultWriteKind Kind, List<WarehouseTaskEvent> Events) Apply(WarehouseTask task, TaskConfirmation c, PostingResult result, ResultWriteMode mode,
        DateTimeOffset now)
    {
        var events = new List<WarehouseTaskEvent>();
        var seq = PutawayTaskSupport.Int(c.Seq);
        switch (result.Kind)
        {
            case PostingResultKind.Posted:
            {
                var (applied, superseded, completed) = PutawayTaskRules.ApplyPosted(task, c, result.TransactionId!, result.TransactionNumber ?? string.Empty,
                    clearLock: mode != ResultWriteMode.Reconcile);
                if (!applied)
                {
                    Integrity(c, "OverPlanned", WarehouseErrorCodes.PostingIntegrityError, "The posted quantity would exceed the planned quantity.", null, mode);
                    events.Add(_support.Event(task, WarehouseTaskEventKind.ConfirmationIntegrityError, now,
                        new Dictionary<string, string?> { ["seq"] = seq, ["reason"] = "OverPlanned", ["transactionId"] = result.TransactionId }));
                    return (ResultWriteKind.IntegrityError, events);
                }

                events.Add(_support.Event(task, mode == ResultWriteMode.Reconcile ? WarehouseTaskEventKind.ConfirmationReconciled : WarehouseTaskEventKind.ConfirmationPosted, now,
                    new Dictionary<string, string?>
                    {
                        ["seq"] = seq, ["transactionId"] = c.TransactionId, ["transactionNumber"] = c.TransactionNumber,
                        ["superseded"] = superseded.Count == 0 ? null : PutawayTaskSupport.Seqs(superseded)
                    }));
                events.AddRange(superseded.Select(s => _support.Event(task, WarehouseTaskEventKind.ConfirmationSuperseded, now,
                    new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(s), ["by"] = seq })));
                if (completed)
                {
                    events.Add(_support.Event(task, WarehouseTaskEventKind.Completed, now, new Dictionary<string, string?>
                        { ["planned"] = PutawayTaskSupport.Q(task.PlannedQuantity), ["confirmed"] = PutawayTaskSupport.Q(task.ConfirmedQuantity) }));
                }

                return (ResultWriteKind.Posted, events);
            }

            case PostingResultKind.Refused:
                c.Status = ConfirmationStatus.Refused;
                c.ErrorCode = result.Code;
                c.ErrorMessage = result.Message;
                c.ErrorDetailsJson = PutawayTaskSupport.Json(result.Details);
                c.ErrorStatus = result.Status;
                c.ErrorReason = null;
                c.RefusalSource = result.RefusalSource;
                c.RefusedOnRetry = mode == ResultWriteMode.Retry;
                c.RefusedAtUtc = now;
                PutawayTaskRules.ClearLock(c);
                events.Add(_support.Event(task, WarehouseTaskEventKind.ConfirmationRefused, now, new Dictionary<string, string?>
                {
                    ["seq"] = seq, ["errorCode"] = result.Code, ["errorStatus"] = PutawayTaskSupport.Int(result.Status),
                    ["refusalSource"] = result.RefusalSource.ToString(), ["refusedOnRetry"] = c.RefusedOnRetry ? "true" : "false"
                }));
                return (ResultWriteKind.Refused, events);

            default:
                Integrity(c, result.Reason ?? "KeyReused", result.Code, result.Message, result.Holder, mode);
                events.Add(_support.Event(task, WarehouseTaskEventKind.ConfirmationIntegrityError, now, new Dictionary<string, string?>
                {
                    ["seq"] = seq, ["reason"] = c.ErrorReason, ["holderTransactionId"] = result.Holder?.TransactionId,
                    ["holderSourceModule"] = result.Holder?.SourceModule, ["holderSourceDocumentId"] = result.Holder?.SourceDocumentId
                }));
                return (ResultWriteKind.IntegrityError, events);
        }
    }

    private static void Integrity(TaskConfirmation c, string reason, string? code, string? message, TaskHolderMovement? holder, ResultWriteMode mode)
    {
        c.Status = ConfirmationStatus.IntegrityError;
        c.ErrorReason = reason;
        c.ErrorCode = code ?? WarehouseErrorCodes.PostingIntegrityError;
        c.ErrorMessage = message;
        c.ErrorStatus = 409;
        if (holder is not null) c.HolderMovement = holder;
        if (mode != ResultWriteMode.Reconcile) PutawayTaskRules.ClearLock(c);
    }

    /// <summary>
    /// (D) — this attempt got Posted but the confirmation is Refused (another attempt's write): one conditional write to IntegrityError
    /// (StatusDiverged, Resolution → PriorResolution); a lost write re-reads — already IntegrityError → no write; still Refused → again (≤ 3,
    /// then 409 WMS_CONFLICT; the defensive branch catches it later).
    /// </summary>
    public async Task<ResultWrite> MarkDivergedAsync(WarehouseTask task, int seq, CancellationToken ct, bool system = false)
    {
        for (var attempt = 0; attempt < PutawayTaskRules.MaxResultWriteAttempts; attempt++)
        {
            var current = task.Confirmations.Single(x => x.Seq == seq);
            if (current.Status == ConfirmationStatus.IntegrityError) return new ResultWrite(ResultWriteKind.AlreadyIntegrityError, task);
            if (current.Status != ConfirmationStatus.Refused) return new ResultWrite(ResultWriteKind.NotPending, task);

            var now = _support.Now();
            var copy = task.Copy();
            var c = copy.Confirmations.Single(x => x.Seq == seq);
            var prior = c.Resolution;
            PutawayTaskRules.MarkDiverged(c);
            var data = new Dictionary<string, string?>
                { ["seq"] = PutawayTaskSupport.Int(seq), ["reason"] = "StatusDiverged", ["priorResolution"] = prior?.ToString() };
            var e = system ? _support.SystemEvent(copy, WarehouseTaskEventKind.ConfirmationIntegrityError, now, data)
                : _support.Event(copy, WarehouseTaskEventKind.ConfirmationIntegrityError, now, data);
            if (await SaveAsync(copy, task.Version, [e], now, ct)) return new ResultWrite(ResultWriteKind.IntegrityError, copy);
            task = await FindAsync(task, ct) ?? throw new InvalidOperationException("A task vanished while its result was written.");
        }

        return new ResultWrite(ResultWriteKind.DivergedExhausted, task);
    }

    /// <summary>One task write: version + 1, the timeline rows, a compare-and-set on the read version; false = 0 records (nothing written).</summary>
    public async Task<bool> SaveAsync(WarehouseTask copy, int expectedVersion, IReadOnlyList<WarehouseTaskEvent> events, DateTimeOffset now, CancellationToken ct)
    {
        copy.Version = expectedVersion + 1;
        copy.UpdatedAt = now;
        var outcome = await _tasks.InTransactionAsync(async (session, tx) =>
        {
            if (!await session.ReplaceTaskAsync(copy, expectedVersion, tx)) return WarehouseWriteOutcome.None;
            await session.InsertEventsAsync(events, tx);
            // DL-5 (§8.18 / §8.19): a pick task that is terminal now moves its outbound in THIS transaction (Picked when every task is terminal
            // and something was picked; otherwise the outbound is only touched, so two concurrent last writes conflict instead of skewing)
            if (copy.Kind == WarehouseTaskKind.Pick && PutawayTaskRules.IsTerminal(copy.Status) && copy.OutboundId is { } outboundId && _pickCompletion is not null)
                await _pickCompletion.ApplyAsync(session, outboundId, now, tx);
            return new WarehouseWriteOutcome(1, copy.Id);
        }, ct);
        return outcome.Affected > 0;
    }

    // ── DL-5 (pack §8.19): the pick kind on the one engine — kept BELOW the poster call (MOD-0176's ExpiryQuarantineTypeExemptionTests pins its line) ──

    /// <summary>The source type of a pick task's movement.</summary>
    public const string PickSourceType = "PICK_TASK";

    /// <summary>The movement source type of a task's kind (S1 putaway / S2 pick).</summary>
    public static string SourceTypeOf(WarehouseTaskKind kind) => kind == WarehouseTaskKind.Pick ? PickSourceType : SourceType;

    /// <summary>A pick movement names the allocation's reservation (INVENTORY 1.7.0 validates it, never consumes it); a putaway movement none.</summary>
    public static string? PickReservation(WarehouseTask task) => task.Kind == WarehouseTaskKind.Pick ? task.ReservationId?.ToString("D") : null;

    /// <summary>The task again, read by its own kind (the S1 member reads putaway tasks only).</summary>
    public Task<WarehouseTask?> FindAsync(WarehouseTask task, CancellationToken ct)
        => task.Kind == WarehouseTaskKind.Pick ? _tasks.FindOfKindAsync(task.Id, WarehouseTaskKind.Pick, ct) : _tasks.FindAsync(task.Id, ct);
}
