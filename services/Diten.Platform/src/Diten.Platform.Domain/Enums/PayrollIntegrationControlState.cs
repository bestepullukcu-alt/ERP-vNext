namespace Diten.Platform.Domain.Enums;

public enum PayrollIntegrationControlState
{
    Pending = 1,
    Deferred = 2,
    Validated = 3,
    Mismatch = 4,
    Waived = 5,
    Blocked = 6,
    Governed = 90,
    Approved = 91,
    Mapped = 92
}
