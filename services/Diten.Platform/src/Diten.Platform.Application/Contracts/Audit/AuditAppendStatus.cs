namespace Diten.Platform.Application.Contracts.Audit;

public enum AuditAppendStatus
{
    Queued = 1,
    Duplicate = 2,
    SkippedRecursion = 3,
    Rejected = 4,
    EnqueueFailed = 5
}
