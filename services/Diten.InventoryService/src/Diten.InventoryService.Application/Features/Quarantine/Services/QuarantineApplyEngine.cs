using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>A target record changed between the read and the write; the work is re-run on fresh records.</summary>
public sealed class QuarantineRetryException() : Exception("A quarantine record changed concurrently.");

/// <summary>
/// Apply engine (pack §8 "Approval → apply", §16 Q1): an APPROVED decision becomes the ledger movements of the stock-effect
/// table (<see cref="QuarantineStockEffects"/>), then the 0175 records follow (Applied event, hold / blocked record, the
/// new blocked records and holds, history).
/// <para><b>CT-Q9 (measured, Q1):</b> the lines cannot share one ledger transaction — <c>IInventoryLedgerRepository.PostAsync</c>
/// opens its own Mongo session and transaction per posting (MongoDB has no nested transactions), and the pack §5 seam is
/// "the same <c>PostAsync</c>". So the engine runs <b>idempotent continue + retry</b>: lines post in order, each under the
/// ledger key <c>quarantine:{decisionId}:{lineNo}</c>; a refused line stops the run with <c>ApplyFailed</c> (posted lines
/// stay posted, the record stays locked); "Yeniden uygula" re-runs with the same keys — a committed line is answered
/// from the ledger and never posted twice. The 0175 records of a successful run are written in ONE 0175 transaction,
/// guarded by the once-only <c>Applied</c> event.</para>
/// </summary>
public sealed class QuarantineApplyEngine
{
    private const int MaxAttempts = 3;

    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineMovementPoster _poster;
    private readonly QuarantineSupport _support;

    public QuarantineApplyEngine(
        IQuarantineRepository repository,
        IQuarantineDecisionRepository decisions,
        QuarantineMovementPoster poster,
        QuarantineSupport support)
    {
        _repository = repository;
        _decisions = decisions;
        _poster = poster;
        _support = support;
    }

    public sealed record ApplyResult(DecisionState State, string? ErrorCode, string? Message);

    /// <summary>Posts the decision's movements as <paramref name="actorUserId"/> (the approver) and records the result.</summary>
    public async Task<ApplyResult> ApplyAsync(QuarantineDecision decision, Guid actorUserId, CancellationToken ct)
    {
        var state = QuarantineFacts.StateOf(await _decisions.EventsAsync([decision.Id], ct));
        if (state == DecisionState.Applied)
        {
            return new ApplyResult(DecisionState.Applied, null, null);
        }

        if (state is not (DecisionState.Approved or DecisionState.ApplyFailed))
        {
            return new ApplyResult(state, QuarantineErrorCodes.DecisionNotApplicable, $"A {state} decision is not applied.");
        }

        var envelope = new MovementEnvelope(
            decision.ItemId, decision.SkuId, decision.SkuLevel, decision.LotId, decision.BaseUomId,
            QuarantineContract.SourceTypeDecision, decision.DecisionNumber, decision.Id.ToString("D"),
            decision.BlockReasonCode ?? decision.Disposition?.Kind.ToString());

        var posted = new List<(PlannedMovement Movement, string TransactionId)>();
        foreach (var movement in QuarantineStockEffects.Plan(decision))
        {
            var result = await _poster.PostAsync(movement, envelope, QuarantineMovementPoster.DecisionKey(decision.Id, movement.LineNo), actorUserId, ct);
            if (!result.Posted)
            {
                // Q2 (runtime probe): a concurrent applier of the same decision may have committed this line's key a moment ago;
                // the ledger then refuses the duplicate on stock, not on the key. Applied by then → that is the answer, no failure.
                return await RecordFailureAsync(decision, actorUserId, result, posted.Select(p => p.TransactionId).ToList(), ct)
                    ? new ApplyResult(DecisionState.ApplyFailed, result.ErrorCode, result.Message)
                    : new ApplyResult(DecisionState.Applied, null, null);
            }

            posted.Add((movement, result.TransactionId!));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RecordAppliedAsync(decision, actorUserId, posted, ct);
                return new ApplyResult(DecisionState.Applied, null, null);
            }
            catch (QuarantineRetryException) when (attempt < MaxAttempts)
            {
                // a hold / blocked record changed between read and write: re-read and write again
            }
            catch (QuarantineDuplicateException)
            {
                // another applier recorded Applied first (once-only event) — nothing more to do
                return new ApplyResult(DecisionState.Applied, null, null);
            }
        }
    }

    /// <summary>Records ApplyFailed — unless the decision is Applied already (a concurrent applier won); false then.</summary>
    private async Task<bool> RecordFailureAsync(
        QuarantineDecision decision, Guid actorUserId, PostedMovement result, IReadOnlyList<string> postedIds, CancellationToken ct)
    {
        var now = QuarantineSupport.Now();
        var failed = QuarantineSupport.Event(decision.Id, DecisionEventKind.ApplyFailed, now, actorUserId, result.ErrorCode, result.Message);
        failed.MovementIds = [.. postedIds];
        Stamp(failed, decision);
        var history = _support.History(SubjectOf(decision), TargetId(decision), HistoryAction.ApplyFailed, now,
            $"{result.ErrorCode}: {result.Message}", decision.Id, actorUserId);
        Stamp(history, decision);
        return await _repository.InTransactionAsync(async (session, c) =>
        {
            if (QuarantineFacts.StateOf(await session.DecisionEventsAsync(decision.Id, c)) == DecisionState.Applied)
            {
                return false;
            }

            await session.InsertDecisionEventAsync(failed, c);
            await session.InsertHistoryAsync([history], c);
            return true;
        }, ct);
    }

    private async Task RecordAppliedAsync(
        QuarantineDecision decision, Guid actorUserId, IReadOnlyList<(PlannedMovement Movement, string TransactionId)> posted, CancellationToken ct)
    {
        var now = QuarantineSupport.Now();
        var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, ct) : null;
        var blocked = decision.BlockedStockId is { } blockedId ? await _repository.FindBlockedAsync(blockedId, ct) : null;

        await _repository.InTransactionAsync<bool>(async (session, c) =>
        {
            if (QuarantineFacts.StateOf(await session.DecisionEventsAsync(decision.Id, c)) == DecisionState.Applied)
            {
                return false;
            }

            var applied = QuarantineSupport.Event(decision.Id, DecisionEventKind.Applied, now, actorUserId);
            applied.MovementIds = posted.Select(p => p.TransactionId).ToList();
            Stamp(applied, decision);
            await session.InsertDecisionEventAsync(applied, c);

            var history = new List<QuarantineHistoryEntry>();
            switch (decision.Kind)
            {
                case DecisionKind.HoldDecision:
                    await ApplyHoldDecisionAsync(session, decision, hold!, actorUserId, now, history, c);
                    break;
                case DecisionKind.Disposition:
                    await ApplyDispositionAsync(session, decision, blocked!, actorUserId, now, posted, history, c);
                    break;
                case DecisionKind.Unblock:
                    await ApplyUnblockAsync(session, decision, blocked!, actorUserId, now, history, c);
                    break;
                default:
                    throw new NotSupportedException($"Decision kind {decision.Kind} is applied from slice Q4 (KQ-I).");
            }

            foreach (var entry in history) Stamp(entry, decision);
            await session.InsertHistoryAsync(history, c);
            return true;
        }, ct);
    }

    private async Task ApplyHoldDecisionAsync(
        IQuarantineWriteSession session, QuarantineDecision decision, QuarantineHold hold, Guid actor, DateTimeOffset now,
        List<QuarantineHistoryEntry> history, CancellationToken ct)
    {
        var expected = hold.Version;
        hold.Status = HoldStatus.Decided;
        hold.DecidedAtUtc = now;
        hold.ActiveDecisionId = null;
        hold.UpdatedAt = now;
        hold.Version = expected + 1;
        if (!await session.ReplaceHoldAsync(hold, expected, ct)) throw new QuarantineRetryException();
        history.Add(_support.History(HistorySubject.Hold, hold.Id, HistoryAction.Applied, now, decision.Reason, decision.Id, actor));

        foreach (var line in decision.Lines.Where(l => l.Decision is LineDecision.Reject or LineDecision.Block).OrderBy(l => l.LineNo))
        {
            var rejected = line.Decision == LineDecision.Reject;
            var record = NewBlocked(decision, BlockedOrigin.QuarantineDecision, line.Quantity, decision.LocationId, now, actor);
            record.BlockedNumber = await session.NextNumberAsync(QuarantineContract.BlockedPrefix, now.UtcDateTime.Year, ct);
            record.OriginHoldId = hold.Id;
            record.OriginLineNo = line.LineNo;
            record.SerialIds = line.SerialIds;
            record.PendingDisposition = rejected; // Reject → waits for a disposition, review done (pack §8)
            record.Reviewed = rejected;           // Block → review still to do
            await session.InsertBlockedAsync(record, ct);
            history.Add(_support.History(HistorySubject.Blocked, record.Id, HistoryAction.Blocked, now, decision.Reason, decision.Id, actor));
        }
    }

    private async Task ApplyDispositionAsync(
        IQuarantineWriteSession session, QuarantineDecision decision, BlockedStock blocked, Guid actor, DateTimeOffset now,
        IReadOnlyList<(PlannedMovement Movement, string TransactionId)> posted, List<QuarantineHistoryEntry> history, CancellationToken ct)
    {
        var disposition = decision.Disposition!;
        var expected = blocked.Version;
        blocked.Quantity -= disposition.Quantity;
        if (blocked.SerialIds is not null && disposition.SerialIds is not null)
        {
            blocked.SerialIds = blocked.SerialIds.Except(disposition.SerialIds, StringComparer.Ordinal).ToList();
        }

        blocked.ActiveDecisionId = null;
        if (blocked.Quantity <= 0m)
        {
            blocked.Quantity = 0m;
            blocked.Status = BlockedStatus.Closed;
            blocked.PendingDisposition = false;
        }

        blocked.UpdatedAt = now;
        blocked.Version = expected + 1;
        if (!await session.ReplaceBlockedAsync(blocked, expected, ct)) throw new QuarantineRetryException();
        history.Add(_support.History(HistorySubject.Blocked, blocked.Id,
            disposition.Kind == DispositionKind.Sample ? HistoryAction.SampleRetained : HistoryAction.Disposed, now, decision.Reason, decision.Id, actor));

        switch (disposition.Kind)
        {
            case DispositionKind.Rework:
            {
                // BLOCKED → QUARANTINE posted: the quantity is a new hold (Source Rework), linked to the rework movement.
                var hold = new QuarantineHold
                {
                    Id = Guid.NewGuid(),
                    TenantId = _support.TenantId,
                    LegalEntityId = decision.LegalEntityId,
                    CreatedAt = now,
                    HoldNumber = await session.NextNumberAsync(QuarantineContract.HoldPrefix, now.UtcDateTime.Year, ct),
                    ItemId = decision.ItemId,
                    SkuId = decision.SkuId,
                    SkuLevel = decision.SkuLevel,
                    LotId = decision.LotId,
                    WarehouseId = decision.WarehouseId,
                    LocationId = decision.LocationId,
                    StockStatus = StockStatus.Quarantine,
                    Quantity = disposition.Quantity,
                    BaseUomId = decision.BaseUomId,
                    SerialIds = disposition.SerialIds,
                    Source = HoldSource.Rework,
                    SourceMovementId = posted.Single().TransactionId,
                    SourceDocument = new HoldSourceDocument
                        { Module = QuarantineContract.ModuleId, Type = QuarantineContract.SourceTypeDecision, Id = decision.Id.ToString("D"), Number = decision.DecisionNumber },
                    EnteredAtUtc = now,
                    CreatedBy = actor
                };
                await session.InsertHoldAsync(hold, ct);
                history.Add(_support.History(HistorySubject.Hold, hold.Id, HistoryAction.Quarantined, now, decision.Reason, decision.Id, actor));
                break;
            }

            case DispositionKind.Sample:
            {
                // KQ-E: the stock stays BLOCKED at the sample location — a blocked record in state SampleRetained, no new status.
                var sample = NewBlocked(decision, blocked.Origin, disposition.Quantity, disposition.SampleLocationId!, now, actor);
                sample.BlockedNumber = await session.NextNumberAsync(QuarantineContract.BlockedPrefix, now.UtcDateTime.Year, ct);
                sample.BlockReasonCode = blocked.BlockReasonCode;
                sample.SerialIds = disposition.SerialIds;
                sample.OriginHoldId = blocked.OriginHoldId;
                sample.Status = BlockedStatus.SampleRetained;
                sample.Reviewed = true;
                sample.PendingDisposition = false;
                await session.InsertBlockedAsync(sample, ct);
                history.Add(_support.History(HistorySubject.Blocked, sample.Id, HistoryAction.SampleRetained, now, decision.Reason, decision.Id, actor));
                break;
            }
        }
    }

    private async Task ApplyUnblockAsync(
        IQuarantineWriteSession session, QuarantineDecision decision, BlockedStock blocked, Guid actor, DateTimeOffset now,
        List<QuarantineHistoryEntry> history, CancellationToken ct)
    {
        var expected = blocked.Version;
        blocked.Quantity = 0m;
        blocked.SerialIds = blocked.SerialIds is null ? null : [];
        blocked.Status = BlockedStatus.Closed;
        blocked.PendingDisposition = false;
        blocked.ActiveDecisionId = null;
        blocked.UpdatedAt = now;
        blocked.Version = expected + 1;
        if (!await session.ReplaceBlockedAsync(blocked, expected, ct)) throw new QuarantineRetryException();
        history.Add(_support.History(HistorySubject.Blocked, blocked.Id, HistoryAction.Unblocked, now, decision.Reason, decision.Id, actor));
    }

    private BlockedStock NewBlocked(QuarantineDecision decision, BlockedOrigin origin, decimal quantity, string locationId, DateTimeOffset now, Guid actor) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _support.TenantId,
        LegalEntityId = decision.LegalEntityId,
        CreatedAt = now,
        ItemId = decision.ItemId,
        SkuId = decision.SkuId,
        SkuLevel = decision.SkuLevel,
        LotId = decision.LotId,
        WarehouseId = decision.WarehouseId,
        LocationId = locationId,
        Quantity = quantity,
        BaseUomId = decision.BaseUomId,
        BlockReasonCode = decision.BlockReasonCode,
        BlockedAtUtc = now,
        Origin = origin,
        OriginDecisionId = decision.Id,
        CreatedBy = actor
    };

    public static HistorySubject SubjectOf(QuarantineDecision decision) => decision.HoldId is not null ? HistorySubject.Hold : HistorySubject.Blocked;

    public static Guid TargetId(QuarantineDecision decision) => decision.HoldId ?? decision.BlockedStockId ?? throw new InvalidOperationException("Decision without a target.");

    private void Stamp(Diten.InventoryService.Domain.Entities.EntityBase entity, QuarantineDecision decision)
    {
        entity.TenantId = _support.TenantId;
        entity.LegalEntityId = decision.LegalEntityId;
    }
}

/// <summary>
/// The outcome of an approval (pack §8 / contract §5): approved → four-eyes check (V17, KQ-C defence in depth, CT-Q13) →
/// <see cref="QuarantineApplyEngine"/>; rejected / cancelled / timed-out → no movement, the record is reopened. Applying the
/// same outcome twice changes nothing (once-only events + idempotent ledger keys). An outcome is accepted only for the
/// approval instance the decision started (CRM trust rule).
/// </summary>
public sealed class QuarantineOutcomeApplier
{
    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineApplyEngine _engine;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    /// <summary>How long a fresh Approved is treated as "the approving call is still applying" (Q2 runtime probe).</summary>
    public static readonly TimeSpan InFlightGrace = TimeSpan.FromMinutes(2);

    public QuarantineOutcomeApplier(
        IQuarantineRepository repository,
        IQuarantineDecisionRepository decisions,
        QuarantineApplyEngine engine,
        QuarantineFacts facts,
        QuarantineSupport support)
    {
        _repository = repository;
        _decisions = decisions;
        _engine = engine;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<DecisionState>> ApplyOutcomeAsync(
        Guid decisionId, ApprovalOutcome outcome, Guid approverUserId, string? instanceId, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(decisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionState>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        var events = await _decisions.EventsAsync([decisionId], ct);
        var state = QuarantineFacts.StateOf(events);
        var started = events.Where(e => e.Kind == DecisionEventKind.WorkflowStarted).Select(e => e.WorkflowInstanceId).LastOrDefault();
        if (instanceId is not null && !string.Equals(instanceId, started, StringComparison.Ordinal))
        {
            return QuarantineSupport.Fail<DecisionState>(QuarantineErrorCodes.DecisionNotPending,
                "The outcome names another approval instance than the one this decision started.", 409);
        }

        if (state == DecisionState.Approved && outcome == ApprovalOutcome.Approved)
        {
            // approved before, the apply did not finish (crash between the steps): finish it — same keys, no double post. A
            // FRESH Approved is the approving call still posting (Q2 runtime probe: parallel reads) — leave it to that call.
            var approvedEvent = events.Last(e => e.Kind == DecisionEventKind.Approved);
            if (QuarantineSupport.Now() - approvedEvent.AtUtc < InFlightGrace)
            {
                return Response<DecisionState>.Success(DecisionState.Approved);
            }

            return Response<DecisionState>.Success((await _engine.ApplyAsync(decision, approvedEvent.ApproverUserId ?? approverUserId, ct)).State);
        }

        if (state != DecisionState.PendingApproval)
        {
            return state is DecisionState.Applied or DecisionState.Rejected or DecisionState.Cancelled or DecisionState.TimedOut
                or DecisionState.FourEyesViolation or DecisionState.ApplyFailed or DecisionState.Withdrawn
                ? Response<DecisionState>.Success(state) // terminal / already handled: the repeat changes nothing
                : QuarantineSupport.Fail<DecisionState>(QuarantineErrorCodes.DecisionNotPending, $"A {state} decision has no approval to finish.", 409);
        }

        var now = QuarantineSupport.Now();
        if (outcome != ApprovalOutcome.Approved)
        {
            var kind = outcome switch
            {
                ApprovalOutcome.Rejected => DecisionEventKind.Rejected,
                ApprovalOutcome.Cancelled => DecisionEventKind.Cancelled,
                _ => DecisionEventKind.TimedOut
            };
            var closing = Approval(decision, kind, now, approverUserId, started, outcome);
            await CloseWithoutMovementAsync(decision, approverUserId, now, [closing],
                kind switch
                {
                    DecisionEventKind.Rejected => HistoryAction.Rejected,
                    DecisionEventKind.Cancelled => HistoryAction.Cancelled,
                    _ => HistoryAction.TimedOut
                }, ct);
            return Response<DecisionState>.Success(QuarantineFacts.StateOf([.. events, closing]));
        }

        var approved = Approval(decision, DecisionEventKind.Approved, now, approverUserId, started, outcome);
        var settings = await _facts.SettingsAsync(ct);
        if (settings.FourEyes && approverUserId == decision.DeciderUserId)
        {
            // V17 / KQ-C: the decider approved their own decision — nothing is posted, the record needs a new decision.
            var violation = QuarantineSupport.Event(decision.Id, DecisionEventKind.FourEyesViolation, now, approverUserId,
                QuarantineErrorCodes.DecisionNotApplicable, "The approver is the decider (four eyes).");
            Stamp(violation, decision);
            await CloseWithoutMovementAsync(decision, approverUserId, now, [approved, violation], HistoryAction.FourEyesViolation, ct);
            return Response<DecisionState>.Success(DecisionState.FourEyesViolation);
        }

        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                await session.InsertDecisionEventAsync(approved, c);
                var history = _support.History(QuarantineApplyEngine.SubjectOf(decision), QuarantineApplyEngine.TargetId(decision),
                    HistoryAction.Approved, now, null, decision.Id, approverUserId);
                Stamp(history, decision);
                await session.InsertHistoryAsync([history], c);
                return true;
            }, ct);
        }
        catch (QuarantineDuplicateException)
        {
            // a concurrent outcome recorded Approved first (once-only event): that call applies — this one reports the state
            return Response<DecisionState>.Success(QuarantineFacts.StateOf(await _decisions.EventsAsync([decisionId], ct)));
        }

        return Response<DecisionState>.Success((await _engine.ApplyAsync(decision, approverUserId, ct)).State);
    }

    /// <summary>Records the closing events, reopens the hold / blocked record and writes the history — no movement.</summary>
    private async Task CloseWithoutMovementAsync(
        QuarantineDecision decision, Guid by, DateTimeOffset now, IReadOnlyList<QuarantineDecisionEvent> closing, HistoryAction action, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, ct) : null;
                var blocked = decision.BlockedStockId is { } blockedId ? await _repository.FindBlockedAsync(blockedId, ct) : null;
                await _repository.InTransactionAsync<bool>(async (session, c) =>
                {
                    foreach (var e in closing) await session.InsertDecisionEventAsync(e, c);
                    await QuarantineReopen.ReopenAsync(session, hold, blocked, decision.Id, now, c);
                    var history = _support.History(QuarantineApplyEngine.SubjectOf(decision), QuarantineApplyEngine.TargetId(decision), action, now, null, decision.Id, by);
                    Stamp(history, decision);
                    await session.InsertHistoryAsync([history], c);
                    return true;
                }, ct);
                return;
            }
            catch (QuarantineRetryException) when (attempt < 3)
            {
            }
            catch (QuarantineDuplicateException)
            {
                return; // the same outcome was recorded by a concurrent caller
            }
        }
    }

    private QuarantineDecisionEvent Approval(
        QuarantineDecision decision, DecisionEventKind kind, DateTimeOffset at, Guid approver, string? instanceId, ApprovalOutcome outcome)
    {
        var e = QuarantineSupport.Event(decision.Id, kind, at, approver);
        e.ApproverUserId = approver;
        e.WorkflowInstanceId = instanceId;
        e.WorkflowOutcome = outcome switch
        {
            ApprovalOutcome.Approved => "approved",
            ApprovalOutcome.Rejected => "rejected",
            ApprovalOutcome.Cancelled => "cancelled",
            _ => "timed-out"
        };
        Stamp(e, decision);
        return e;
    }

    private void Stamp(Diten.InventoryService.Domain.Entities.EntityBase entity, QuarantineDecision decision)
    {
        entity.TenantId = _support.TenantId;
        entity.LegalEntityId = decision.LegalEntityId;
    }
}

/// <summary>Contract §5: on every terminal state except Applied the hold / blocked record is reopened (the lock is released).</summary>
public static class QuarantineReopen
{
    public static async Task ReopenAsync(
        IQuarantineWriteSession session, QuarantineHold? hold, BlockedStock? blocked, Guid decisionId, DateTimeOffset now, CancellationToken ct)
    {
        if (hold is not null && hold.ActiveDecisionId == decisionId)
        {
            var expected = hold.Version;
            hold.ActiveDecisionId = null;
            hold.Status = HoldStatus.Open;
            hold.UpdatedAt = now;
            hold.Version = expected + 1;
            if (!await session.ReplaceHoldAsync(hold, expected, ct)) throw new QuarantineRetryException();
        }

        if (blocked is not null && blocked.ActiveDecisionId == decisionId)
        {
            var expected = blocked.Version;
            blocked.ActiveDecisionId = null;
            blocked.UpdatedAt = now;
            blocked.Version = expected + 1;
            if (!await session.ReplaceBlockedAsync(blocked, expected, ct)) throw new QuarantineRetryException();
        }
    }
}
