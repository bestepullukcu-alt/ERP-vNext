namespace Diten.MdmService.Domain.Enums;

public enum GskuCorrectionWorkflowCheckpoint
{
    Prepared = 1,
    StartOutcomeUnknown = 2,
    WorkflowStarted = 3,
    AwaitingDecision = 4,
    DecisionObserved = 5,
    DecisionApplied = 6,
    Completed = 7,
    AwaitingMakerReplay = 8,
    ManualReconciliationRequired = 9
}
