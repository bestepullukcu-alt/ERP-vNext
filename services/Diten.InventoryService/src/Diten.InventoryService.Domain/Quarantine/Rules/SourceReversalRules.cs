using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Rules;

/// <summary>Where a hold / blocked record came from (pack MOD-0175-FU01 §4.1 origin table).</summary>
public enum RecordOrigin
{
    /// <summary>An inbound movement MOD-0175 did not post (intake hold, <c>OtherInbound</c> blocked record): the Void rows apply.</summary>
    Intake,

    /// <summary>A movement of MOD-0175 itself (Manual / Retest / Recall / Rework hold, <c>ManualBlock</c>) — or an origin not in the table (fail-closed).</summary>
    Quarantine
}

public enum SourceReversalVerdict
{
    Voided,
    Conflict,
    NoChange
}

/// <summary>Why a source reversal changed nothing (pack §8.2 "Komut sonucu adlıdır").</summary>
public enum SourceReversalNoChange
{
    AlreadyRecorded,
    LostRace,
    NoRecord,
    Unmatched
}

/// <summary>
/// The facts <see cref="SourceReversalRules.Evaluate"/> reads — gathered by the command handler from the record, its active decision,
/// the ledger (posted line keys) and the lot history (MOD-0174, read only).
/// </summary>
public sealed record SourceReversalFacts
{
    public required HistorySubject Subject { get; init; }

    public required RecordOrigin Origin { get; init; }

    public HoldStatus HoldStatus { get; init; }

    public BlockedStatus BlockedStatus { get; init; }

    /// <summary>The REVERSAL this call is about.</summary>
    public required string ReversalTransactionId { get; init; }

    /// <summary>The record's <c>Voided.ReversalTransactionId</c>, when voided.</summary>
    public string? VoidedReversalTransactionId { get; init; }

    public Guid? VoidedDecisionId { get; init; }

    /// <summary>How many lines of the voided decision the ledger holds (R0a).</summary>
    public int VoidedDecisionPostedLines { get; init; }

    /// <summary>The record's <c>SourceReversalConflict.ReversalTransactionId</c>, when marked.</summary>
    public string? ConflictReversalTransactionId { get; init; }

    public Guid? ActiveDecisionId { get; init; }

    /// <summary>The active decision's state; null when there is none (or it cannot be read).</summary>
    public DecisionState? ActiveDecisionState { get; init; }

    /// <summary>How many lines of the active decision the ledger holds (measured by key, never from events).</summary>
    public int ActiveDecisionPostedLines { get; init; }

    /// <summary>The active decision changes its lot (<c>QuarantineApplyEngine.ChangesLot</c>).</summary>
    public bool ActiveDecisionChangesLot { get; init; }

    /// <summary>The lot history holds the decision's keyed row (<c>mod-0175:{decisionId}</c>) — its lot step was applied (R5a).</summary>
    public bool LotStepApplied { get; init; }

    /// <summary>The record's quantity now.</summary>
    public decimal Quantity { get; init; }

    /// <summary>The source movement's base quantity (a blocked record that lost quantity cannot close, R9).</summary>
    public decimal OriginalQuantity { get; init; }
}

/// <summary>The rule that matched (<c>R0a</c> … <c>R13</c>) and what it means.</summary>
public sealed record SourceReversalOutcome(string Rule, SourceReversalVerdict Verdict, SourceReversalCause? Cause = null, SourceReversalNoChange? NoChange = null)
{
    /// <summary>R5a: the record is voided AND marked in the same transaction.</summary>
    public bool VoidsAndMarks => Verdict == SourceReversalVerdict.Voided && Cause is not null;
}

/// <summary>
/// MOD-0175-FU01 pack §8.3 — the source-reversal rule table, pure. Rows are read top to bottom, the first match wins; a combination
/// no row names is R13 (<c>NoChange (Unmatched)</c>, never Void: fail-closed). Origin is §4.1 (<see cref="OriginOf(HoldSource, bool?)"/>):
/// only an INTAKE record can be voided; a record of a MOD-0175 movement is only ever marked (R1).
/// </summary>
public static class SourceReversalRules
{
    private static readonly HashSet<HoldSource> IntakeHoldSources =
        [HoldSource.GoodsReceipt, HoldSource.Production, HoldSource.CustomerReturn, HoldSource.OtherInbound];

    private static readonly HashSet<HoldSource> QuarantineHoldSources =
        [HoldSource.Manual, HoldSource.Retest, HoldSource.Recall, HoldSource.Rework];

    private static readonly HashSet<BlockedOrigin> IntakeBlockedOrigins = [BlockedOrigin.OtherInbound];

    /// <summary>
    /// §4.1: the hold's <c>Source</c>, cross-checked with the source movement (<paramref name="originalIsQuarantineMovement"/>: its
    /// <c>SourceModule</c> is MOD-0175; null = the movement could not be read). An intake value whose movement is MOD-0175's — or
    /// unreadable — and a value outside the table count as MOD-0175 (<c>Mismatch</c> = true: the caller logs a warning).
    /// </summary>
    public static (RecordOrigin Origin, bool Mismatch) OriginOf(HoldSource source, bool? originalIsQuarantineMovement)
    {
        if (IntakeHoldSources.Contains(source))
        {
            return originalIsQuarantineMovement == false ? (RecordOrigin.Intake, false) : (RecordOrigin.Quarantine, true);
        }

        return (RecordOrigin.Quarantine, !QuarantineHoldSources.Contains(source));
    }

    /// <summary>§4.1 for a blocked record: only <c>OtherInbound</c> is intake; <c>ManualBlock</c> is MOD-0175's; anything else fails closed.</summary>
    public static (RecordOrigin Origin, bool Mismatch) OriginOf(BlockedOrigin origin, bool? originalIsQuarantineMovement)
    {
        if (IntakeBlockedOrigins.Contains(origin))
        {
            return originalIsQuarantineMovement == false ? (RecordOrigin.Intake, false) : (RecordOrigin.Quarantine, true);
        }

        return (RecordOrigin.Quarantine, origin != BlockedOrigin.ManualBlock);
    }

    public static SourceReversalOutcome Evaluate(SourceReversalFacts f)
    {
        var voidedStatus = f.Subject == HistorySubject.Hold ? f.HoldStatus == HoldStatus.Voided : f.BlockedStatus == BlockedStatus.Voided;
        var sameVoid = string.Equals(f.VoidedReversalTransactionId, f.ReversalTransactionId, StringComparison.Ordinal);
        var marked = f.ConflictReversalTransactionId is not null;

        // R0a (r4): a line of the decision voided with the record was posted after the close (§8.6 W5 / W6 window)
        if (f.Origin == RecordOrigin.Intake && voidedStatus && sameVoid && f.VoidedDecisionId is not null && !marked && f.VoidedDecisionPostedLines >= 1)
        {
            return new("R0a", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
        }

        // R0: this reversal is already recorded (the mark, or the close)
        if (string.Equals(f.ConflictReversalTransactionId, f.ReversalTransactionId, StringComparison.Ordinal) || (voidedStatus && sameVoid))
        {
            return new("R0", SourceReversalVerdict.NoChange, NoChange: SourceReversalNoChange.AlreadyRecorded);
        }

        // R12 (defence, G-5): voided by another reversal, or already marked — a mark is written once
        if (voidedStatus || marked)
        {
            return new("R12", SourceReversalVerdict.NoChange, NoChange: SourceReversalNoChange.AlreadyRecorded);
        }

        if (f.Origin == RecordOrigin.Quarantine)
        {
            var known = f.Subject == HistorySubject.Hold
                ? f.HoldStatus is HoldStatus.Open or HoldStatus.DecisionPending or HoldStatus.Decided
                : f.BlockedStatus is BlockedStatus.Open or BlockedStatus.Closed or BlockedStatus.SampleRetained;
            return known ? new("R1", SourceReversalVerdict.Conflict, SourceReversalCause.OwnMovementReversed) : Unmatched;
        }

        return f.Subject == HistorySubject.Hold ? Hold(f) : Blocked(f);
    }

    private static readonly SourceReversalOutcome Unmatched = new("R13", SourceReversalVerdict.NoChange, NoChange: SourceReversalNoChange.Unmatched);

    private static bool ApprovedOrFailed(DecisionState? state) => state is DecisionState.Approved or DecisionState.ApplyFailed;

    private static SourceReversalOutcome Hold(SourceReversalFacts f)
    {
        switch (f.HoldStatus)
        {
            case HoldStatus.Open when f.ActiveDecisionId is null:
                return new("R2", SourceReversalVerdict.Voided);
            case HoldStatus.Open when f.ActiveDecisionState == DecisionState.Submitted && f.ActiveDecisionPostedLines == 0:
                return new("R3", SourceReversalVerdict.Voided);
            case HoldStatus.DecisionPending when f.ActiveDecisionId is not null:
                if (f.ActiveDecisionState == DecisionState.PendingApproval && f.ActiveDecisionPostedLines == 0)
                {
                    return new("R4", SourceReversalVerdict.Voided);
                }

                if (ApprovedOrFailed(f.ActiveDecisionState) && f.ActiveDecisionPostedLines == 0)
                {
                    return f.ActiveDecisionChangesLot && f.LotStepApplied
                        ? new("R5a", SourceReversalVerdict.Voided, SourceReversalCause.LotChanged)
                        : new("R5", SourceReversalVerdict.Voided);
                }

                return ApprovedOrFailed(f.ActiveDecisionState) && f.ActiveDecisionPostedLines >= 1
                    ? new("R6", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted)
                    : Unmatched;
            case HoldStatus.Decided:
                return new("R7", SourceReversalVerdict.Conflict, SourceReversalCause.Decided);
            default:
                return Unmatched;
        }
    }

    private static SourceReversalOutcome Blocked(SourceReversalFacts f)
    {
        switch (f.BlockedStatus)
        {
            case BlockedStatus.Open:
                var closable = f.ActiveDecisionId is null
                               || (f.ActiveDecisionState is DecisionState.Submitted or DecisionState.PendingApproval or DecisionState.Approved or DecisionState.ApplyFailed
                                   && f.ActiveDecisionPostedLines == 0);
                if (f.Quantity == f.OriginalQuantity && closable)
                {
                    return new("R8", SourceReversalVerdict.Voided);
                }

                if (f.Quantity < f.OriginalQuantity)
                {
                    return new("R9", SourceReversalVerdict.Conflict, SourceReversalCause.QuantityChanged);
                }

                return f.ActiveDecisionId is not null && ApprovedOrFailed(f.ActiveDecisionState) && f.ActiveDecisionPostedLines >= 1
                    ? new("R10", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted)
                    : Unmatched;
            case BlockedStatus.Closed or BlockedStatus.SampleRetained:
                return new("R11", SourceReversalVerdict.Conflict, SourceReversalCause.Decided);
            default:
                return Unmatched;
        }
    }
}
