namespace Diten.Platform.Domain.Enums.Workflow;

// MOD-0023 Batch 01: runtime status for a workflow instance. Instance start / transition logic
// lands in later batches; this batch only persists the value.
public enum WorkflowInstanceStatus
{
    Pending = 0,
    Active = 1,
    Approved = 2,
    Rejected = 3,
    Escalated = 4,
    TimedOut = 5,
    Cancelled = 6,
    Completed = 7
}
