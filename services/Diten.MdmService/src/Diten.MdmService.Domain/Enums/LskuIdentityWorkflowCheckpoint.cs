namespace Diten.MdmService.Domain.Enums;

public enum LskuIdentityWorkflowCheckpoint
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
    WithdrawalApplied = 18
}
