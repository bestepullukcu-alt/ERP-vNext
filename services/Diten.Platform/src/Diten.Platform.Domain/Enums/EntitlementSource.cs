namespace Diten.Platform.Domain.Enums;

public enum EntitlementSource
{
    ManualOverride = 0,
    Addon = 1,
    Trial = 2,
    System = 3
}

public enum TenantModuleEffectiveAccess
{
    NoAccess = 0,
    Active = 1,
    Expired = 2,
    BlockedByOverride = 3,
    EnabledByOverride = 4,
    SystemLocked = 5
}
