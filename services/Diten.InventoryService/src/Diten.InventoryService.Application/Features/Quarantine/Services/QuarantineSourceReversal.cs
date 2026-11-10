using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175-FU01 (pack §8.2, §8.3) — what both <c>ApplySourceReversalTo{Hold,Blocked}</c> handlers do. Reads the REVERSAL and its
/// original from the ledger (caller's tenant + company), the record by its source movement, its active decision and the facts
/// <see cref="SourceReversalRules"/> needs: the decision's posted line keys (the ledger, never the events), the lot history row of an
/// applied lot step (MOD-0174, read only). Exactly once: a recorded close / mark answers <c>NoChange</c> without a transaction; the
/// change is ONE quarantine transaction that writes the record against the version it read (the lock it shares with every decision
/// writer, §8.6) — a lost write re-reads and re-applies the table (≤ 3), and its central record rolls back with it (K2). Writes no stock.
/// </summary>
public sealed class QuarantineSourceReversal
{
    private const int MaxAttempts = 3;

    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly IInventoryLedgerRepository _ledger;
    private readonly ILotRepository _lots;
    private readonly QuarantineSupport _support;
    private readonly ILogger<QuarantineSourceReversal> _logger;

    public QuarantineSourceReversal(
        IQuarantineRepository repository,
        IQuarantineDecisionRepository decisions,
        IInventoryLedgerRepository ledger,
        ILotRepository lots,
        QuarantineSupport support,
        ILogger<QuarantineSourceReversal> logger)
    {
        _repository = repository;
        _decisions = decisions;
        _ledger = ledger;
        _lots = lots;
        _support = support;
        _logger = logger;
    }

    /// <summary>The record a source-reversal command acts on: a hold or a blocked-stock record, behind one shape.</summary>
    private sealed class Target(QuarantineHold? hold, BlockedStock? blocked)
    {
        public QuarantineHold? Hold { get; } = hold;
        public BlockedStock? Blocked { get; } = blocked;
        public HistorySubject Subject => Hold is not null ? HistorySubject.Hold : HistorySubject.Blocked;
        public Guid Id => Hold is not null ? Hold.Id : Blocked!.Id;
        public string Number => Hold is not null ? Hold.HoldNumber : Blocked!.BlockedNumber;
        public int Version => Hold is not null ? Hold.Version : Blocked!.Version;
        public Guid? ActiveDecisionId => Hold is not null ? Hold.ActiveDecisionId : Blocked!.ActiveDecisionId;
        public RecordVoid? Voided => Hold is not null ? Hold.Voided : Blocked!.Voided;
        public SourceReversalMark? Conflict => Hold is not null ? Hold.SourceReversalConflict : Blocked!.SourceReversalConflict;
        public string Status => Hold is not null ? Hold.Status.ToString() : Blocked!.Status.ToString();
    }

    public async Task<Response<SourceReversalResultDto>> ApplyAsync(HistorySubject subject, string reversalTransactionId, CancellationToken ct)
    {
        // V2: the REVERSAL, in the caller's tenant + company only
        var reversal = Guid.TryParse(reversalTransactionId, out var reversalId) ? await _ledger.FindTransactionAsync(reversalId, ct) : null;
        if (reversal is null || reversal.MovementType != MovementType.Reversal || reversal.ReversesTransactionId is not { } originalId)
        {
            _logger.LogWarning("quarantine.source_reversal.not_a_reversal Transaction={Transaction}", reversalTransactionId);
            return Result(SourceReversalVerdict.NoChange, SourceReversalNoChange.NoRecord, "V2", null);
        }

        var original = await _ledger.FindTransactionAsync(originalId, ct);
        var sourceMovementId = originalId.ToString("D");
        var lost = false;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // V3: the record of this source movement, in its own tenant + company
            var target = subject == HistorySubject.Hold
                ? await _repository.FindHoldBySourceMovementAsync(sourceMovementId, ct) is { } h ? new Target(h, null) : null
                : await _repository.FindBlockedBySourceMovementAsync(sourceMovementId, ct) is { } b ? new Target(null, b) : null;
            if (target is null)
            {
                return Result(SourceReversalVerdict.NoChange, SourceReversalNoChange.NoRecord, "V3", null);
            }

            var (facts, active) = await FactsAsync(target, reversal, original, ct);
            var outcome = SourceReversalRules.Evaluate(facts);
            if (outcome.Verdict == SourceReversalVerdict.NoChange)
            {
                if (outcome.NoChange == SourceReversalNoChange.Unmatched)
                {
                    _logger.LogError("quarantine.source_reversal.unmatched Record={Record} RecordStatus={Status} DecisionState={State} Reversal={Reversal}",
                        target.Number, target.Status, facts.ActiveDecisionState?.ToString() ?? "-", reversal.TransactionNumber);
                }

                var reason = lost && outcome.NoChange == SourceReversalNoChange.AlreadyRecorded ? SourceReversalNoChange.LostRace : outcome.NoChange;
                return Result(SourceReversalVerdict.NoChange, reason, outcome.Rule, target);
            }

            try
            {
                await WriteAsync(target, outcome, active, reversal, ct);
            }
            catch (Exception ex) when (ex is QuarantineRetryException or QuarantineDuplicateException)
            {
                lost = true; // another writer changed the record first: read again, apply the table again
                continue;
            }

            if (outcome.VoidsAndMarks)
            {
                _logger.LogWarning("quarantine.source_reversal.lot_changed Record={Record} Decision={Decision} Lot={Lot} Reversal={Reversal}",
                    target.Number, active?.Id, active?.LotId, reversal.TransactionNumber);
            }

            return Result(outcome.Verdict, null, outcome.Rule, target);
        }

        _logger.LogError("quarantine.source_reversal.retries_exhausted Subject={Subject} Reversal={Reversal}", subject, reversal.TransactionNumber);
        return QuarantineSupport.Fail<SourceReversalResultDto>(QuarantineErrorCodes.Conflict,
            "The record kept changing while the source reversal was applied; the sweep retries it.", 409);
    }

    private async Task<(SourceReversalFacts Facts, QuarantineDecision? Active)> FactsAsync(
        Target target, InventoryTransaction reversal, InventoryTransaction? original, CancellationToken ct)
    {
        bool? originalIsQuarantine = original is null ? null : string.Equals(original.SourceModule, QuarantineContract.ModuleId, StringComparison.Ordinal);
        var (origin, mismatch) = target.Hold is { } hold
            ? SourceReversalRules.OriginOf(hold.Source, originalIsQuarantine)
            : SourceReversalRules.OriginOf(target.Blocked!.Origin, originalIsQuarantine);
        if (mismatch)
        {
            _logger.LogWarning("quarantine.source_reversal.origin_fail_closed Record={Record} Origin={Origin} OriginalModule={Module}",
                target.Number, target.Hold?.Source.ToString() ?? target.Blocked!.Origin.ToString(), original?.SourceModule ?? "-");
        }

        var active = target.ActiveDecisionId is { } activeId ? await _decisions.FindAsync(activeId, ct) : null;
        DecisionState? state = active is null ? null : QuarantineFacts.StateOf(await _decisions.EventsAsync([active.Id], ct));
        var changesLot = target.Hold is not null && active is not null && QuarantineApplyEngine.ChangesLot(active);
        var lotStepApplied = changesLot && Guid.TryParse(active!.LotId, out var lotId)
                             && await _lots.FindStatusChangeByKeyAsync(lotId, ApplyApprovedLotStatusChangeHandler.Key(active.Id), ct) is not null;

        var reversalKey = reversal.Id.ToString("D");
        var voidedPosted = 0;
        if (target.Voided is { DecisionId: { } voidedDecisionId } voided && target.Conflict is null
            && string.Equals(voided.ReversalTransactionId, reversalKey, StringComparison.Ordinal)
            && await _decisions.FindAsync(voidedDecisionId, ct) is { } voidedDecision)
        {
            voidedPosted = await PostedLinesAsync(voidedDecision, ct);
        }

        return (new SourceReversalFacts
        {
            Subject = target.Subject,
            Origin = origin,
            HoldStatus = target.Hold?.Status ?? default,
            BlockedStatus = target.Blocked?.Status ?? default,
            ReversalTransactionId = reversalKey,
            VoidedReversalTransactionId = target.Voided?.ReversalTransactionId,
            VoidedDecisionId = target.Voided?.DecisionId,
            VoidedDecisionPostedLines = voidedPosted,
            ConflictReversalTransactionId = target.Conflict?.ReversalTransactionId,
            ActiveDecisionId = target.ActiveDecisionId,
            ActiveDecisionState = state,
            ActiveDecisionPostedLines = active is null ? 0 : await PostedLinesAsync(active, ct),
            ActiveDecisionChangesLot = changesLot,
            LotStepApplied = lotStepApplied,
            Quantity = target.Hold?.Quantity ?? target.Blocked!.Quantity,
            OriginalQuantity = original?.BaseQuantity ?? 0m
        }, active);
    }

    /// <summary>"Posted line" = the ledger holds the line's key <c>quarantine:{decisionId}:{lineNo}</c> (events may lag the posting).</summary>
    private async Task<int> PostedLinesAsync(QuarantineDecision decision, CancellationToken ct)
    {
        var posted = 0;
        foreach (var lineNo in QuarantineStockEffects.Plan(decision).Select(m => m.LineNo).Distinct())
        {
            if (await _ledger.FindByIdempotencyKeyAsync(QuarantineMovementPoster.DecisionKey(decision.Id, lineNo), ct) is not null)
            {
                posted++;
            }
        }

        return posted;
    }

    private async Task WriteAsync(Target target, SourceReversalOutcome outcome, QuarantineDecision? active, InventoryTransaction reversal, CancellationToken ct)
    {
        var now = QuarantineSupport.Now();
        var expected = target.Version;
        var activeId = target.ActiveDecisionId;
        var reversalKey = reversal.Id.ToString("D");
        var mark = outcome.Cause is { } cause
            ? new SourceReversalMark
            {
                AtUtc = now, ReversalTransactionId = reversalKey, ReversalTransactionNumber = reversal.TransactionNumber,
                ReversedByUserId = reversal.CreatedBy, Cause = cause
            }
            : null;

        QuarantineHistoryEntry Row(HistoryAction action, Guid? decisionId)
        {
            var row = _support.History(target.Subject, target.Id, action, now, reversal.TransactionNumber, decisionId, reversal.CreatedBy);
            row.CorrelationId = reversal.CorrelationId;
            row.ReversalTransactionId = reversalKey;
            return row;
        }

        await _repository.InTransactionAsync<bool>(async (session, c) =>
        {
            var history = new List<QuarantineHistoryEntry>();
            RecordVoid? voided = null;
            if (outcome.Verdict == SourceReversalVerdict.Voided)
            {
                if (activeId is { } decisionId && active is not null)
                {
                    await session.InsertDecisionEventAsync(QuarantineSupport.Event(decisionId, DecisionEventKind.Voided, now, reversal.CreatedBy, null, reversal.TransactionNumber), c);
                }

                voided = new RecordVoid
                {
                    AtUtc = now, Reason = VoidReason.SourceReversed, ReversalTransactionId = reversalKey, ReversalTransactionNumber = reversal.TransactionNumber,
                    ReversedByUserId = reversal.CreatedBy, DecisionId = activeId
                };
                history.Add(Row(HistoryAction.SourceReversed, activeId));
            }

            if (mark is not null)
            {
                history.Add(Row(HistoryAction.SourceReversalConflict, activeId ?? target.Voided?.DecisionId));
            }

            bool written;
            if (target.Hold is { } hold)
            {
                if (voided is not null)
                {
                    hold.Status = HoldStatus.Voided;
                    hold.Voided = voided;
                    hold.ActiveDecisionId = null;
                }

                hold.SourceReversalConflict = mark ?? hold.SourceReversalConflict;
                hold.UpdatedAt = now;
                hold.Version = expected + 1;
                written = await session.ReplaceHoldAsync(hold, expected, c);
            }
            else
            {
                var blocked = target.Blocked!;
                if (voided is not null)
                {
                    blocked.Status = BlockedStatus.Voided;
                    blocked.Voided = voided;
                    blocked.ActiveDecisionId = null;
                    blocked.PendingDisposition = false;
                }

                blocked.SourceReversalConflict = mark ?? blocked.SourceReversalConflict;
                blocked.UpdatedAt = now;
                blocked.Version = expected + 1;
                written = await session.ReplaceBlockedAsync(blocked, expected, c);
            }

            if (!written)
            {
                throw new QuarantineRetryException();
            }

            await session.InsertHistoryAsync(history, c);
            return true;
        }, ct);
    }

    private static Response<SourceReversalResultDto> Result(SourceReversalVerdict verdict, SourceReversalNoChange? noChange, string rule, Target? target)
        => Response<SourceReversalResultDto>.Success(new SourceReversalResultDto(verdict, noChange, rule, target?.Id, target?.Number));
}
