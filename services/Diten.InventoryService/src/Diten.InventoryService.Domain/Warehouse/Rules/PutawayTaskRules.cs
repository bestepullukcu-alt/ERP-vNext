using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>The outcome a posting attempt wants to write (pack §8.3 step 4 "INVENTORY cevap eşlemesi").</summary>
public enum AttemptOutcome
{
    Posted,
    Refused,
    KeyReused,
    HolderReversed
}

/// <summary>
/// What a result write does after reading the task (pack §8.3 step 4 "tek kural" (A)–(E), flow rows 25–28). <see cref="Write"/> = the
/// confirmation is still Pending and this attempt owns it ((A) on a re-read); the rest write nothing except <see cref="MarkDiverged"/> ((D)).
/// </summary>
public enum ResultDecision
{
    Write,

    /// <summary>(B) the confirmation already holds this attempt's result (Posted, same transaction) — no change, the current task answers.</summary>
    AlreadyDone,

    /// <summary>(C1) another attempt owns the confirmation and its lock is valid → 409 WMS_CONFIRMATION_IN_FLIGHT.</summary>
    InFlightElsewhere,

    /// <summary>(C2) another attempt owns it, its lock expired → 409 WMS_CONFLICT + the current task.</summary>
    OwnedByExpiredAttempt,

    /// <summary>(D) this attempt got Posted / a replay and the confirmation is Refused → IntegrityError (StatusDiverged).</summary>
    MarkDiverged,

    /// <summary>(D) the confirmation is already IntegrityError — no write, PriorResolution kept → 409 WMS_POSTING_INTEGRITY_ERROR.</summary>
    AlreadyIntegrityError,

    /// <summary>(E) this attempt's result is Refused / IntegrityError and the confirmation is no longer Pending → 409 WMS_CONFLICT.</summary>
    NotPending
}

/// <summary>
/// The pure rules of a putaway task (pack §8.3, §8.13, §8.15, §8.16; WMS draft §6.1). No I/O, no clock of its own: the handler reads, these
/// rules decide, the repository writes with a version compare-and-set.
/// </summary>
public static class PutawayTaskRules
{
    public const int MaxNoteLength = 1000;
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 1000;
    public const int MaxScanRefLength = 200;

    /// <summary>The CAS retries of one result write before 503 WMS_CONFIRMATION_CONFLICT_RETRY (≤ 3, flow row 25).</summary>
    public const int MaxResultWriteAttempts = 3;

    /// <summary>The defensive sweep looks back this far (§8.3 "son 30 dk").</summary>
    public static readonly TimeSpan DefensiveWindow = TimeSpan.FromMinutes(30);

    /// <summary>§4.2 <c>PostingKey</c>.</summary>
    public static string PostingKey(Guid taskId, int seq) => $"wms:put:{taskId:D}:{seq}";

    public static bool IsTerminal(WarehouseTaskStatus status) => status is WarehouseTaskStatus.Completed or WarehouseTaskStatus.Cancelled;

    public static bool IsOpen(WarehouseTask task) => !IsTerminal(task.Status);

    public static decimal PendingQuantity(WarehouseTask task) => task.Confirmations.Where(c => c.Status == ConfirmationStatus.Pending).Sum(c => c.Quantity);

    /// <summary>Remaining = planned − confirmed − pending (a Pending confirmation already counts; §4.2).</summary>
    public static decimal Remaining(WarehouseTask task) => Math.Max(0m, task.PlannedQuantity - task.ConfirmedQuantity - PendingQuantity(task));

    /// <summary>The open quantity a task still holds on its source bucket (§8.2: planned − confirmed while open; 0 once terminal).</summary>
    public static decimal OpenHold(WarehouseTask task) => IsOpen(task) ? Math.Max(0m, task.PlannedQuantity - task.ConfirmedQuantity) : 0m;

    public static bool HasPending(WarehouseTask task) => task.Confirmations.Any(c => c.Status == ConfirmationStatus.Pending);

    public static bool HasPosted(WarehouseTask task) => task.Confirmations.Any(c => c.Status == ConfirmationStatus.Posted);

    public static bool HasIntegrityError(WarehouseTask task) => task.Confirmations.Any(c => c.Status == ConfirmationStatus.IntegrityError);

    public static bool IsUnresolvedRefused(TaskConfirmation c) => c.Status == ConfirmationStatus.Refused && c.Resolution is null;

    /// <summary>
    /// The task status the confirmed quantity gives (M-4): a terminal task keeps its status (a resolve never re-opens it, flow rows 23–24).
    /// </summary>
    public static WarehouseTaskStatus Derive(WarehouseTask task)
        => IsTerminal(task.Status) ? task.Status
            : task.ConfirmedQuantity >= task.PlannedQuantity ? WarehouseTaskStatus.Completed
            : task.ConfirmedQuantity > 0m ? WarehouseTaskStatus.InProgress
            : WarehouseTaskStatus.Open;

    /// <summary>
    /// The conditional increment of transaction 2 (pack §8.3 step 4, AC-67 (5)): a Posted result may only add <paramref name="quantity"/> while
    /// <c>Confirmed + q ≤ Planned</c>; otherwise the confirmation becomes IntegrityError (OverPlanned). A defensive rule — the Pending lock keeps
    /// it from firing in the real flow; the unit test calls it directly.
    /// </summary>
    public static bool CanIncrement(decimal confirmed, decimal quantity, decimal planned) => confirmed + quantity <= planned;

    /// <summary>The flight lock of a confirmation is held and has not expired.</summary>
    public static bool LockValid(TaskConfirmation c, DateTimeOffset now) => c.AttemptId is not null && c.InFlightUntilUtc is { } until && until > now;

    /// <summary>A lock may be taken: the confirmation is Pending and its lock is empty or expired (§4.2).</summary>
    public static bool CanTakeLock(TaskConfirmation c, DateTimeOffset now) => c.Status == ConfirmationStatus.Pending && !LockValid(c, now);

    /// <summary>
    /// The precondition of a result write (pack §8.3 step 4 filter): the confirmation is still Pending AND this attempt owns it — for the
    /// sweeper (no attempt id) its lock is empty or expired.
    /// </summary>
    public static bool MayWriteResult(TaskConfirmation c, Guid? attemptId, DateTimeOffset now)
        => c.Status == ConfirmationStatus.Pending && (attemptId is { } mine ? c.AttemptId == mine : !LockValid(c, now));

    /// <summary>
    /// The one rule after a result write found the confirmation not as expected (pack §8.3 step 4 (A)–(E)). <paramref name="transactionId"/> is
    /// this attempt's ledger transaction when <paramref name="outcome"/> is Posted.
    /// </summary>
    public static ResultDecision Decide(TaskConfirmation c, Guid? attemptId, DateTimeOffset now, AttemptOutcome outcome, string? transactionId)
    {
        if (MayWriteResult(c, attemptId, now)) return ResultDecision.Write;
        if (c.Status == ConfirmationStatus.Pending)
        {
            // (C): Pending but another attempt owns it (a sweeper write never lands here — its filter is "lock empty or expired")
            return LockValid(c, now) ? ResultDecision.InFlightElsewhere : ResultDecision.OwnedByExpiredAttempt;
        }

        if (outcome == AttemptOutcome.Posted)
        {
            if (c.Status == ConfirmationStatus.Posted && string.Equals(c.TransactionId, transactionId, StringComparison.Ordinal)) return ResultDecision.AlreadyDone;
            if (c.Status == ConfirmationStatus.Refused) return ResultDecision.MarkDiverged;
            if (c.Status == ConfirmationStatus.IntegrityError) return ResultDecision.AlreadyIntegrityError;
        }

        return ResultDecision.NotPending;
    }

    /// <summary>
    /// The task's one badge (§4.1; independent reviews 13 F-3, 14 F-1, 15 F-1): IntegrityError &gt; PendingLong &gt; Pending &gt; Refused &gt; None.
    /// Only an UNRESOLVED Refused (no Resolution) raises PostingRefused; a Pending older than <paramref name="longPending"/> is PendingLong.
    /// </summary>
    public static IntegrationFlag Flag(WarehouseTask task, DateTimeOffset now, TimeSpan longPending)
    {
        if (task.Confirmations.Any(c => c.Status == ConfirmationStatus.IntegrityError)) return IntegrationFlag.PostingIntegrityError;
        var pending = task.Confirmations.Where(c => c.Status == ConfirmationStatus.Pending).ToList();
        if (pending.Any(c => now - c.ConfirmedAtUtc > longPending)) return IntegrationFlag.PostingPendingLong;
        if (pending.Count > 0) return IntegrationFlag.PostingPending;
        return task.Confirmations.Any(IsUnresolvedRefused) ? IntegrationFlag.PostingRefused : IntegrationFlag.None;
    }

    /// <summary>A reason (short close, cancel, return, resolve, deviation) is 10–1000 characters after trim.</summary>
    public static bool IsReason(string? reason) => reason is { Length: >= MinReasonLength and <= MaxReasonLength };

    /// <summary>The operator rule (§8.13): the assignee, or a supervisor (holder of <c>warehouse.putaway-tasks.update</c>).</summary>
    public static bool MayAct(WarehouseTask task, Guid userId, bool supervisor) => supervisor || task.AssigneeUserId == userId;

    /// <summary>
    /// Applies a Posted result to the task (flow rows 2 / 3 / 4 / 13 / 25): status, transaction, the confirmed quantity (conditional — false =
    /// OverPlanned), derived status, the serial slot, the lock, and Superseded on the unresolved Refused siblings (an IntegrityError sibling is
    /// skipped). Returns the superseded seqs and whether the task became Completed.
    /// </summary>
    public static (bool Applied, IReadOnlyList<int> Superseded, bool Completed) ApplyPosted(WarehouseTask task, TaskConfirmation c, string transactionId,
        string transactionNumber, bool clearLock)
    {
        if (!CanIncrement(task.ConfirmedQuantity, c.Quantity, task.PlannedQuantity)) return (false, [], false);
        var before = task.Status;
        c.Status = ConfirmationStatus.Posted;
        c.TransactionId = transactionId;
        c.TransactionNumber = transactionNumber;
        if (clearLock) ClearLock(c);
        task.ConfirmedQuantity += c.Quantity;
        var superseded = Supersede(task, c.Seq);
        task.Status = Derive(task);
        if (IsTerminal(task.Status)) task.OpenSerialSlot = null;
        return (true, superseded, before != WarehouseTaskStatus.Completed && task.Status == WarehouseTaskStatus.Completed);
    }

    /// <summary>Superseded: every UNRESOLVED Refused sibling of <paramref name="postedSeq"/> (IntegrityError siblings are skipped).</summary>
    public static IReadOnlyList<int> Supersede(WarehouseTask task, int postedSeq)
    {
        var closed = new List<int>();
        foreach (var sibling in task.Confirmations.Where(s => s.Seq != postedSeq && IsUnresolvedRefused(s)))
        {
            sibling.Resolution = ConfirmationResolution.SupersededByNewConfirmation;
            closed.Add(sibling.Seq);
        }

        return closed;
    }

    public static void ClearLock(TaskConfirmation c)
    {
        c.AttemptId = null;
        c.InFlightUntilUtc = null;
    }

    /// <summary>Refused / IntegrityError → IntegrityError (StatusDiverged): <c>Resolution</c> moves to <c>PriorResolution</c> in the same write.</summary>
    public static void MarkDiverged(TaskConfirmation c)
    {
        c.PriorResolution = c.Resolution;
        c.Resolution = null;
        c.Status = ConfirmationStatus.IntegrityError;
        c.ErrorReason = "StatusDiverged";
        c.ErrorCode = "WMS_POSTING_INTEGRITY_ERROR";
        c.ErrorMessage = "The ledger holds this confirmation's movement although the confirmation was refused.";
        c.ErrorStatus = 409;
        ClearLock(c);
    }
}
