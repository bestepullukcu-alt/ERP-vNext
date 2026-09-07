namespace Diten.Platform.Domain.Enums.Workflow;

public enum WorkflowStartCheckpoint
{
    None = 0,
    Reserved = 1,
    TaskPersisted = 2,
    AssignmentSnapshotPersisted = 3,
    StartLogPersisted = 4,
    Completed = 5
}
