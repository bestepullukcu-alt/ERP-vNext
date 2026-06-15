namespace Diten.Platform.Common.Authorization;

public enum EntitlementResolutionSource
{
    Plan = 0,
    Override = 1,
    Addon = 2,
    Trial = 3,
    Temporary = 4,
    Permission = 5,
    PlatformAdminBypass = 6,
    Unknown = 99
}
