namespace Diten.PvgService.Domain.CaseProcessing;

public enum SafetyCaseMasterStatus
{
    PendingHandoffAcceptance = 0,
    HandoffAccepted = 1,
    InProcessing = 2,
    AssessmentUpdated = 3,
    SignalMinimumReady = 4,
    HandoffToSignalQueued = 5,
    HandoffToSignalAccepted = 6,
    ClosedNoSignal = 7
}
