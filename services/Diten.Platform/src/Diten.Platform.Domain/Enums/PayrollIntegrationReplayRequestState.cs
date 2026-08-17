namespace Diten.Platform.Domain.Enums;

public enum PayrollIntegrationReplayRequestState
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    Executed = 4,
    Cancelled = 5,
    Blocked = 6
}
