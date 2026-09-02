namespace Diten.MdmService.Domain.Enums;

public enum GlobalProductIdentityWorkflowCheckpoint
{
    Prepared = 1,
    StartOutcomeUnknown = 2,
    WorkflowStarted = 3,
    LocalPendingApplied = 4,
    AwaitingDecision = 5,
    DecisionObserved = 6,
    DecisionApplied = 7,
    Completed = 8,
    AwaitingMakerReplay = 9,
    ManualReconciliationRequired = 10,
    AbandonedBeforeWorkflowStart = 11,
    Superseded = 12
}
