namespace Diten.Platform.Domain.Enums;

public enum WorkTaskStatus
{
    Open = 0,
    Assigned = 1,
    Completed = 2,
    Cancelled = 3
}

public enum TaskEscalationPolicy
{
    None = 0,
    Notify = 1,
    Reassign = 2
}

public enum ChecklistTemplateStatus
{
    Active = 0,
    Archived = 1
}

public enum ChecklistRunStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2
}
