namespace Diten.MdmService.Domain.Enums;

public enum FinishedGoodIdentityWorkflowCheckpoint
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
    ManualReconciliationRequired = 11
}
