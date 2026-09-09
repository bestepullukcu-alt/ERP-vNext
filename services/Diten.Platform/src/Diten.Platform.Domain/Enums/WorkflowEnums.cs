namespace Diten.Platform.Domain.Enums;

public enum WorkflowDefinitionStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum WorkflowInstanceStatus
{
    Running = 0,
    Completed = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum ApprovalTaskStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Delegated = 3,
    Cancelled = 4
}

public enum WorkflowEscalationAction
{
    None = 0,
    Notify = 1,
    Reassign = 2,
    Escalate = 3
}
