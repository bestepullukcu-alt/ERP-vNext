namespace Diten.Platform.Domain.Enums;

public enum TenantSubscriptionStatus
{
    PendingProvisioning = 0,
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Cancelled = 4,
    Expired = 5,
    Suspended = 6,
    TrialExpired = 7
}
