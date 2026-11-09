using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 pack §17 "Birim": the §8.3 table row by row (R0a–R13, R5a with lot key × ChangesLot), the §4.1 origin table and its
/// cross-check, <see cref="QuarantineRecordStates"/>, and <c>StateOf</c>'s Voided mapping and absorption. AC-2 (b): a combination no row
/// names is <c>NoChange (Unmatched)</c>, never Void.
/// </summary>
public sealed class SourceReversalRulesTests
{
    private const string Reversal = "11111111-1111-1111-1111-111111111111";
    private const string OtherReversal = "22222222-2222-2222-2222-222222222222";

    private static SourceReversalFacts Hold(HoldStatus status, DecisionState? state = null, int posted = 0, RecordOrigin origin = RecordOrigin.Intake,
        bool changesLot = false, bool lotApplied = false) => new()
    {
        Subject = HistorySubject.Hold, Origin = origin, HoldStatus = status, ReversalTransactionId = Reversal,
        ActiveDecisionId = state is null ? null : Guid.NewGuid(), ActiveDecisionState = state, ActiveDecisionPostedLines = posted,
        ActiveDecisionChangesLot = changesLot, LotStepApplied = lotApplied, Quantity = 4m, OriginalQuantity = 4m
    };

    private static SourceReversalFacts Blocked(BlockedStatus status, DecisionState? state = null, int posted = 0, decimal quantity = 3m,
        RecordOrigin origin = RecordOrigin.Intake) => new()
    {
        Subject = HistorySubject.Blocked, Origin = origin, BlockedStatus = status, ReversalTransactionId = Reversal,
        ActiveDecisionId = state is null ? null : Guid.NewGuid(), ActiveDecisionState = state, ActiveDecisionPostedLines = posted,
        Quantity = quantity, OriginalQuantity = 3m
    };

    private static void Expect(SourceReversalFacts facts, string rule, SourceReversalVerdict verdict, SourceReversalCause? cause = null, SourceReversalNoChange? noChange = null)
    {
        var outcome = SourceReversalRules.Evaluate(facts);
        Assert.Equal((rule, verdict, cause, noChange), (outcome.Rule, outcome.Verdict, outcome.Cause, outcome.NoChange));
    }

    [Fact]
    public void Hold_rows_R1_to_R7()
    {
        Expect(Hold(HoldStatus.Open), "R2", SourceReversalVerdict.Voided);
        Expect(Hold(HoldStatus.Open, DecisionState.Submitted), "R3", SourceReversalVerdict.Voided);
        Expect(Hold(HoldStatus.DecisionPending, DecisionState.PendingApproval), "R4", SourceReversalVerdict.Voided);
        Expect(Hold(HoldStatus.DecisionPending, DecisionState.Approved), "R5", SourceReversalVerdict.Voided);
        Expect(Hold(HoldStatus.DecisionPending, DecisionState.ApplyFailed), "R5", SourceReversalVerdict.Voided);
        Expect(Hold(HoldStatus.DecisionPending, DecisionState.Approved, posted: 1), "R6", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
        Expect(Hold(HoldStatus.DecisionPending, DecisionState.ApplyFailed, posted: 2), "R6", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
        Expect(Hold(HoldStatus.Decided), "R7", SourceReversalVerdict.Conflict, SourceReversalCause.Decided);
        foreach (var status in new[] { HoldStatus.Open, HoldStatus.DecisionPending, HoldStatus.Decided })
        {
            Expect(Hold(status, status == HoldStatus.DecisionPending ? DecisionState.PendingApproval : null, origin: RecordOrigin.Quarantine),
                "R1", SourceReversalVerdict.Conflict, SourceReversalCause.OwnMovementReversed);
        }
    }

    /// <summary>R5a (r5): the lot key decides, and only for a decision that changes its lot — the four combinations.</summary>
    [Theory]
    [InlineData(true, true, "R5a")]
    [InlineData(true, false, "R5")]
    [InlineData(false, true, "R5")]
    [InlineData(false, false, "R5")]
    public void R5a_needs_a_lot_changing_decision_and_its_lot_key(bool changesLot, bool lotApplied, string rule)
    {
        foreach (var state in new[] { DecisionState.Approved, DecisionState.ApplyFailed })
        {
            var outcome = SourceReversalRules.Evaluate(Hold(HoldStatus.DecisionPending, state, changesLot: changesLot, lotApplied: lotApplied));
            Assert.Equal(rule, outcome.Rule);
            Assert.Equal(SourceReversalVerdict.Voided, outcome.Verdict);
            Assert.Equal(rule == "R5a" ? SourceReversalCause.LotChanged : null, outcome.Cause);
            Assert.Equal(rule == "R5a", outcome.VoidsAndMarks);
        }
    }

    [Fact]
    public void Blocked_rows_R8_to_R11()
    {
        Expect(Blocked(BlockedStatus.Open), "R8", SourceReversalVerdict.Voided);
        foreach (var state in new[] { DecisionState.Submitted, DecisionState.PendingApproval, DecisionState.Approved, DecisionState.ApplyFailed })
        {
            Expect(Blocked(BlockedStatus.Open, state), "R8", SourceReversalVerdict.Voided);
        }

        Expect(Blocked(BlockedStatus.Open, quantity: 2m), "R9", SourceReversalVerdict.Conflict, SourceReversalCause.QuantityChanged);
        Expect(Blocked(BlockedStatus.Open, DecisionState.PendingApproval, quantity: 2m), "R9", SourceReversalVerdict.Conflict, SourceReversalCause.QuantityChanged);
        Expect(Blocked(BlockedStatus.Open, DecisionState.Approved, posted: 1), "R10", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
        Expect(Blocked(BlockedStatus.Closed), "R11", SourceReversalVerdict.Conflict, SourceReversalCause.Decided);
        Expect(Blocked(BlockedStatus.SampleRetained), "R11", SourceReversalVerdict.Conflict, SourceReversalCause.Decided);
        Expect(Blocked(BlockedStatus.Open, origin: RecordOrigin.Quarantine), "R1", SourceReversalVerdict.Conflict, SourceReversalCause.OwnMovementReversed);
        Expect(Blocked(BlockedStatus.Closed, origin: RecordOrigin.Quarantine), "R1", SourceReversalVerdict.Conflict, SourceReversalCause.OwnMovementReversed);
    }

    [Fact]
    public void R0a_R0_and_R12_recorded_reversals()
    {
        var decision = Guid.NewGuid();
        var voided = Hold(HoldStatus.Voided) with { VoidedReversalTransactionId = Reversal, VoidedDecisionId = decision, VoidedDecisionPostedLines = 1 };
        Expect(voided, "R0a", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
        // no posted line of the voided decision, no decision, or already marked → R0 (nothing written)
        Expect(voided with { VoidedDecisionPostedLines = 0 }, "R0", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        Expect(voided with { VoidedDecisionId = null }, "R0", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        Expect(voided with { ConflictReversalTransactionId = Reversal }, "R0", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        // a MOD-0175 record is never voided, so R0a is intake only
        Expect(voided with { Origin = RecordOrigin.Quarantine }, "R0", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        // the mark of this reversal is written already (any status)
        Expect(Hold(HoldStatus.Decided) with { ConflictReversalTransactionId = Reversal }, "R0", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        // R12: voided by another reversal (G-5 says it cannot happen)
        Expect(Hold(HoldStatus.Voided) with { VoidedReversalTransactionId = OtherReversal }, "R12", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        Expect(Blocked(BlockedStatus.Voided) with { VoidedReversalTransactionId = OtherReversal }, "R12", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.AlreadyRecorded);
        Expect(Blocked(BlockedStatus.Voided) with { VoidedReversalTransactionId = Reversal, VoidedDecisionId = decision, VoidedDecisionPostedLines = 1 },
            "R0a", SourceReversalVerdict.Conflict, SourceReversalCause.LinesPosted);
    }

    /// <summary>AC-2 (b): every combination outside the table is NoChange (Unmatched) — never Void, never an exception.</summary>
    [Fact]
    public void Unmatched_combinations_are_R13_never_Void()
    {
        var unmatched = new List<SourceReversalFacts>
        {
            Hold(HoldStatus.DecisionPending, DecisionState.Draft),
            Hold(HoldStatus.DecisionPending, DecisionState.Withdrawn),
            Hold(HoldStatus.DecisionPending, DecisionState.StartFailed),
            Hold(HoldStatus.DecisionPending, DecisionState.Submitted),
            Hold(HoldStatus.DecisionPending, DecisionState.Rejected),
            Hold(HoldStatus.DecisionPending, DecisionState.Cancelled),
            Hold(HoldStatus.DecisionPending, DecisionState.TimedOut),
            Hold(HoldStatus.DecisionPending, DecisionState.FourEyesViolation),
            Hold(HoldStatus.DecisionPending) with { ActiveDecisionId = null },
            Hold(HoldStatus.Open, DecisionState.PendingApproval),
            Hold(HoldStatus.Open, DecisionState.Approved),
            Hold((HoldStatus)99),
            Hold(HoldStatus.DecisionPending, (DecisionState)99),
            Hold((HoldStatus)99, origin: RecordOrigin.Quarantine),
            Blocked((BlockedStatus)99),
            Blocked(BlockedStatus.Open, DecisionState.Draft),
            Blocked(BlockedStatus.Open, quantity: 5m)
        };
        Assert.All(unmatched, f => Expect(f, "R13", SourceReversalVerdict.NoChange, noChange: SourceReversalNoChange.Unmatched));
    }

    [Fact]
    public void Origin_table_and_the_cross_check_fail_closed()
    {
        foreach (var source in new[] { HoldSource.GoodsReceipt, HoldSource.Production, HoldSource.CustomerReturn, HoldSource.OtherInbound })
        {
            Assert.Equal((RecordOrigin.Intake, false), SourceReversalRules.OriginOf(source, false));
            Assert.Equal((RecordOrigin.Quarantine, true), SourceReversalRules.OriginOf(source, true)); // the movement is MOD-0175's
            Assert.Equal((RecordOrigin.Quarantine, true), SourceReversalRules.OriginOf(source, null)); // unreadable
        }

        foreach (var source in new[] { HoldSource.Manual, HoldSource.Retest, HoldSource.Recall, HoldSource.Rework })
        {
            Assert.Equal((RecordOrigin.Quarantine, false), SourceReversalRules.OriginOf(source, true));
            Assert.Equal((RecordOrigin.Quarantine, false), SourceReversalRules.OriginOf(source, false));
        }

        Assert.Equal((RecordOrigin.Quarantine, true), SourceReversalRules.OriginOf((HoldSource)99, false)); // a value the table does not name
        Assert.Equal((RecordOrigin.Intake, false), SourceReversalRules.OriginOf(BlockedOrigin.OtherInbound, false));
        Assert.Equal((RecordOrigin.Quarantine, true), SourceReversalRules.OriginOf(BlockedOrigin.OtherInbound, true));
        Assert.Equal((RecordOrigin.Quarantine, false), SourceReversalRules.OriginOf(BlockedOrigin.ManualBlock, true));
        foreach (var origin in new[] { BlockedOrigin.QuarantineDecision, BlockedOrigin.LotBlocked, BlockedOrigin.Rework, (BlockedOrigin)99 })
        {
            Assert.Equal((RecordOrigin.Quarantine, true), SourceReversalRules.OriginOf(origin, false));
        }
    }

    [Fact]
    public void Open_and_terminal_predicate()
    {
        Assert.True(QuarantineRecordStates.IsOpen(HoldStatus.Open));
        Assert.True(QuarantineRecordStates.IsOpen(HoldStatus.DecisionPending));
        Assert.False(QuarantineRecordStates.IsOpen(HoldStatus.Decided));
        Assert.False(QuarantineRecordStates.IsOpen(HoldStatus.Voided));
        Assert.True(QuarantineRecordStates.IsTerminal(HoldStatus.Decided));
        Assert.True(QuarantineRecordStates.IsTerminal(HoldStatus.Voided));
        Assert.True(QuarantineRecordStates.IsTerminal("Voided"));
        Assert.False(QuarantineRecordStates.IsTerminal("Open"));
        Assert.Equal([HoldStatus.Open, HoldStatus.DecisionPending], QuarantineRecordStates.OpenHoldStatuses);
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineDecisionRules.HoldState(HoldStatus.Voided, null)!.Code);
        Assert.Equal("QUARANTINE_BLOCKED_CLOSED", QuarantineDecisionRules.BlockedState(BlockedStatus.Voided, null)!.Code);
    }

    private static QuarantineDecisionEvent E(DecisionEventKind kind, long sequence) => new() { Kind = kind, Sequence = sequence };

    /// <summary>§4.2 / §8.6 invariant 1: Voided maps, and once present it is the state whatever comes after it (every kind, Withdrawn included).</summary>
    [Fact]
    public void StateOf_maps_Voided_and_Voided_is_absorbing()
    {
        Assert.Equal(DecisionState.Voided, QuarantineFacts.StateOf([E(DecisionEventKind.Submitted, 1), E(DecisionEventKind.Voided, 2)]));
        foreach (var later in Enum.GetValues<DecisionEventKind>().Where(k => k != DecisionEventKind.Voided))
        {
            Assert.Equal(DecisionState.Voided, QuarantineFacts.StateOf([E(DecisionEventKind.Submitted, 1), E(DecisionEventKind.Voided, 2), E(later, 3)]));
            Assert.Equal(DecisionState.Voided, QuarantineFacts.StateOf([E(later, 3), E(DecisionEventKind.Voided, 2), E(DecisionEventKind.Submitted, 1)]));
        }

        Assert.Equal(DecisionState.Rejected, QuarantineFacts.StateOf([E(DecisionEventKind.WorkflowStarted, 1), E(DecisionEventKind.Rejected, 2)]));
        Assert.Equal("Voided", QuarantineSupport.Event(Guid.NewGuid(), DecisionEventKind.Voided, DateTimeOffset.UtcNow, null).OnceKey);
    }
}
