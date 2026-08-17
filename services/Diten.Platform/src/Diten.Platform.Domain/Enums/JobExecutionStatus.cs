namespace Diten.Platform.Domain.Enums;

public enum JobExecutionStatus
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    Retrying = 3,
    Cancelled = 4,
    DeadLettered = 5,
    Skipped = 6
}
