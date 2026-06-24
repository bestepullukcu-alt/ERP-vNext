namespace Diten.Platform.Domain.Enums.Workflow;

// MOD-0023 Batch 01: action recorded on an (append-only) workflow transition log row. The transition
// engine that emits these lands in later batches; this batch only defines and persists the value.
public enum WorkflowTransitionAction
{
    Start = 0,
    Approve = 1,
    Reject = 2,
    Delegate = 3,
    Escalate = 4,
    Timeout = 5,
    RequestInfo = 6,
    Cancel = 7
}
