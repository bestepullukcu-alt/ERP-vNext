namespace Diten.MdmService.Domain.Enums;

public enum FirstGskuIdentityWorkflowCheckpoint
{
    Prepared = 1,
    StartOutcomeUnknown = 2,
    WorkflowStarted = 3,
    RevisionPendingApplied = 4,
    PairPendingApplied = 5,
    AwaitingDecision = 6,
    DecisionObserved = 7,
    RevisionApproved = 8,
    PairApproved = 9,
    GskuDraftRestored = 10,
    PairDraftRestored = 11,
    Completed = 12,
    AwaitingMakerReplay = 13,
    ManualReconciliationRequired = 14,
    ApprovalValidated = 15,
    AbandonedBeforeWorkflowStart = 16,
    Superseded = 17,
    WithdrawalRequested = 18,
    WithdrawalPreflightObserved = 19,
    WithdrawalOutcomeUnknown = 20,
    WithdrawalObserved = 21,
    WithdrawalApplied = 22
}
