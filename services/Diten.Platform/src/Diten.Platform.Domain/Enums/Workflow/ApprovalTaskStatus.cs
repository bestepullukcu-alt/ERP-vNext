namespace Diten.Platform.Domain.Enums.Workflow;

// MOD-0023 Batch 01: status for an approval task. Approve/reject/delegate transition logic
// lands in later batches; this batch only persists the value.
public enum ApprovalTaskStatus
{
    WaitingApproval = 0,
    WaitingEvidence = 1,
    Approved = 2,
    Rejected = 3,
    Delegated = 4,
    Escalated = 5,
    Cancelled = 6,
    TimedOut = 7
}
