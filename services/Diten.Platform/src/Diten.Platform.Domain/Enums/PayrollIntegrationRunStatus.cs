namespace Diten.Platform.Domain.Enums;

public enum PayrollIntegrationRunStatus
{
    Draft = 1,
    Queued = 2,
    Running = 3,
    Completed = 4,
    CompletedWithExceptions = 5,
    Failed = 6,
    Cancelled = 7,
    Blocked = 8
}
