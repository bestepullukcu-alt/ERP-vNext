namespace Diten.MdmService.Domain.Enums;

// MOD-0290-FU04 §8.1. PendingActivation is internal: the contract publishes it as Draft (first activation) or
// Deactivated (reactivation), §16.3.
public enum ItemLifecycleStatus
{
    Draft = 1,
    PendingActivation = 2,
    Active = 3,
    Deactivated = 4,
    Retired = 5
}
