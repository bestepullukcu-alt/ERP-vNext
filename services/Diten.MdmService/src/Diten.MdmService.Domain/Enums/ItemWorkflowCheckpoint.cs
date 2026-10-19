namespace Diten.MdmService.Domain.Enums;

// MOD-0290-FU04 S2: the item activation approval's checkpoints — the LSKU identity flow's (§8.4, the accepted GSKU
// pattern), value for value, so the same store invariants and the same gauge rules read the same way.
public enum ItemWorkflowCheckpoint
{
    Prepared = 1,
    StartOutcomeUnknown = 2,
    WorkflowStarted = 3,
    LocalPendingApplied = 4,
    AwaitingDecision = 5,
    DecisionObserved = 6,
    ApprovalValidated = 7,
    DecisionApplied = 8,
    Completed = 9,
    AwaitingMakerReplay = 10,
    ManualReconciliationRequired = 11,
    AbandonedBeforeWorkflowStart = 12,
    Superseded = 13,
    WithdrawalRequested = 14,
    WithdrawalPreflightObserved = 15,
    WithdrawalOutcomeUnknown = 16,
    WithdrawalObserved = 17,
    WithdrawalApplied = 18,
    // The engine decided and MDM does not apply it (self-approval, closed without a decision, the activation gate no
    // longer met): the item is back where the request found it — Draft, or Deactivated for a reactivation.
    DecisionNotApplicable = 19
}
