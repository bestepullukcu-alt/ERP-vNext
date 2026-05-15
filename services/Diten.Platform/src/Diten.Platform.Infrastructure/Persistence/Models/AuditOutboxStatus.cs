namespace Diten.Platform.Infrastructure.Persistence.Models;

public enum AuditOutboxStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    DeadLetter = 5
}
