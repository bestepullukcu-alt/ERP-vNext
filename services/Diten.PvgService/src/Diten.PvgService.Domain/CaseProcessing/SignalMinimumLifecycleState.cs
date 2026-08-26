namespace Diten.PvgService.Domain.CaseProcessing;

public enum SignalMinimumLifecycleState
{
    IntakeAccepted = 0,
    ProcessingInProgress = 1,
    EvidencePending = 2,
    AssessmentPending = 3,
    SignalMinimumReady = 4,
    HandoffToSignalQueued = 5,
    HandoffToSignalAccepted = 6,
    ClosedNoSignal = 7,
    Rejected = 8,
    Duplicate = 9,
    OnHold = 10
}
